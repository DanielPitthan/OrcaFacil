using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Repositories;

public class FechamentoRepository : GenericRepository<FechamentoPeriodo>, IFechamentoRepository
{
    public FechamentoRepository(IDbContextFactory<AppDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<bool> EstaFechadoAsync(int mes, int ano)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.FechamentosPeriodo.AnyAsync(f => f.Mes == mes && f.Ano == ano && f.Fechado);
    }

    public async Task<bool> AlternarAsync(int mes, int ano)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        var fechamento = await context.FechamentosPeriodo.FirstOrDefaultAsync(f => f.Mes == mes && f.Ano == ano);

        if (fechamento is null)
        {
            fechamento = new FechamentoPeriodo { Mes = mes, Ano = ano, Fechado = true };
            context.FechamentosPeriodo.Add(fechamento);
        }
        else
        {
            fechamento.Fechado = !fechamento.Fechado;
            fechamento.AtualizadoEm = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
        return fechamento.Fechado;
    }
}
