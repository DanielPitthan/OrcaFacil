using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Dirpf;

/// <summary>
/// Separa o pregão da liquidação. A #30 gravava só a liquidação em
/// <c>OperacaoAtivo.Data</c>. A #104 altera essa regra: o pregão vai para
/// <c>OperacaoAtivo.DataPregao</c> e a apuração do IR (mês, teto de R$ 20 mil
/// e day trade) usa o pregão. O vencimento do DARF continua pela liquidação.
/// Perguntas e Respostas IRPF 2026, pergunta 745:
/// https://www.gov.br/receitafederal/pt-br/centrais-de-conteudo/publicacoes/perguntas-e-respostas/dirpf/p-r-irpf-2026-v1-00-2026-04-23.pdf
/// </summary>
public static class DataPregaoOperacao
{
    /// <summary>Liquidação à vista da B3: dois pregões depois da operação.</summary>
    public const int DiasUteisLiquidacaoVista = 2;

    /// <summary>
    /// No extrato B3, <paramref name="dataInformada"/> é a liquidação e o retorno é o pregão.
    /// No lançamento manual, a data digitada já é o pregão e o retorno é ela mesma.
    /// </summary>
    public static DateTime Resolver(DateTime dataInformada, OrigemOperacaoAtivo origem)
    {
        var data = dataInformada.Date;
        if (origem == OrigemOperacaoAtivo.ExtratoB3)
            return CalendarioBancarioNacional.RecuarDiasUteis(data, DiasUteisLiquidacaoVista);

        return data;
    }
}
