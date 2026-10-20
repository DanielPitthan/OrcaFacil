using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using OrcaFacil.Core.Ativos;

namespace OrcaFacil.Core.B3;

/// <summary>
/// Lê o CSV do extrato de movimentação da B3 (separador ';', UTF-8 com ou sem BOM,
/// ou Windows-1252). Não calcula posição, não remove duplicata e não grava.
/// A data de liquidação do arquivo é a data da operação. O valor de referência é
/// Valor da Operação. Renda fixa permanece no resultado.
/// Registro inválido vai para <see cref="ResultadoParseB3.Erros"/> com o motivo;
/// não é descartado em silêncio. Linha em branco não é registro.
/// </summary>
public sealed class ParserExtratoB3 : IParserExtratoB3
{
    private const string FormatoData = "dd/MM/yyyy";
    private const string MensagemCabecalho =
        "O cabeçalho não é o do extrato da B3 (Entrada/Saída, Data, Movimentação, Produto, Instituição, Quantidade, Preço unitário e Valor da Operação).";
    private static readonly CultureInfo CulturaData = CultureInfo.InvariantCulture;
    private static readonly UTF8Encoding Utf8Estrito = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly string[] ColunasEsperadas =
    [
        "entrada/saida",
        "data",
        "movimentacao",
        "produto",
        "instituicao",
        "quantidade",
        "preco unitario",
        "valor da operacao"
    ];

    private readonly Dictionary<string, RegraMovimentacaoB3> _mapeamento;

    public ParserExtratoB3()
        : this(TabelaMovimentacaoB3.Padrao)
    {
    }

    public ParserExtratoB3(IReadOnlyDictionary<string, RegraMovimentacaoB3> mapeamento)
    {
        ArgumentNullException.ThrowIfNull(mapeamento);
        _mapeamento = new Dictionary<string, RegraMovimentacaoB3>(StringComparer.Ordinal);
        foreach (var par in mapeamento)
        {
            ArgumentNullException.ThrowIfNull(par.Value);
            var chave = TextoB3.NormalizarChave(par.Key);
            if (chave.Length == 0)
                throw new ArgumentException("A tabela de movimentação não aceita texto vazio.", nameof(mapeamento));

            if (!_mapeamento.TryAdd(chave, par.Value))
                throw new ArgumentException($"A movimentação '{par.Key}' repete outra chave.", nameof(mapeamento));
        }
    }

    public ResultadoParseB3 Ler(Stream csv)
    {
        ArgumentNullException.ThrowIfNull(csv);
        using var memoria = new MemoryStream();
        csv.CopyTo(memoria);
        return Interpretar(memoria.ToArray());
    }

    private ResultadoParseB3 Interpretar(byte[] bytes)
    {
        if (bytes.Length == 0)
            return Vazio();

        var texto = Decodificar(bytes).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (string.IsNullOrWhiteSpace(texto))
            return Vazio();

        var partes = texto.Split('\n');
        var linhas = new List<LinhaExtratoB3>();
        var erros = new List<ErroParseB3>();
        int[]? colunas = null;
        var largura = 0;

        for (var i = 0; i < partes.Length; i++)
        {
            var numero = i + 1;
            if (string.IsNullOrWhiteSpace(partes[i]))
                continue;

            var campos = Separar(partes[i], out var aspasAbertas);
            if (colunas is null)
            {
                if (!TentarMapearCabecalho(campos, out colunas, out largura))
                {
                    erros.Add(new ErroParseB3(numero, MensagemCabecalho));
                    return new ResultadoParseB3(linhas, erros);
                }

                continue;
            }

            if (TentarLinha(numero, campos, aspasAbertas, colunas, largura, out var linha, out var motivo))
                linhas.Add(linha);
            else
                erros.Add(new ErroParseB3(numero, motivo));
        }

        if (colunas is null)
            erros.Add(new ErroParseB3(1, "O arquivo está vazio."));

        return new ResultadoParseB3(linhas, erros);
    }

