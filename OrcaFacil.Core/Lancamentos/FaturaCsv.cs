using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OrcaFacil.Core.Cesta;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Lancamentos;

/// <summary>
/// Lê o CSV padronizado da fatura. Não grava no banco: devolve linhas válidas e erros por linha.
/// Erro de versão, cabeçalho ou codificação recusa o arquivo inteiro.
/// </summary>
public static class FaturaCsv
{
    private const int DescricaoMaxima = 120;
    private const decimal ValorMaximo = 1_000_000_000m;
    private const string ColunaEstrutura = "Linha";

    private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;
    private static readonly UTF8Encoding Utf8Estrito = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public sealed record Linha(
        int Numero,
        DateTime Data,
        string Descricao,
        decimal Valor,
        bool EhEstorno,
        int CategoriaId,
        string NomeCategoria,
        NaturezaItemLancamento Natureza,
        int? ParcelaAtual,
        int? TotalParcelas,
        string Descritivo,
        string Hash);

    public sealed record ErroLinha(int Numero, string Coluna, string Mensagem);

    public sealed record Leitura(
        IReadOnlyList<Linha> Linhas,
        IReadOnlyList<ErroLinha> Erros,
        string? ErroArquivo)
    {
        public static Leitura Recusar(string erroArquivo) => new([], [], erroArquivo);

        public bool ArquivoAceito => ErroArquivo is null;
    }

    public static Leitura Ler(byte[] arquivo, IReadOnlyList<Categoria> categorias)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        ArgumentNullException.ThrowIfNull(categorias);

        if (arquivo.Length > FaturaCsvLayout.TamanhoMaximoBytes)
            return Leitura.Recusar("O arquivo passa de 1 MB. Nada foi importado.");

        if (!TentarDecodificar(arquivo, out var texto, out var erroCodificacao))
            return Leitura.Recusar(erroCodificacao!);

        if (string.IsNullOrWhiteSpace(texto))
            return Leitura.Recusar("O arquivo está vazio. Nada foi importado.");

        var registros = DividirRegistros(texto);
        if (!TentarAceitarPrologo(registros, out var erroPrologo))
            return Leitura.Recusar(erroPrologo!);

        var mapa = MapearCategorias(categorias);
        var linhas = new List<Linha>();
        var erros = new List<ErroLinha>();

        for (var i = 2; i < registros.Count; i++)
        {
            var bruta = registros[i].Texto;
            if (string.IsNullOrWhiteSpace(bruta) || FaturaCsvLayout.EhLinhaExemplo(bruta))
                continue;

            LerLinha(bruta, registros[i].Numero, mapa, linhas, erros);
        }

