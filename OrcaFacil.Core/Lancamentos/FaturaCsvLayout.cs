using System.Text;

namespace OrcaFacil.Core.Lancamentos;

/// <summary>
/// Contrato da versão 1 do CSV de fatura do próprio OrcaFacil.
/// Não há layout de banco: a primeira linha identifica o arquivo e a segunda é o cabeçalho fixo.
/// </summary>
public static class FaturaCsvLayout
{
    public const int Versao = 1;

    public const string NomeArquivoModelo = "orcafacil-modelo-fatura-v1.csv";

    public const string LinhaVersao = "OrcaFacil;Fatura;1";

    public const string Cabecalho = "Data;Descricao;Valor;Categoria;Natureza;ParcelaAtual;TotalParcelas;Descritivo";

    public const char Separador = ';';

    /// <summary>Acima disto o arquivo é recusado, sem interpretar o conteúdo.</summary>
    public const int TamanhoMaximoBytes = 1_048_576;

    public const string MensagemArquivoGrande = "O arquivo passa de 1 MB. Nada foi importado.";

    public const string ColunaData = "Data";
    public const string ColunaDescricao = "Descricao";
    public const string ColunaValor = "Valor";
    public const string ColunaCategoria = "Categoria";
    public const string ColunaNatureza = "Natureza";
    public const string ColunaParcelaAtual = "ParcelaAtual";
    public const string ColunaTotalParcelas = "TotalParcelas";
    public const string ColunaDescritivo = "Descritivo";

    public static IReadOnlyList<string> Colunas { get; } = Cabecalho.Split(Separador);

    public static readonly string Exemplo1 = Montar(
        "16/03/2026", "Supermercado", "234,56", "Alimentação", "Avulsa", "", "", "Compras da semana");

    public static readonly string Exemplo2 = Montar(
        "10/03/2026", "Notebook", "350,00", "Compras", "Parcelada", "2", "12", "Parcela da loja");

    public static IReadOnlyList<string> LinhasExemplo { get; } = [Exemplo1, Exemplo2];

    public static string GerarModelo()
    {
        var sb = new StringBuilder();
        sb.Append(LinhaVersao).Append("\r\n");
        sb.Append(Cabecalho).Append("\r\n");
        sb.Append(Exemplo1).Append("\r\n");
        sb.Append(Exemplo2).Append("\r\n");
        return sb.ToString();
    }

    /// <summary>
    /// UTF-8 com BOM. No .NET atual, <see cref="Encoding.GetBytes(string)"/> não emite o preâmbulo;
    /// ele entra na frente, como o <c>StreamWriter</c> faz ao gravar o arquivo.
    /// </summary>
    public static byte[] Codificar(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var preambulo = encoding.GetPreamble();
        var corpo = encoding.GetBytes(texto);
        var arquivo = new byte[preambulo.Length + corpo.Length];
        preambulo.CopyTo(arquivo, 0);
        corpo.CopyTo(arquivo, preambulo.Length);
        return arquivo;
    }

    public static bool EhLinhaExemplo(string linha)
        => string.Equals(linha, Exemplo1, StringComparison.Ordinal)
            || string.Equals(linha, Exemplo2, StringComparison.Ordinal);

    /// <summary>
    /// Mesma regra de <c>CsvExportService.EscaparCsv</c>: aspas se houver separador, aspas ou quebra de linha.
    /// Texto que abre com =, +, @ ou com hífen que não é valor recebe um apóstrofo,
    /// para o Excel não tratar a célula como fórmula. <c>-10,00</c> continua negativo.
    /// </summary>
    public static string Escapar(string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        var texto = ProtegerFormula(valor);
        if (texto.Contains(Separador) || texto.Contains('"') || texto.Contains('\n') || texto.Contains('\r'))
            return $"\"{texto.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

        return texto;
    }

    /// <summary>Apóstrofo na frente de gatilho de fórmula. Valor monetário negativo não muda.</summary>
    public static string ProtegerFormula(string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        if (valor.Length == 0)
            return valor;

        if (valor[0] is '=' or '+' or '@')
            return "'" + valor;

        if (valor[0] == '-' && !PareceValorMonetario(valor))
            return "'" + valor;

        return valor;
    }

    private static bool PareceValorMonetario(string valor)
    {
        if (valor.Length < 2 || valor[0] != '-')
            return false;

        var virgula = valor.IndexOf(',');
        var fimInteiro = virgula >= 0 ? virgula : valor.Length;
        if (fimInteiro <= 1)
            return false;

        for (var i = 1; i < fimInteiro; i++)
        {
            if (!char.IsAsciiDigit(valor[i]))
                return false;
        }

        if (virgula < 0)
            return true;

        var fracao = valor.Length - virgula - 1;
        if (fracao is < 1 or > 2)
            return false;

        for (var i = virgula + 1; i < valor.Length; i++)
        {
            if (!char.IsAsciiDigit(valor[i]))
                return false;
        }

        return true;
    }

    private static string Montar(params string[] campos)
        => string.Join(Separador, campos.Select(Escapar));
}
