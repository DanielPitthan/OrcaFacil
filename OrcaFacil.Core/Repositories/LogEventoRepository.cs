using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Repositories;

public class LogEventoRepository : GenericRepository<LogEvento>, ILogEventoRepository
{
    public LogEventoRepository(IDbContextFactory<AppDbContext> contextFactory) : base(contextFactory)
    {
    }

    public Task RegistrarAsync(TipoLogEvento tipo, string mensagem)
        => AddAsync(new LogEvento { Tipo = tipo, Mensagem = mensagem, CriadoEm = DateTime.Now });

    public async Task<List<LogEvento>> ListarAsync(TipoLogEvento? tipo = null)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        var query = context.LogsEvento.AsQueryable();
        if (tipo.HasValue)
            query = query.Where(l => l.Tipo == tipo.Value);

        return await query
            .OrderByDescending(l => l.CriadoEm)
            .ThenByDescending(l => l.Id)
            .ToListAsync();
    }

    public async Task<int> LimparAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.LogsEvento.ExecuteDeleteAsync();
    }
}
