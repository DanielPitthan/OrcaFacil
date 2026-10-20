using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Ativos;

/// <summary>Regra já resolvida para uma data. A alíquota não entra na conta do lançamento.</summary>
public readonly record struct RegraResultado(bool CustosReduzemResultado, int CasasDecimais, decimal Aliquota);

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
