using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using Serilog;

namespace OrcaFacil.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IDbContextFactory<AppDbContext> factory)
    {
        await using var context = await factory.CreateDbContextAsync();
        var resultado = await PreparoBanco.MigrarEVerificarAsync(
            context,
            new OpcoesPreparoBanco { VersaoAplicativo = VersaoAplicativo() });
        Registrar(resultado);
        if (resultado.ExigeRecuperacao)
            throw new IntegridadeBancoException(resultado);

        var haMembro = await context.MembrosFamilia.AnyAsync();
        if (PoliticaSeed.DevePopular(resultado, haMembro))
            await SeedData.PopularAsync(context);
    }

    private static string VersaoAplicativo()
    {
        try
        {
            return AppInfo.VersionString;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return "desconhecida";
        }
    }

    private static void Registrar(ResultadoPreparoBanco resultado)
    {
        var relatorio = resultado.Relatorio;
        try
        {
            if (resultado.CaminhoBackup is not null)
            {
                Log.Information(
                    "Backup consistente antes da migração, sem apagar os anteriores: {Backup}.",
                    resultado.CaminhoBackup);
            }

            if (!resultado.ExigeRecuperacao)
            {
                Log.Information(
                    "Integridade do orcafacil.db ok. Tabelas: {Tabelas}. Migrations no histórico: {Historico}.",
                    Juntar(relatorio.TabelasExistentes),
                    Juntar(relatorio.MigrationsNoHistorico));
                return;
            }

            Log.Error(
                resultado.FalhaMigrate,
                "Integridade do orcafacil.db falhou. O check não alterou o arquivo e nada foi apagado. Existe: {Tabelas}. Faltam tabelas: {TabelasFaltando}. Faltam colunas: {Colunas}. Faltam índices: {Indices}. Histórico: {Historico}. Histórico sem: {Ausentes}. Histórico desconhecido: {Desconhecidas}. Migration pela metade: {PelaMetade}. Histórico com objeto faltando: {HistoricoIncompleto}. Próxima migration não registrada: {Pendente}.",
                Juntar(relatorio.TabelasExistentes),
                Juntar(relatorio.TabelasFaltando),
                Juntar(relatorio.ColunasFaltando),
                Juntar(relatorio.IndicesFaltando),
                Juntar(relatorio.MigrationsNoHistorico),
                Juntar(relatorio.MigrationsAusentesNoHistorico),
                Juntar(relatorio.MigrationsDesconhecidasNoHistorico),
                Juntar(relatorio.MigrationsPelaMetade),
                Juntar(relatorio.MigrationsNoHistoricoComObjetoFaltando),
                relatorio.PrimeiraMigrationNaoRegistrada ?? "(nenhuma)");
        }
        catch (Exception logEx)
        {
            System.Diagnostics.Debug.WriteLine(logEx);
        }
    }

    private static string Juntar(IReadOnlyList<string> itens) =>
        itens.Count == 0 ? "(nenhum)" : string.Join(", ", itens);
}
