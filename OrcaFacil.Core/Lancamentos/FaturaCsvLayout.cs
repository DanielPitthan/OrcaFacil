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

    /// <summary>Mesma regra de <c>CsvExportService.EscaparCsv</c>: aspas se houver separador, aspas ou quebra de linha.</summary>
    public static string Escapar(string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        if (valor.Contains(Separador) || valor.Contains('"') || valor.Contains('\n'))
            return $"\"{valor.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

        return valor;
    }

    private static string Montar(params string[] campos)
        => string.Join(Separador, campos.Select(Escapar));
}
