using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Repositories;

public class TransacaoRepository : GenericRepository<Transacao>, ITransacaoRepository
{
    public TransacaoRepository(IDbContextFactory<AppDbContext> contextFactory) : base(contextFactory)
    {
    }

    public override async Task<Transacao?> GetByIdAsync(int id)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Transacoes
            .Include(t => t.Categoria)
            .Include(t => t.Cartao)
            .Include(t => t.MembroFamilia)
            .Include(t => t.Itens)
            .ThenInclude(i => i.Categoria)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<List<Transacao>> GetByPeriodoAsync(DateTime inicio, DateTime fim, int? categoriaId = null, TipoTransacao? tipo = null, int? membroFamiliaId = null)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        var query = context.Transacoes
            .Include(t => t.Categoria)
            .Include(t => t.Cartao)
            .Include(t => t.MembroFamilia)
            .Include(t => t.Itens)
            .ThenInclude(i => i.Categoria)
            .Where(t => t.Data >= inicio && t.Data <= fim);

        if (categoriaId.HasValue)
            query = query.Where(t => t.CategoriaId == categoriaId.Value || t.Itens.Any(i => i.CategoriaId == categoriaId.Value));
        if (tipo.HasValue)
            query = query.Where(t => t.Tipo == tipo.Value);
        if (membroFamiliaId.HasValue)
            query = query.Where(t => t.MembroFamiliaId == membroFamiliaId.Value);

        // Receitas primeiro, depois despesas; dentro de cada tipo, mais recentes primeiro.
        return await query
            .OrderBy(t => t.Tipo)
            .ThenByDescending(t => t.Data)
            .ToListAsync();
    }

    public override async Task UpdateAsync(Transacao entity)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        var tracked = await context.Transacoes.FirstOrDefaultAsync(t => t.Id == entity.Id)
            ?? throw new InvalidOperationException("Lançamento não encontrado.");

        // Só os campos escalares: navegações carregadas não podem regravar FKs nem apagar os itens.
        context.Entry(tracked).CurrentValues.SetValues(entity);
        await context.SaveChangesAsync();
    }

    public async Task<List<Transacao>> GetDespesasPendentesParaAvisoAsync(DateTime ateData)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        var fimDoDia = ateData.Date.AddDays(1).AddTicks(-1);

        return await context.Transacoes
            .Include(t => t.Categoria)
            .Where(t => t.Tipo == TipoTransacao.Despesa && !t.Pago && t.Data <= fimDoDia)
            .OrderBy(t => t.Data)
            .ToListAsync();
    }

    public async Task<List<Transacao>> GetRecorrentesAtivasAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Transacoes
            .Include(t => t.Categoria)
            .Where(t => t.Recorrencia != TipoRecorrencia.Unica)
            .ToListAsync();
    }

    public async Task<List<Transacao>> GetDespesasRecorrentesPorPeriodoAsync(DateTime inicio, DateTime fim)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Transacoes
            .Include(t => t.Categoria)
            .Where(t => t.Tipo == TipoTransacao.Despesa && t.Recorrencia != TipoRecorrencia.Unica && t.Data >= inicio && t.Data <= fim)
            .ToListAsync();
    }

    public async Task<List<Transacao>> GetDespesasParaCopiaAsync(DateTime inicio, DateTime fim)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Transacoes
            .Include(t => t.Itens)
            .Where(t => t.Tipo == TipoTransacao.Despesa && t.Data >= inicio && t.Data <= fim)
            .Where(t => t.Recorrencia != TipoRecorrencia.Unica
                || t.Itens.Any(i => i.Natureza == NaturezaItemLancamento.Assinatura
                    || (i.Natureza == NaturezaItemLancamento.Parcelada
                        && i.ParcelaAtual != null
                        && i.TotalParcelas != null
                        && i.ParcelaAtual < i.TotalParcelas)))
            .OrderBy(t => t.Data)
            .ThenBy(t => t.Id)
            .ToListAsync();
    }

    public async Task SubstituirItensAsync(int transacaoId, IReadOnlyList<ItemLancamento> itens)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        var existentes = await context.ItensLancamento.Where(i => i.TransacaoId == transacaoId).ToListAsync();
        context.ItensLancamento.RemoveRange(existentes);

        foreach (var item in itens)
        {
            item.Id = 0;
            item.TransacaoId = transacaoId;
            item.Transacao = null;
            item.Categoria = null;
            context.ItensLancamento.Add(item);
        }

        await context.SaveChangesAsync();
    }

    public async Task<HashSet<int>> GetOrigemIdsComCopiaNoPeriodoAsync(DateTime inicio, DateTime fim)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        var origemIds = await context.Transacoes
            .Where(t => t.Data >= inicio && t.Data <= fim && t.TransacaoOrigemId != null)
            .Select(t => t.TransacaoOrigemId!.Value)
            .Distinct()
            .ToListAsync();

        return origemIds.ToHashSet();
    }

    public async Task<decimal> SomaPorTipoAsync(TipoTransacao tipo, DateTime inicio, DateTime fim, int? membroFamiliaId = null, bool? pago = null)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        var query = context.Transacoes.Where(t => t.Tipo == tipo && t.Data >= inicio && t.Data <= fim);
        if (membroFamiliaId.HasValue)
            query = query.Where(t => t.MembroFamiliaId == membroFamiliaId.Value);
        if (pago.HasValue)
            query = query.Where(t => t.Pago == pago.Value);

        return await query.SumAsync(t => (decimal?)t.Valor) ?? 0m;
    }

    public async Task<decimal> SomaTotalPorTipoAsync(TipoTransacao tipo)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Transacoes
            .Where(t => t.Tipo == tipo)
            .SumAsync(t => (decimal?)t.Valor) ?? 0m;
    }

    public async Task<Dictionary<int, decimal>> SomaPorCategoriaAsync(DateTime inicio, DateTime fim, TipoTransacao tipo, int? membroFamiliaId = null)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        var query = context.Transacoes
            .Where(t => t.Tipo == tipo && t.Data >= inicio && t.Data <= fim);
        if (membroFamiliaId.HasValue)
            query = query.Where(t => t.MembroFamiliaId == membroFamiliaId.Value);

        var transacoes = await query
            .Select(t => new
            {
                t.CategoriaId,
                t.Valor,
                Itens = t.Itens.Select(i => new { i.CategoriaId, i.Valor }).ToList()
            })
            .ToListAsync();

        var somas = new Dictionary<int, decimal>();
        foreach (var transacao in transacoes)
        {
            if (transacao.Itens.Count == 0)
            {
                Somar(somas, transacao.CategoriaId, transacao.Valor);
                continue;
            }

            foreach (var item in transacao.Itens)
                Somar(somas, item.CategoriaId, item.Valor);

            var restante = transacao.Valor - transacao.Itens.Sum(i => i.Valor);
            if (restante > 0)
                Somar(somas, transacao.CategoriaId, restante);
        }

        return somas;
    }

    private static void Somar(Dictionary<int, decimal> somas, int categoriaId, decimal valor)
    {
        somas.TryGetValue(categoriaId, out var atual);
        somas[categoriaId] = atual + valor;
    }
}
