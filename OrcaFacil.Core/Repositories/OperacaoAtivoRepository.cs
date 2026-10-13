using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Repositories;

public class OperacaoAtivoRepository : GenericRepository<OperacaoAtivo>, IOperacaoAtivoRepository
{
    public OperacaoAtivoRepository(IDbContextFactory<AppDbContext> contextFactory) : base(contextFactory)
    {
    }

    public override async Task<OperacaoAtivo?> GetByIdAsync(int id)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.OperacoesAtivo
            .Include(o => o.Ativo)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public override async Task UpdateAsync(OperacaoAtivo entity)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        entity.Ativo = null;
        entity.MembroFamilia = null;
        context.OperacoesAtivo.Update(entity);
        await context.SaveChangesAsync();
    }

    public async Task<List<OperacaoAtivo>> ListarPorAtivoAsync(int ativoId, int membroFamiliaId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.OperacoesAtivo
            .Where(o => o.AtivoId == ativoId && o.MembroFamiliaId == membroFamiliaId)
            .OrderBy(o => o.Data)
            .ThenBy(o => o.Id)
            .ToListAsync();
    }

    public async Task<List<OperacaoAtivo>> ListarPorMembroAsync(int membroFamiliaId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.OperacoesAtivo
            .Include(o => o.Ativo)
            .Where(o => o.MembroFamiliaId == membroFamiliaId)
            .OrderBy(o => o.Data)
            .ThenBy(o => o.Id)
            .ToListAsync();
    }

    public async Task<bool> ExistePorAtivoAsync(int ativoId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.OperacoesAtivo.AnyAsync(o => o.AtivoId == ativoId);
    }
}
