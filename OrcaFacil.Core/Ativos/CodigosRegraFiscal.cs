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

    public static string Aliquota(TipoAtivo tipo) => tipo switch
    {
        TipoAtivo.Acao => AliquotaAcao,
        TipoAtivo.Fii => AliquotaFii,
        TipoAtivo.Etf => AliquotaEtf,
        TipoAtivo.Bdr => AliquotaBdr,
        _ => throw new InvalidOperationException("Tipo de ativo sem regra fiscal.")
    };
}