        return new Leitura(linhas, erros, null);
    }

    /// <summary>
    /// Identificação estável da linha já normalizada. A importação compara este hash
    /// com os itens da fatura para não gravar a mesma linha duas vezes.
    /// A hora não entra. O valor é a magnitude positiva; o estorno entra à parte.
    /// </summary>
    public static string CalcularHash(
        DateTime data,
        string descricao,
        decimal valor,
        bool ehEstorno,
        int categoriaId,
        NaturezaItemLancamento natureza,
        int? parcelaAtual,
        int? totalParcelas,
        string descritivo)
    {
        var canonico = string.Join('\u001f',
            FaturaCsvLayout.Versao.ToString(Invariante),
            data.Date.ToString("yyyy-MM-dd", Invariante),
            descricao ?? string.Empty,
            valor.ToString("0.00", Invariante),
            ehEstorno ? "1" : "0",
            categoriaId.ToString(Invariante),
            natureza.ToString(),
            parcelaAtual?.ToString(Invariante) ?? string.Empty,
            totalParcelas?.ToString(Invariante) ?? string.Empty,
            descritivo ?? string.Empty);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonico)));
    }

    private static bool TentarDecodificar(byte[] arquivo, out string texto, out string? erro)
    {
        texto = string.Empty;
        if (arquivo.Length < 3 || arquivo[0] != 0xEF || arquivo[1] != 0xBB || arquivo[2] != 0xBF)
        {
            erro = "O arquivo precisa estar em UTF-8 com BOM. Nada foi importado.";
            return false;
        }

        try
        {
            texto = Utf8Estrito.GetString(arquivo, 3, arquivo.Length - 3);
            erro = null;
            return true;
        }
        catch (DecoderFallbackException)
        {
            erro = "O arquivo não está em UTF-8 válido. Nada foi importado.";
            return false;
        }
    }

    private readonly record struct RegistroCsv(int Numero, string Texto);

    /// <summary>
    /// Quebra de linha fora das aspas separa o registro. Dentro das aspas, ela faz parte do campo.
    /// O número é a linha física em que o registro começa.
    /// </summary>
    private static List<RegistroCsv> DividirRegistros(string texto)
    {
        var normalizado = texto.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var registros = new List<RegistroCsv>();
        var atual = new StringBuilder();
        var aspas = false;
        var linhaFisica = 1;
        var inicio = 1;

        for (var i = 0; i < normalizado.Length; i++)
        {
            var c = normalizado[i];
            if (c == '"')
            {
                atual.Append(c);
                if (aspas && i + 1 < normalizado.Length && normalizado[i + 1] == '"')
                {
                    atual.Append('"');
                    i++;
                    continue;
                }

                aspas = !aspas;
                continue;
            }

            if (c == '\n' && !aspas)
            {
                registros.Add(new RegistroCsv(inicio, atual.ToString()));
                atual.Clear();
                linhaFisica++;
                inicio = linhaFisica;
                continue;
            }

            if (c == '\n')
                linhaFisica++;

            atual.Append(c);
        }

        registros.Add(new RegistroCsv(inicio, atual.ToString()));
        return registros;
    }

    private static bool TentarAceitarPrologo(IReadOnlyList<RegistroCsv> linhas, out string? erro)
    {
        if (linhas.Count == 0 || string.IsNullOrWhiteSpace(linhas[0].Texto))
        {
            erro = "O arquivo está vazio. Nada foi importado.";
            return false;
        }

        erro = ErroDeVersao(linhas[0].Texto);
        if (erro is not null)
            return false;

        if (linhas.Count < 2 || string.IsNullOrWhiteSpace(linhas[1].Texto))
        {
            erro = $"O arquivo não tem o cabeçalho. A segunda linha precisa ser exatamente {FaturaCsvLayout.Cabecalho}. Nada foi importado.";
            return false;
        }

        if (!string.Equals(linhas[1].Texto, FaturaCsvLayout.Cabecalho, StringComparison.Ordinal))
        {
            erro = $"O cabeçalho não confere com o modelo. A segunda linha precisa ser exatamente {FaturaCsvLayout.Cabecalho}. Nada foi importado.";
            return false;
        }

        return true;
    }

    private static string? ErroDeVersao(string linha)
    {
        if (string.Equals(linha, FaturaCsvLayout.LinhaVersao, StringComparison.Ordinal))
            return null;

        const string marcador = "OrcaFacil;Fatura;";
        if (linha.StartsWith(marcador, StringComparison.Ordinal))
        {
            var informado = linha[marcador.Length..];
            if (informado.Length == 0)
                return "A versão do arquivo não foi informada. A primeira linha precisa ser OrcaFacil;Fatura;1. Nada foi importado.";

            if (int.TryParse(informado, NumberStyles.None, Invariante, out var versao) && versao != FaturaCsvLayout.Versao)
                return $"A versão {versao} do arquivo não é suportada. Nada foi importado. Baixe o modelo da versão {FaturaCsvLayout.Versao}.";
        }

        return "O arquivo não é uma fatura do OrcaFacil. A primeira linha precisa ser OrcaFacil;Fatura;1. Nada foi importado.";
    }

    private static Dictionary<string, Categoria> MapearCategorias(IReadOnlyList<Categoria> categorias)
    {
        var mapa = new Dictionary<string, Categoria>(StringComparer.OrdinalIgnoreCase);
        foreach (var categoria in categorias)
        {
            if (string.IsNullOrWhiteSpace(categoria.Nome))
                continue;

            mapa.TryAdd(categoria.Nome.Trim(), categoria);
        }

        return mapa;
    }

    private static void LerLinha(
        string bruta,
        int numero,
        Dictionary<string, Categoria> categorias,
        List<Linha> linhas,
        List<ErroLinha> erros)
    {
        if (!AspasFechadas(bruta))
        {
            erros.Add(new ErroLinha(numero, ColunaEstrutura, "Feche as aspas da linha."));
            return;
        }

        var campos = ProdutoCestaCsv.Separar(bruta, FaturaCsvLayout.Separador);
        if (campos.Count != FaturaCsvLayout.Colunas.Count)
        {
            erros.Add(new ErroLinha(
                numero,
                ColunaEstrutura,
                "A linha precisa ter 8 colunas separadas por ponto e vírgula, como no modelo."));
            return;
        }

        var dataTexto = campos[0].Trim();
        var descricao = LerTexto(campos[1]);
        var valorTexto = campos[2].Trim();
        var categoriaTexto = LerTexto(campos[3]);
        var naturezaTexto = LerTexto(campos[4]);
        var parcelaTexto = campos[5].Trim();
        var totalTexto = campos[6].Trim();
        var descritivo = LerTexto(campos[7]);
        var daLinha = new List<ErroLinha>();

        DateTime data = default;
        if (dataTexto.Length == 0)
            daLinha.Add(new ErroLinha(numero, FaturaCsvLayout.ColunaData, "Informe a data."));
        else if (!DateTime.TryParseExact(dataTexto, "dd'/'MM'/'yyyy", Invariante, DateTimeStyles.None, out data))
            daLinha.Add(new ErroLinha(numero, FaturaCsvLayout.ColunaData, "Use a data no formato dd/MM/yyyy."));

        if (descricao.Length == 0)
            daLinha.Add(new ErroLinha(numero, FaturaCsvLayout.ColunaDescricao, "Informe a descrição."));
        else if (descricao.Length > DescricaoMaxima)
            daLinha.Add(new ErroLinha(numero, FaturaCsvLayout.ColunaDescricao, $"A descrição pode ter no máximo {DescricaoMaxima} caracteres."));

        var valorAceito = false;
        var ehEstorno = false;
        decimal magnitude = 0;
        if (valorTexto.Length == 0)
        {
            daLinha.Add(new ErroLinha(numero, FaturaCsvLayout.ColunaValor, "Informe o valor."));
        }
        else if (!TentarLerValor(valorTexto, out magnitude, out var negativo))
        {
            daLinha.Add(new ErroLinha(numero, FaturaCsvLayout.ColunaValor, "Use o valor no formato 1234,56, sem R$ e sem ponto."));
        }
        else if (magnitude <= 0)
        {
            daLinha.Add(new ErroLinha(numero, FaturaCsvLayout.ColunaValor, "Informe um valor maior que zero."));
        }
        else if (magnitude > ValorMaximo)
        {
            daLinha.Add(new ErroLinha(numero, FaturaCsvLayout.ColunaValor, "O valor passa do limite permitido."));
        }
        else
        {
            valorAceito = true;
            ehEstorno = negativo;
        }

        Categoria? categoria = null;
        if (categoriaTexto.Length == 0)
            daLinha.Add(new ErroLinha(numero, FaturaCsvLayout.ColunaCategoria, "Informe a categoria."));
        else if (!categorias.TryGetValue(categoriaTexto, out categoria))
            daLinha.Add(new ErroLinha(numero, FaturaCsvLayout.ColunaCategoria, $"A categoria \"{categoriaTexto}\" não existe."));

        NaturezaItemLancamento? natureza = null;
        if (naturezaTexto.Length == 0)
            natureza = NaturezaItemLancamento.Avulsa;
        else if (!TentarNatureza(naturezaTexto, out var naturezaLida))
            daLinha.Add(new ErroLinha(numero, FaturaCsvLayout.ColunaNatureza, "Use Avulsa, Parcelada ou Assinatura."));
        else
            natureza = naturezaLida;

        var estornoIncompativel = valorAceito && ehEstorno && natureza is not null && natureza != NaturezaItemLancamento.Avulsa;
        if (estornoIncompativel)
            daLinha.Add(new ErroLinha(numero, FaturaCsvLayout.ColunaNatureza, "Estorno só pode ser avulso."));

        int? parcelaAtual = null;
        int? totalParcelas = null;
        if (natureza is not null && !estornoIncompativel)
            ValidarParcelas(numero, natureza.Value, parcelaTexto, totalTexto, daLinha, ref parcelaAtual, ref totalParcelas);

        if (descritivo.Length > ItensFaturaInclusao.DescritivoMaximo)
        {
            daLinha.Add(new ErroLinha(
                numero,
                FaturaCsvLayout.ColunaDescritivo,
                $"O descritivo pode ter no máximo {ItensFaturaInclusao.DescritivoMaximo} caracteres."));
        }

        if (daLinha.Count > 0)
        {
            erros.AddRange(daLinha);
            return;
        }

        var descritivoFinal = descritivo.Length == 0 ? descricao : descritivo;
        var naturezaFinal = natureza!.Value;
        linhas.Add(new Linha(
            numero,
            data.Date,
            descricao,
            magnitude,
            ehEstorno,
            categoria!.Id,
            categoria.Nome.Trim(),
            naturezaFinal,
            parcelaAtual,
            totalParcelas,
            descritivoFinal,
            CalcularHash(data, descricao, magnitude, ehEstorno, categoria.Id, naturezaFinal, parcelaAtual, totalParcelas, descritivoFinal)));
    }

    private static void ValidarParcelas(
        int numero,
        NaturezaItemLancamento natureza,
        string parcelaTexto,
        string totalTexto,
        List<ErroLinha> erros,
        ref int? parcelaAtual,
        ref int? totalParcelas)
    {
        var temParcela = parcelaTexto.Length > 0;
        var temTotal = totalTexto.Length > 0;

        if (natureza != NaturezaItemLancamento.Parcelada)
        {
            if (temParcela || temTotal)
            {
                erros.Add(new ErroLinha(
                    numero,
                    temParcela ? FaturaCsvLayout.ColunaParcelaAtual : FaturaCsvLayout.ColunaTotalParcelas,
                    "Parcela atual e total de parcelas só valem para a natureza Parcelada."));
            }

            return;
        }

        if (!temParcela && !temTotal)
        {
            erros.Add(new ErroLinha(numero, FaturaCsvLayout.ColunaParcelaAtual, "Informe a parcela atual e o total de parcelas."));
            return;
        }

        if (!temParcela || !temTotal)
        {
            erros.Add(new ErroLinha(
                numero,
                temParcela ? FaturaCsvLayout.ColunaTotalParcelas : FaturaCsvLayout.ColunaParcelaAtual,
                "Informe a parcela atual e o total de parcelas juntas."));
            return;
        }

        if (!TentarInteiro(parcelaTexto, out var atual))
            erros.Add(new ErroLinha(numero, FaturaCsvLayout.ColunaParcelaAtual, "Use um número inteiro na parcela atual."));
        else if (atual < 1)
            erros.Add(new ErroLinha(numero, FaturaCsvLayout.ColunaParcelaAtual, "A parcela atual precisa ser maior que zero."));
        else
            parcelaAtual = atual;

        if (!TentarInteiro(totalTexto, out var total))
            erros.Add(new ErroLinha(numero, FaturaCsvLayout.ColunaTotalParcelas, "Use um número inteiro no total de parcelas."));
        else if (total < 1)
            erros.Add(new ErroLinha(numero, FaturaCsvLayout.ColunaTotalParcelas, "O total de parcelas precisa ser maior que zero."));
        else
            totalParcelas = total;

        if (parcelaAtual is not null && totalParcelas is not null && parcelaAtual > totalParcelas)
            erros.Add(new ErroLinha(numero, FaturaCsvLayout.ColunaParcelaAtual, "A parcela atual não pode ser maior que o total de parcelas."));
    }

    private static string LerTexto(string campo)
    {
        var texto = campo.Trim();
        if (texto.Length >= 2 && texto[0] == '\'' && texto[1] is '=' or '+' or '-' or '@')
            return texto[1..];

        return texto;
    }

    private static bool TentarInteiro(string texto, out int valor)
        => int.TryParse(texto, NumberStyles.None, Invariante, out valor);

    private static bool TentarNatureza(string texto, out NaturezaItemLancamento natureza)
    {
        foreach (var candidata in Enum.GetValues<NaturezaItemLancamento>())
        {
            if (string.Equals(candidata.ToString(), texto, StringComparison.OrdinalIgnoreCase))
            {
                natureza = candidata;
                return true;
            }
        }

        natureza = default;
        return false;
    }

    private static bool TentarLerValor(string texto, out decimal magnitude, out bool negativo)
    {
        magnitude = 0;
        negativo = false;
        if (texto.Length == 0)
            return false;

        var indice = 0;
        if (texto[0] == '-')
        {
            negativo = true;
            indice = 1;
        }

        if (indice >= texto.Length || !char.IsAsciiDigit(texto[indice]))
            return false;

        var virgula = texto.IndexOf(',', indice);
        var fimInteiro = virgula >= 0 ? virgula : texto.Length;
        for (var i = indice; i < fimInteiro; i++)
        {
            if (!char.IsAsciiDigit(texto[i]))
                return false;
        }

        var centavos = 0;
        if (virgula >= 0)
        {
            var fracao = texto[(virgula + 1)..];
            if (fracao.Length is < 1 or > 2)
                return false;

            foreach (var digito in fracao)
            {
                if (!char.IsAsciiDigit(digito))
                    return false;
            }

            centavos = fracao.Length == 1
                ? (fracao[0] - '0') * 10
                : (fracao[0] - '0') * 10 + (fracao[1] - '0');
        }

        var inteiro = texto[indice..fimInteiro];
        if (!decimal.TryParse(inteiro, NumberStyles.None, Invariante, out var reais))
            return false;

        magnitude = reais + centavos / 100m;
        return true;
    }

    private static bool AspasFechadas(string linha)
    {
        var dentro = false;
        for (var i = 0; i < linha.Length; i++)
        {
            if (linha[i] != '"')
                continue;

            if (dentro && i + 1 < linha.Length && linha[i + 1] == '"')
            {
                i++;
                continue;
            }

            dentro = !dentro;
        }

        return !dentro;
    }
}
