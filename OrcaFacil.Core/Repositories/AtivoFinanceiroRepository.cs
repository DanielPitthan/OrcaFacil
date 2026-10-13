using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Repositories;

public class AtivoFinanceiroRepository : GenericRepository<AtivoFinanceiro>, IAtivoFinanceiroRepository
{
    public AtivoFinanceiroRepository(IDbContextFactory<AppDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<List<AtivoFinanceiro>> ListarAsync(bool somenteAtivos)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        var query = context.AtivosFinanceiros.AsQueryable();
        if (somenteAtivos)
            query = query.Where(a => a.Ativo);

        return await query.OrderBy(a => a.Ticker).ToListAsync();
    }

    public async Task<bool> ExisteTickerAsync(string ticker, int? ignorarId = null)
    {
        var normalizado = TickerB3.Normalizar(ticker);
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.AtivosFinanceiros.AnyAsync(a =>
            a.Ticker == normalizado && (ignorarId == null || a.Id != ignorarId));
    }
}
