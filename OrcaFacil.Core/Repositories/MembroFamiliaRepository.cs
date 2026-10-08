using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Repositories;

public class MembroFamiliaRepository : GenericRepository<MembroFamilia>, IMembroFamiliaRepository
{
    public MembroFamiliaRepository(IDbContextFactory<AppDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<List<MembroFamilia>> GetAtivosAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.MembrosFamilia
            .Where(m => m.Ativo)
            .OrderBy(m => m.Nome)
            .ToListAsync();
    }
}
