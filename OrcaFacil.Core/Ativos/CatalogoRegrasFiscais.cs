using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Ativos;

/// <summary>
/// Semente da tabela de vigência. A calculadora não lê estes números:
/// ela recebe o que estiver gravado na data da venda.
/// A alíquota não reduz o lançamento. O lançamento é o resultado econômico.
/// A apuração mensal lê a linha vigente na data. Os números não ficam no cálculo.
/// A Lei nº 11.033/2004, art. 2º e art. 3º, I, vale desde 01/01/2005:
/// 15% nas operações comuns, 20% no day trade e isenção de R$ 20.000,00.
/// https://www.planalto.gov.br/ccivil_03/_ato2004-2006/2004/lei/l11033.htm
/// A Receita confirma 15% e 20%, o DARF 6015 e o IRRF de 0,005% e de 1%.
/// https://www.gov.br/receitafederal/pt-br/assuntos/meu-imposto-de-renda/pagamento/renda-variavel/bolsa-de-valores-1
/// https://www.gov.br/receitafederal/pt-br/assuntos/meu-imposto-de-renda/pagamento/renda-variavel/bolsa-de-valores-1/calculo-e-pagamento-do-imposto
/// https://www.gov.br/receitafederal/pt-br/assuntos/meu-imposto-de-renda/pagamento/renda-variavel/bolsa-de-valores-1/retencoes
/// FII e Fiagro: ganho na venda a 20%, na mesma cesta, sem separar day trade.
/// https://www.gov.br/receitafederal/pt-br/assuntos/meu-imposto-de-renda/pagamento/renda-variavel/fundos-de-investimento-no-brasil
/// https://www.gov.br/receitafederal/pt-br/assuntos/meu-imposto-de-renda/pagamento/renda-variavel/manual/manual-de-utilizacao-do-revar-renda-variavel
/// DARF abaixo de R$ 10 acumula. O prejuízo atravessa o ano dentro da mesma cesta
/// (operações comuns, day trade e FII/Fiagro não se compensam). Prejuízo de ações
/// em mês com vendas até o limite também compensa: a isenção é só do ganho
/// (Perguntas e Respostas IRPF 2026, perguntas 707 e 729). A estimativa de IRRF
/// nasce desligada: o imposto retido entra só quando informado.
/// Renda fixa e JCP: o IR regressivo do art. 1º da Lei nº 11.033/2004 não é calculado
/// (RF.IrRegressivo.Automatico = 0) e o JCP não é reconstituído pela alíquota de 15%
/// (RF.Jcp.GrossUp = 0). A DIRPF usa o valor já gravado.
/// https://www.planalto.gov.br/ccivil_03/_ato2004-2006/2004/lei/l11033.htm
/// </summary>
public static class CatalogoRegrasFiscais
{
    public static readonly DateTime Inicio = new(1995, 1, 1);

    /// <summary>Produção de efeitos da Lei nº 11.033/2004 para a renda variável.</summary>
    public static readonly DateTime InicioLei11033 = new(2005, 1, 1);

    /// <summary>Art. 9º da Lei nº 9.249/1995: JCP a partir de 1º de janeiro de 1996.</summary>
    public static readonly DateTime InicioJcp = new(1996, 1, 1);

    public static IReadOnlyList<RegraFiscal> Linhas() =>
    [
        Linha(1, CodigosRegraFiscal.CustosReduzemResultado, 1m),
        Linha(2, CodigosRegraFiscal.CasasDecimaisResultado, 2m),
        Linha(3, CodigosRegraFiscal.AliquotaAcao, 0.15m),
        Linha(4, CodigosRegraFiscal.AliquotaFii, 0.20m),
        Linha(5, CodigosRegraFiscal.AliquotaEtf, 0.15m),
        Linha(6, CodigosRegraFiscal.AliquotaBdr, 0.15m),
        Linha(7, CodigosRegraFiscal.AliquotaDayTrade, 0.20m, InicioLei11033),
        Linha(8, CodigosRegraFiscal.IsencaoVendasAcoes, 20_000m, InicioLei11033),
        Linha(9, CodigosRegraFiscal.DarfCodigo, 6015m, InicioLei11033),
        Linha(10, CodigosRegraFiscal.DarfValorMinimo, 10m, InicioLei11033),
        Linha(11, CodigosRegraFiscal.DarfAcumularAbaixoDoMinimo, 1m, InicioLei11033),
        Linha(12, CodigosRegraFiscal.PrejuizoCompensaEntreAnos, 1m, InicioLei11033),
        Linha(13, CodigosRegraFiscal.PrejuizoCompensaNoMesIsento, 1m, InicioLei11033),
        Linha(14, CodigosRegraFiscal.IrrfEstimar, 0m, InicioLei11033),
        Linha(15, CodigosRegraFiscal.IrrfAliquotaComum, 0.00005m, InicioLei11033),
        Linha(16, CodigosRegraFiscal.IrrfAliquotaDayTrade, 0.01m, InicioLei11033),
        Linha(17, CodigosRegraFiscal.IrrfValorMinimoRetencao, 1m, InicioLei11033),
        Linha(18, CodigosRegraFiscal.IrrfCompensaMesesSeguintes, 0m, InicioLei11033),
        Linha(19, CodigosRegraFiscal.IrRegressivoAutomatico, 0m, InicioLei11033),
        Linha(20, CodigosRegraFiscal.JcpGrossUp, 0m, InicioJcp)
    ];

    private static RegraFiscal Linha(int id, string codigo, decimal valor, DateTime? inicio = null) => new()
    {
        Id = id,
        Codigo = codigo,
        Valor = valor,
        VigenciaInicio = inicio ?? Inicio,
        VigenciaFim = null,
        EhSistema = true
    };
}
