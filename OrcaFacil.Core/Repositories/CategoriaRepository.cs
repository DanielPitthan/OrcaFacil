using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Repositories;

public class CategoriaRepository : GenericRepository<Categoria>, ICategoriaRepository
{
    public CategoriaRepository(IDbContextFactory<AppDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<List<Categoria>> GetAtivasAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Categorias
            .Where(c => c.Ativo)
            .OrderBy(c => c.Nome)
            .ToListAsync();
    }

    public async Task<bool> ExisteNomeAsync(string nome, int? ignorarId = null)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        var query = context.Categorias.Where(c => c.Nome.ToLower() == nome.ToLower());
        if (ignorarId.HasValue)
            query = query.Where(c => c.Id != ignorarId.Value);
        return await query.AnyAsync();
    }

    public async Task<bool> PossuiTransacoesAsync(int categoriaId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Transacoes.AnyAsync(t => t.CategoriaId == categoriaId);
    }
}
