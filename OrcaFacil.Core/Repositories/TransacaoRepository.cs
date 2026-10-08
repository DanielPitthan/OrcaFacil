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
            .Include(t => t.MembroFamilia)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<List<Transacao>> GetByPeriodoAsync(DateTime inicio, DateTime fim, int? categoriaId = null, TipoTransacao? tipo = null, int? membroFamiliaId = null)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        var query = context.Transacoes
            .Include(t => t.Categoria)
            .Include(t => t.MembroFamilia)
            .Where(t => t.Data >= inicio && t.Data <= fim);

        if (categoriaId.HasValue)
            query = query.Where(t => t.CategoriaId == categoriaId.Value);
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

        // Evita que navegações carregadas (Categoria/Membro) sobrescrevam FKs no Update detached.
        entity.Categoria = null;
        entity.MembroFamilia = null;
        entity.TransacaoOrigem = null;

        context.Transacoes.Update(entity);
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

    public async Task<Dictionary<int, decimal>> SomaPorCategoriaAsync(DateTime inicio, DateTime fim, TipoTransacao tipo, int? membroFamiliaId = null)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        var query = context.Transacoes.Where(t => t.Tipo == tipo && t.Data >= inicio && t.Data <= fim);
        if (membroFamiliaId.HasValue)
            query = query.Where(t => t.MembroFamiliaId == membroFamiliaId.Value);

        return await query
            .GroupBy(t => t.CategoriaId)
            .Select(g => new { CategoriaId = g.Key, Total = g.Sum(t => t.Valor) })
            .ToDictionaryAsync(x => x.CategoriaId, x => x.Total);
    }
}
