using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Dirpf;

/// <summary>
/// A reclassificação muda o tipo usado na apuração dos meses que já fecharam.
/// O texto avisa que o DARF daquele mês pode precisar de complemento.
/// </summary>
public static class AvisoDarfComplementar
{
    public const string Prefixo =
        "A reclassificação de ETF alterou o resultado de meses já fechados. Pode haver DARF complementar.";

    public static IReadOnlyList<(int Ano, int Mes)> MesesAlterados(
        IReadOnlyList<OperacaoApuracao> antes,
        IReadOnlyList<OperacaoApuracao> depois,
        IEnumerable<RegraFiscal> regras,
        IEnumerable<FechamentoPeriodo> fechamentos)
    {
        ArgumentNullException.ThrowIfNull(antes);
        ArgumentNullException.ThrowIfNull(depois);
        ArgumentNullException.ThrowIfNull(regras);
        ArgumentNullException.ThrowIfNull(fechamentos);

        var listaRegras = regras.ToList();
        var fechados = fechamentos
            .Where(fechamento => fechamento.Fechado)
            .Select(fechamento => (fechamento.Ano, fechamento.Mes))
            .Distinct()
            .OrderBy(periodo => periodo.Ano)
            .ThenBy(periodo => periodo.Mes)
            .ToList();
        if (fechados.Count == 0)
            return [];

        var mudou = new List<(int Ano, int Mes)>();
        foreach (var ano in fechados.Select(periodo => periodo.Ano).Distinct())
        {
            var apuracaoAntes = ApuradorIrMensal.Apurar(antes, listaRegras, ano);
            var apuracaoDepois = ApuradorIrMensal.Apurar(depois, listaRegras, ano);
            foreach (var periodo in fechados.Where(periodo => periodo.Ano == ano))
            {
                if (!Igual(apuracaoAntes, apuracaoDepois, periodo.Ano, periodo.Mes))
                    mudou.Add(periodo);
            }
        }

        return mudou;
    }

    public static string Mensagem(IReadOnlyList<(int Ano, int Mes)> meses)
    {
        ArgumentNullException.ThrowIfNull(meses);
        if (meses.Count == 0)
            return Prefixo;

        var lista = string.Join(", ", meses.Select(periodo => $"{periodo.Mes:00}/{periodo.Ano}"));
        var completa = $"{Prefixo} Confira {lista}.";
        return completa.Length <= 300 ? completa : Prefixo;
    }

    private static bool Igual(ApuracaoIr antes, ApuracaoIr depois, int ano, int mes)
        => Assinatura(antes, ano, mes) == Assinatura(depois, ano, mes);

    /// <summary>
    /// O que muda o imposto do mês e o DARF. A coluna de vendas de ações, sozinha, não gera complemento.
    /// </summary>
    private static string Assinatura(ApuracaoIr apuracao, int ano, int mes)
    {
        var meses = apuracao.Meses
            .Where(linha => linha.Ano == ano && linha.Mes == mes)
            .OrderBy(linha => linha.PerfilId)
            .ThenBy(linha => linha.Cesta)
            .Select(linha => string.Join('|',
                linha.PerfilId,
                linha.Cesta,
                linha.Resultado,
                linha.PrejuizoCompensado,
                linha.PrejuizoACompensar,
                linha.BaseCalculo,
                linha.Imposto,
                linha.ImpostoLiquido,
                linha.GanhoIsentoAcoes));
        var darfs = apuracao.Darfs
            .Where(linha => linha.Ano == ano && linha.Mes == mes)
            .OrderBy(linha => linha.PerfilId)
            .Select(linha => string.Join('|',
                linha.PerfilId,
                linha.ImpostoDoMes,
                linha.AcumuladoAnterior,
                linha.SaldoAcumulado,
                linha.Valor));
        return string.Join(';', meses) + "#" + string.Join(';', darfs);
    }
}
