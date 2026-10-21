using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Ativos;

/// <summary>Nomes das regras. Os valores e as datas ficam na tabela, não aqui.</summary>
public static class CodigosRegraFiscal
{
    public const string CustosReduzemResultado = "RV.CustosReduzemResultado";
    public const string CasasDecimaisResultado = "RV.CasasDecimaisResultado";
    public const string AliquotaAcao = "RV.Aliquota.Acao";
    public const string AliquotaFii = "RV.Aliquota.Fii";
    public const string AliquotaEtf = "RV.Aliquota.Etf";
    public const string AliquotaBdr = "RV.Aliquota.Bdr";
    public const string AliquotaDayTrade = "RV.Aliquota.DayTrade";
    public const string IsencaoVendasAcoes = "RV.Isencao.VendasAcoes";
    public const string DarfCodigo = "RV.Darf.Codigo";
    public const string DarfValorMinimo = "RV.Darf.ValorMinimo";
    public const string DarfAcumularAbaixoDoMinimo = "RV.Darf.AcumularAbaixoDoMinimo";
    public const string PrejuizoCompensaEntreAnos = "RV.Prejuizo.CompensaEntreAnos";
    public const string PrejuizoCompensaNoMesIsento = "RV.Prejuizo.CompensaNoMesIsento";
    public const string IrrfEstimar = "RV.Irrf.Estimar";
    public const string IrrfAliquotaComum = "RV.Irrf.AliquotaComum";
    public const string IrrfAliquotaDayTrade = "RV.Irrf.AliquotaDayTrade";
    public const string IrrfValorMinimoRetencao = "RV.Irrf.ValorMinimoRetencao";
    public const string IrrfCompensaMesesSeguintes = "RV.Irrf.CompensaMesesSeguintes";

    public static string Aliquota(TipoAtivo tipo) => tipo switch
    {
        TipoAtivo.Acao => AliquotaAcao,
        TipoAtivo.Fii => AliquotaFii,
        TipoAtivo.Etf => AliquotaEtf,
        TipoAtivo.Bdr => AliquotaBdr,
        _ => throw new InvalidOperationException("Tipo de ativo sem regra fiscal.")
    };
}
