using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Services.Interfaces;

namespace OrcaFacil.Services;

public class OrcamentoService : IOrcamentoService
{
    private readonly IOrcamentoRepository _orcamentoRepository;
    private readonly ICategoriaRepository _categoriaRepository;
    private readonly ITransacaoRepository _transacaoRepository;

    public OrcamentoService(
        IOrcamentoRepository orcamentoRepository,
        ICategoriaRepository categoriaRepository,
        ITransacaoRepository transacaoRepository)
    {
        _orcamentoRepository = orcamentoRepository;
        _categoriaRepository = categoriaRepository;
        _transacaoRepository = transacaoRepository;
    }

    public async Task<List<OrcamentoItemDto>> ListarPorMesAsync(int mes, int ano)
    {
        var categorias = await _categoriaRepository.GetAtivasAsync();
        var orcamentos = await _orcamentoRepository.GetByMesAnoAsync(mes, ano);

        var inicio = new DateTime(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddTicks(-1);
        var gastos = await _transacaoRepository.SomaPorCategoriaAsync(inicio, fim, TipoTransacao.Despesa);

        var mesAnterior = inicio.AddMonths(-1);
        var fimAnterior = inicio.AddTicks(-1);
        var gastosAnteriores = await _transacaoRepository.SomaPorCategoriaAsync(mesAnterior, fimAnterior, TipoTransacao.Despesa);

        return categorias.Select(categoria =>
        {
            var orcamento = orcamentos.FirstOrDefault(o => o.CategoriaId == categoria.Id);
            gastos.TryGetValue(categoria.Id, out var gasto);
            gastosAnteriores.TryGetValue(categoria.Id, out var gastoAnterior);

            return new OrcamentoItemDto
            {
                OrcamentoId = orcamento?.Id,
                CategoriaId = categoria.Id,
                CategoriaNome = categoria.Nome,
                CategoriaIcone = categoria.Icone,
                CategoriaCorHex = categoria.CorHex,
                ValorLimite = orcamento?.ValorLimite ?? 0m,
                ValorGasto = gasto,
                ValorGastoMesAnterior = gastoAnterior,
                MetaReducaoPercentual = orcamento?.MetaReducaoPercentual
            };
        })
        .Where(item => item.ValorLimite > 0 || item.ValorGasto > 0 || item.ValorGastoMesAnterior > 0)
        .OrderByDescending(item => item.PercentualUsado)
        .ToList();
    }

    public async Task DefinirLimiteAsync(OrcamentoFormModel dto)
    {
        var existente = await _orcamentoRepository.GetByCategoriaMesAnoAsync(dto.CategoriaId, dto.Mes, dto.Ano);

        if (existente is not null)
        {
            existente.ValorLimite = dto.ValorLimite;
            existente.MetaReducaoPercentual = dto.MetaReducaoPercentual;
            await _orcamentoRepository.UpdateAsync(existente);
            return;
        }

        var orcamento = new Orcamento
        {
            CategoriaId = dto.CategoriaId,
            ValorLimite = dto.ValorLimite,
            MetaReducaoPercentual = dto.MetaReducaoPercentual,
            Mes = dto.Mes,
            Ano = dto.Ano
        };
        await _orcamentoRepository.AddAsync(orcamento);
    }

    public async Task<int> TrazerDoMesAnteriorAsync(int mes, int ano)
    {
        var mesAnterior = new DateTime(ano, mes, 1).AddMonths(-1);
        var anteriores = await _orcamentoRepository.GetByMesAnoAsync(mesAnterior.Month, mesAnterior.Year);
        if (anteriores.Count == 0)
            return 0;

        var atuais = await _orcamentoRepository.GetByMesAnoAsync(mes, ano);
        var jaDefinidos = atuais.Select(o => o.CategoriaId).ToHashSet();

        var quantidade = 0;
        foreach (var origem in anteriores)
        {
            if (jaDefinidos.Contains(origem.CategoriaId))
                continue;

            await _orcamentoRepository.AddAsync(new Orcamento
            {
                CategoriaId = origem.CategoriaId,
                ValorLimite = origem.ValorLimite,
                MetaReducaoPercentual = origem.MetaReducaoPercentual,
                Mes = mes,
                Ano = ano
            });
            quantidade++;
        }

        return quantidade;
    }

    public async Task<bool> ExistemOrcamentosParaTrazerAsync(int mes, int ano)
    {
        var mesAnterior = new DateTime(ano, mes, 1).AddMonths(-1);
        var anteriores = await _orcamentoRepository.GetByMesAnoAsync(mesAnterior.Month, mesAnterior.Year);
        if (anteriores.Count == 0)
            return false;

        var atuais = await _orcamentoRepository.GetByMesAnoAsync(mes, ano);
        var jaDefinidos = atuais.Select(o => o.CategoriaId).ToHashSet();
        return anteriores.Any(o => !jaDefinidos.Contains(o.CategoriaId));
    }

    public async Task<List<AlertaOrcamentoDto>> ObterAlertasAsync(int mes, int ano, decimal percentualMinimo = 90m)
    {
        var itens = await ListarPorMesAsync(mes, ano);

        return itens
            .Where(i => i.ValorLimite > 0 && i.PercentualUsado >= percentualMinimo)
            .Select(i => new AlertaOrcamentoDto
            {
                CategoriaId = i.CategoriaId,
                CategoriaNome = i.CategoriaNome,
                CategoriaIcone = i.CategoriaIcone,
                ValorLimite = i.ValorLimite,
                ValorGasto = i.ValorGasto
            })
            .ToList();
    }
}
