using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Services.Interfaces;

namespace OrcaFacil.Services;

public class RelatorioService : IRelatorioService
{
    private readonly ITransacaoRepository _transacaoRepository;

    public RelatorioService(ITransacaoRepository transacaoRepository)
    {
        _transacaoRepository = transacaoRepository;
    }

    public async Task<RelatorioResultDto> GerarAsync(FiltroRelatorioDto filtro)
    {
        var fimInclusive = filtro.Fim.Date.AddDays(1).AddTicks(-1);
        var transacoes = await _transacaoRepository.GetByPeriodoAsync(
            filtro.Inicio.Date, fimInclusive, filtro.CategoriaId, filtro.Tipo, filtro.MembroFamiliaId);

        return new RelatorioResultDto
        {
            Transacoes = transacoes,
            TotalReceitas = transacoes.Where(t => t.Tipo == TipoTransacao.Receita).Sum(t => t.Valor),
            TotalDespesas = transacoes.Where(t => t.Tipo == TipoTransacao.Despesa).Sum(t => t.Valor)
        };
    }
}
