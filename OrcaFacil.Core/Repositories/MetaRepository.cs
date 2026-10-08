using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Repositories;

public class MetaRepository : GenericRepository<Meta>, IMetaRepository
{
    public MetaRepository(IDbContextFactory<AppDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<List<Meta>> GetByMembroAsync(int membroFamiliaId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Metas
            .Where(m => m.MembroFamiliaId == membroFamiliaId)
            .OrderBy(m => m.Concluida)
            .ThenBy(m => m.Prazo)
            .ToListAsync();
    }

    public async Task<List<Meta>> GetAtivasAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Metas
            .Where(m => !m.Concluida)
            .OrderBy(m => m.Prazo)
            .ToListAsync();
    }
}
