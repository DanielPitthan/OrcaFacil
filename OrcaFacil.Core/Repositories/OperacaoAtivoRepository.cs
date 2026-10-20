using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core;
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
        var operacoes = await context.OperacoesAtivo
            .Where(o => o.AtivoId == ativoId && o.MembroFamiliaId == membroFamiliaId)
            .ToListAsync();
        return OrdemPorDataEId.Ordenar(operacoes, o => o.Data, o => o.Id).ToList();
    }

    public async Task<List<OperacaoAtivo>> ListarPorMembroAsync(int membroFamiliaId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        var operacoes = await context.OperacoesAtivo
            .Include(o => o.Ativo)
            .Where(o => o.MembroFamiliaId == membroFamiliaId)
            .ToListAsync();
        return OrdemPorDataEId.Ordenar(operacoes, o => o.Data, o => o.Id).ToList();
    }

    public async Task<bool> ExistePorAtivoAsync(int ativoId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.OperacoesAtivo.AnyAsync(o => o.AtivoId == ativoId);
    }
}
