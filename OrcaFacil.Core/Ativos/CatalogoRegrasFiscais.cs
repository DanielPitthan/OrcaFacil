using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Ativos;

/// <summary>
/// Semente da tabela de vigência. A calculadora não lê estes números:
/// ela recebe o que estiver gravado na data da venda.
/// A alíquota não reduz o lançamento. O lançamento é o resultado econômico;
/// a alíquota vigente só precisa existir, para o período não ficar sem regra.
/// </summary>
public static class CatalogoRegrasFiscais
{
    public static readonly DateTime Inicio = new(1995, 1, 1);

    public static IReadOnlyList<RegraFiscal> Linhas() =>
    [
        Linha(1, CodigosRegraFiscal.CustosReduzemResultado, 1m),
        Linha(2, CodigosRegraFiscal.CasasDecimaisResultado, 2m),
        Linha(3, CodigosRegraFiscal.AliquotaAcao, 0.15m),
        Linha(4, CodigosRegraFiscal.AliquotaFii, 0.20m),
        Linha(5, CodigosRegraFiscal.AliquotaEtf, 0.15m),
        Linha(6, CodigosRegraFiscal.AliquotaBdr, 0.15m)
    ];

    private static RegraFiscal Linha(int id, string codigo, decimal valor) => new()
    {
        Id = id,
        Codigo = codigo,
        Valor = valor,
        VigenciaInicio = Inicio,
        VigenciaFim = null,
        EhSistema = true
    };
}
