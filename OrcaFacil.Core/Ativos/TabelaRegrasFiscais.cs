using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Ativos;

/// <summary>Regra já resolvida para uma data. A alíquota não entra na conta do lançamento.</summary>
public readonly record struct RegraResultado(bool CustosReduzemResultado, int CasasDecimais, decimal Aliquota);

/// <summary>
/// Parâmetros da apuração mensal na data. Nada disso entra no lançamento de lucro ou prejuízo.
/// </summary>
public readonly record struct ParametrosApuracaoIr(
    decimal AliquotaDayTrade,
    decimal LimiteVendasAcoes,
    int CodigoDarf,
    decimal ValorMinimoDarf,
    bool AcumularAbaixoDoMinimo,
    bool CompensaPrejuizoEntreAnos,
    bool CompensaPrejuizoNoMesIsento,
    bool EstimarIrrf,
    decimal AliquotaIrrfComum,
    decimal AliquotaIrrfDayTrade,
    decimal ValorMinimoRetencaoIrrf,
    bool CompensaIrrfMesesSeguintes,
    int CasasDecimais);

/// <summary>Escolhe, para a data, a vigência mais recente de cada código. Sem linha, recusa.</summary>
public sealed class TabelaRegrasFiscais
{
    private readonly IReadOnlyList<RegraFiscal> _regras;

    public TabelaRegrasFiscais(IEnumerable<RegraFiscal> regras)
    {
        _regras = regras.ToList();
    }

    public RegraResultado Para(DateTime data, TipoAtivo tipo)
    {
        var custos = Exigir(CodigosRegraFiscal.CustosReduzemResultado, data);
        var casas = Exigir(CodigosRegraFiscal.CasasDecimaisResultado, data);
        var aliquota = Exigir(CodigosRegraFiscal.Aliquota(tipo), data);

        if (custos.Valor is not 0m and not 1m)
            throw new InvalidOperationException(
                $"A regra fiscal {custos.Codigo} vigente em {data:dd/MM/yyyy} não é 0 nem 1.");

        if (casas.Valor != decimal.Truncate(casas.Valor) || casas.Valor is < 0m or > 8m)
            throw new InvalidOperationException(
                $"A regra fiscal {casas.Codigo} vigente em {data:dd/MM/yyyy} está fora do intervalo.");

        if (aliquota.Valor is < 0m or > 1m)
            throw new InvalidOperationException(
                $"A regra fiscal {aliquota.Codigo} vigente em {data:dd/MM/yyyy} está fora do intervalo.");

        return new RegraResultado(custos.Valor == 1m, (int)casas.Valor, aliquota.Valor);
    }

    public ParametrosApuracaoIr ParaApuracao(DateTime data)
    {
        var casas = Exigir(CodigosRegraFiscal.CasasDecimaisResultado, data);
        if (casas.Valor != decimal.Truncate(casas.Valor) || casas.Valor is < 0m or > 8m)
            throw new InvalidOperationException(
                $"A regra fiscal {casas.Codigo} vigente em {data:dd/MM/yyyy} está fora do intervalo.");

        var dayTrade = ExigirAliquota(CodigosRegraFiscal.AliquotaDayTrade, data);
        var irrfComum = ExigirAliquota(CodigosRegraFiscal.IrrfAliquotaComum, data);
        var irrfDayTrade = ExigirAliquota(CodigosRegraFiscal.IrrfAliquotaDayTrade, data);
        var limite = Exigir(CodigosRegraFiscal.IsencaoVendasAcoes, data);
        var codigo = Exigir(CodigosRegraFiscal.DarfCodigo, data);
        var minimo = Exigir(CodigosRegraFiscal.DarfValorMinimo, data);
        var retencao = Exigir(CodigosRegraFiscal.IrrfValorMinimoRetencao, data);

        if (limite.Valor < 0m)
            throw new InvalidOperationException(
                $"A regra fiscal {limite.Codigo} vigente em {data:dd/MM/yyyy} está fora do intervalo.");

        if (codigo.Valor != decimal.Truncate(codigo.Valor) || codigo.Valor is < 1m or > 9999m)
            throw new InvalidOperationException(
                $"A regra fiscal {codigo.Codigo} vigente em {data:dd/MM/yyyy} está fora do intervalo.");

        if (minimo.Valor < 0m)
            throw new InvalidOperationException(
                $"A regra fiscal {minimo.Codigo} vigente em {data:dd/MM/yyyy} está fora do intervalo.");

        if (retencao.Valor < 0m)
            throw new InvalidOperationException(
                $"A regra fiscal {retencao.Codigo} vigente em {data:dd/MM/yyyy} está fora do intervalo.");

        return new ParametrosApuracaoIr(
            dayTrade,
            limite.Valor,
            (int)codigo.Valor,
            minimo.Valor,
            Flag(CodigosRegraFiscal.DarfAcumularAbaixoDoMinimo, data),
            Flag(CodigosRegraFiscal.PrejuizoCompensaEntreAnos, data),
            Flag(CodigosRegraFiscal.PrejuizoCompensaNoMesIsento, data),
            Flag(CodigosRegraFiscal.IrrfEstimar, data),
            irrfComum,
            irrfDayTrade,
            retencao.Valor,
            Flag(CodigosRegraFiscal.IrrfCompensaMesesSeguintes, data),
            (int)casas.Valor);
    }

    private decimal ExigirAliquota(string codigo, DateTime data)
    {
        var regra = Exigir(codigo, data);
        if (regra.Valor is < 0m or > 1m)
            throw new InvalidOperationException(
                $"A regra fiscal {regra.Codigo} vigente em {data:dd/MM/yyyy} está fora do intervalo.");

        return regra.Valor;
    }

    public bool EstaLigada(string codigo, DateTime data) => Flag(codigo, data);

    private bool Flag(string codigo, DateTime data)
    {
        var regra = Exigir(codigo, data);
        if (regra.Valor is not 0m and not 1m)
            throw new InvalidOperationException(
                $"A regra fiscal {regra.Codigo} vigente em {data:dd/MM/yyyy} não é 0 nem 1.");

        return regra.Valor == 1m;
    }

    private RegraFiscal Exigir(string codigo, DateTime data)
    {
        var dia = data.Date;
        var vigente = _regras
            .Where(regra => regra.Codigo == codigo
                && regra.VigenciaInicio.Date <= dia
                && (regra.VigenciaFim is null || regra.VigenciaFim.Value.Date >= dia))
            .OrderByDescending(regra => regra.VigenciaInicio)
            .ThenByDescending(regra => regra.Id)
            .FirstOrDefault();

        if (vigente is null)
            throw new InvalidOperationException($"Não há regra fiscal vigente em {dia:dd/MM/yyyy} para {codigo}.");

        return vigente;
    }
}
