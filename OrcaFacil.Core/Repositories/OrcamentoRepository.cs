using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Repositories;

public class OrcamentoRepository : GenericRepository<Orcamento>, IOrcamentoRepository
{
    public OrcamentoRepository(IDbContextFactory<AppDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<List<Orcamento>> GetByMesAnoAsync(int mes, int ano)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Orcamentos
            .Include(o => o.Categoria)
            .Where(o => o.Mes == mes && o.Ano == ano)
            .ToListAsync();
    }

    public async Task<Orcamento?> GetByCategoriaMesAnoAsync(int categoriaId, int mes, int ano)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Orcamentos
            .FirstOrDefaultAsync(o => o.CategoriaId == categoriaId && o.Mes == mes && o.Ano == ano);
    }
}
