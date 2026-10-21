using System.Globalization;

namespace OrcaFacil.Core.B3;

/// <summary>
/// O que a gravação do extrato realmente persistiu: a quantidade de linhas
/// salvas e as datas das Transacao criadas nessa gravação.
/// </summary>
public sealed class ResultadoGravacaoB3
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private ResultadoGravacaoB3(int linhas, int lancamentos, IReadOnlyList<DateTime> meses)
    {
        Linhas = linhas;
        Lancamentos = lancamentos;
        Meses = meses;
        MesMaisRecente = meses.Count == 0 ? null : meses[^1];
        Texto = MontarTexto(lancamentos, meses);
        Rota = MesMaisRecente is DateTime mes
            ? $"/lancamentos?mes={mes:yyyy-MM}"
            : "/ativos";
    }

    public int Linhas { get; }

    public int Lancamentos { get; }

    /// <summary>Primeiro dia de cada mês com lançamento, do mais antigo ao mais recente.</summary>
    public IReadOnlyList<DateTime> Meses { get; }

    public DateTime? MesMaisRecente { get; }

    public string Texto { get; }

    public string Rota { get; }

    public static ResultadoGravacaoB3 Criar(int linhas, IEnumerable<DateTime> datas)
    {
        ArgumentNullException.ThrowIfNull(datas);
        var lista = datas.ToList();
        var meses = lista
            .Select(data => new DateTime(data.Year, data.Month, 1))
            .Distinct()
            .OrderBy(mes => mes)
            .ToList();
        return new ResultadoGravacaoB3(linhas, lista.Count, meses);
    }

    private static string MontarTexto(int quantidade, IReadOnlyList<DateTime> meses)
    {
        if (quantidade == 0)
            return "Nenhum lançamento foi gerado.";

        var substantivo = quantidade == 1 ? "lançamento" : "lançamentos";
        return $"{quantidade} {substantivo} em {Juntar(meses)}.";
    }

    private static string Juntar(IReadOnlyList<DateTime> meses)
    {
        var textos = meses.Select(FormatarMes).ToList();
        if (textos.Count <= 1)
            return textos.Count == 0 ? "" : textos[0];

        return string.Join(", ", textos.Take(textos.Count - 1)) + " e " + textos[^1];
    }

    private static string FormatarMes(DateTime mes)
    {
        var nome = PtBr.DateTimeFormat.AbbreviatedMonthNames[mes.Month - 1].TrimEnd('.');
        return $"{nome}/{mes:yyyy}";
    }
}
