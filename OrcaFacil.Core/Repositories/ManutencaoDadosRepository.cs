using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Repositories;

public class ManutencaoDadosRepository : IManutencaoDadosRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public ManutencaoDadosRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Dictionary<string, int>> ContarRegistrosAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return new Dictionary<string, int>
        {
            ["Lançamentos"] = await context.Transacoes.CountAsync(),
            ["Orçamentos mensais"] = await context.Orcamentos.CountAsync(),
            ["Metas de economia"] = await context.Metas.CountAsync(),
            ["Categorias"] = await context.Categorias.CountAsync(),
            ["Fechamentos de mês"] = await context.FechamentosPeriodo.CountAsync(),
            ["Logs"] = await context.LogsEvento.CountAsync()
        };
    }

    public async Task LimparAsync(TipoLimpezaDados tipo)
    {
        if (tipo == TipoLimpezaDados.Nenhum)
            return;

        var tudo = tipo.HasFlag(TipoLimpezaDados.Tudo);

        await using var context = await _contextFactory.CreateDbContextAsync();
        await using (var transacao = await context.Database.BeginTransactionAsync())
        {
            if (tudo || tipo.HasFlag(TipoLimpezaDados.Lancamentos))
            {
                await context.Transacoes.ExecuteDeleteAsync();
                await context.FechamentosPeriodo.ExecuteDeleteAsync();
            }

            if (tudo || tipo.HasFlag(TipoLimpezaDados.Orcamentos))
                await context.Orcamentos.ExecuteDeleteAsync();

            if (tudo || tipo.HasFlag(TipoLimpezaDados.Metas))
                await context.Metas.ExecuteDeleteAsync();

            if (tudo)
            {
                await context.LogsEvento.ExecuteDeleteAsync();
                await context.Categorias.ExecuteDeleteAsync();
                await context.MembrosFamilia.ExecuteDeleteAsync();
            }

            await transacao.CommitAsync();
        }

        // VACUUM não pode rodar dentro de uma transação.
        await context.Database.ExecuteSqlRawAsync("VACUUM");
    }
}