    private bool TentarLinha(
        int numero,
        IReadOnlyList<string> campos,
        bool aspasAbertas,
        int[] colunas,
        int largura,
        [NotNullWhen(true)] out LinhaExtratoB3? linha,
        out string motivo)
    {
        linha = null;
        if (aspasAbertas)
        {
            motivo = "Feche as aspas da linha.";
            return false;
        }

        if (campos.Count != largura)
        {
            motivo = "A linha precisa ter 8 colunas separadas por ponto e vírgula.";
            return false;
        }

        var motivos = new List<string>();
        var entrada = Campo(campos, colunas, 0);
        var dataTexto = Campo(campos, colunas, 1);
        var movimentacao = Campo(campos, colunas, 2);
        var produto = Campo(campos, colunas, 3);
        var instituicaoBruta = Campo(campos, colunas, 4);
        var quantidadeTexto = Campo(campos, colunas, 5);
        var precoTexto = Campo(campos, colunas, 6);
        var valorTexto = Campo(campos, colunas, 7);

        var direcaoOk = TentarDirecao(entrada, out var direcao);
        if (!direcaoOk)
            motivos.Add($"Entrada/Saída inválida: '{entrada}'. Use Credito ou Debito.");

        var dataOk = DateOnly.TryParseExact(dataTexto, FormatoData, CulturaData, DateTimeStyles.None, out var data);
        if (!dataOk)
            motivos.Add($"Data inválida: '{dataTexto}'. Use dd/MM/aaaa.");

        var tipoOk = false;
        var tipo = default(TipoOperacaoB3);
        if (direcaoOk)
        {
            var chave = TextoB3.NormalizarChave(movimentacao);
            if (!_mapeamento.TryGetValue(chave, out var regra) || !regra.TentarResolver(direcao, out tipo))
                motivos.Add($"Movimentação não mapeada: '{movimentacao}'.");
            else
                tipoOk = true;
        }

        if (!NumeroPtBr.TentarOpcional(quantidadeTexto, out var quantidade))
            motivos.Add($"Quantidade inválida: '{quantidadeTexto}'.");
        if (!NumeroPtBr.TentarOpcional(precoTexto, out var preco))
            motivos.Add($"Preço unitário inválido: '{precoTexto}'.");
        if (!NumeroPtBr.TentarOpcional(valorTexto, out var valor))
            motivos.Add($"Valor da operação inválido: '{valorTexto}'.");

        var ticker = ClassificadorAtivoB3.ExtrairTicker(produto);
        var descricao = produto.Trim();
        var classe = ClassificadorAtivoB3.Classificar(ticker, descricao);
        if (ticker.Length == 0)
            motivos.Add("Informe o ticker do produto.");
        else if (classe != ClasseAtivoB3.RendaFixa && !TickerB3.EhValido(ticker))
            motivos.Add($"Ticker inválido: '{ticker}'.");

        if (motivos.Count > 0 || !direcaoOk || !dataOk || !tipoOk)
        {
            motivo = motivos.Count > 0 ? string.Join(" ", motivos) : "A linha não pôde ser lida.";
            return false;
        }

        linha = new LinhaExtratoB3(
            numero,
            direcao,
            data,
            movimentacao.Trim(),
            tipo,
            ticker,
            descricao,
            classe,
            instituicaoBruta.Trim(),
            quantidade,
            preco,
            valor);
        motivo = "";
        return true;
    }

    private static string Campo(IReadOnlyList<string> campos, int[] colunas, int indiceLogico)
    {
        var indice = colunas[indiceLogico];
        return indice >= 0 && indice < campos.Count ? campos[indice].Trim() : "";
    }

    private static bool TentarDirecao(string texto, out DirecaoB3 direcao)
    {
        switch (TextoB3.NormalizarChave(texto))
        {
            case "credito":
                direcao = DirecaoB3.Credito;
                return true;
            case "debito":
                direcao = DirecaoB3.Debito;
                return true;
            default:
                direcao = default;
                return false;
        }
    }

    private static bool TentarMapearCabecalho(IReadOnlyList<string> campos, out int[] colunas, out int largura)
    {
        largura = campos.Count;
        colunas = new int[ColunasEsperadas.Length];
        Array.Fill(colunas, -1);
        for (var i = 0; i < campos.Count; i++)
        {
            var nome = TextoB3.NormalizarChave(campos[i]);
            var indice = Array.IndexOf(ColunasEsperadas, nome);
            if (indice < 0 || colunas[indice] >= 0)
                return false;

            colunas[indice] = i;
        }

        return colunas.All(indice => indice >= 0);
    }

    private static List<string> Separar(string linha, out bool aspasAbertas)
    {
        var campos = new List<string>();
        var atual = new StringBuilder();
        var aspas = false;
        for (var i = 0; i < linha.Length; i++)
        {
            var caractere = linha[i];
            if (caractere == '"')
            {
                if (aspas && i + 1 < linha.Length && linha[i + 1] == '"')
                {
                    atual.Append('"');
                    i++;
                }
                else
                {
                    aspas = !aspas;
                }
            }
            else if (caractere == ';' && !aspas)
            {
                campos.Add(atual.ToString());
                atual.Clear();
            }
            else
            {
                atual.Append(caractere);
            }
        }

        campos.Add(atual.ToString());
        aspasAbertas = aspas;
        return campos;
    }

    private static string Decodificar(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        try
        {
            return Utf8Estrito.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252).GetString(bytes);
        }
    }

    private static ResultadoParseB3 Vazio()
        => new([], [new ErroParseB3(1, "O arquivo está vazio.")]);
}
