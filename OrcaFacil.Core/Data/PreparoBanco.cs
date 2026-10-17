using Microsoft.EntityFrameworkCore;

namespace OrcaFacil.Core.Data;

/// <summary>
/// Aplica as migrations e, em seguida, só lê o arquivo para ver se a estrutura
/// bate com o model. O check em si não grava nem apaga.
/// </summary>
public sealed class OpcoesPreparoBanco
{
    public string VersaoAplicativo { get; init; } = "desconhecida";
}

public static class PreparoBanco
{
    public static Task<ResultadoPreparoBanco> MigrarEVerificarAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
        => MigrarEVerificarAsync(context, opcoes: null, cancellationToken);

    public static async Task<ResultadoPreparoBanco> MigrarEVerificarAsync(
        AppDbContext context,
        OpcoesPreparoBanco? opcoes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        string? backup = null;
        if (opcoes is not null)
        {
            try
            {
                backup = await BackupBanco.CriarSeMigrationPendenteAsync(
                    context,
                    opcoes.VersaoAplicativo,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                var relatorioBackup = await VerificadorIntegridadeBanco.VerificarAsync(context, cancellationToken);
                return new ResultadoPreparoBanco(
                    ex,
                    relatorioBackup,
                    caminhoBackup: null,
                    aviso: "Não foi possível criar o backup consistente antes da migração. A migração não rodou e o banco não foi apagado.");
            }
        }

        ResultadoReconciliacao reconciliacao;
        try
        {
            reconciliacao = await ReconciliadorAddCartoes.ReconciliarSeParcialAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            var relatorioFalha = await VerificadorIntegridadeBanco.VerificarAsync(context, cancellationToken);
            return new ResultadoPreparoBanco(
                ex,
                relatorioFalha,
                backup,
                "Não foi possível reconciliar a migration AddCartoes. A migração não foi repetida e o banco não foi apagado.");
        }

        if (reconciliacao.Bloqueado)
        {
            var relatorioParcial = await VerificadorIntegridadeBanco.VerificarAsync(context, cancellationToken);
            return new ResultadoPreparoBanco(
                new InvalidOperationException(reconciliacao.Motivo),
                relatorioParcial,
                backup,
                reconciliacao.Motivo);
        }

        Exception? falhaMigrate = null;
        try
        {
            await context.Database.MigrateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            falhaMigrate = ex;
        }

        var relatorio = await VerificadorIntegridadeBanco.VerificarAsync(context, cancellationToken);
        return new ResultadoPreparoBanco(falhaMigrate, relatorio, backup);
    }

    public static async Task ExigirIntegroAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var resultado = await MigrarEVerificarAsync(context, cancellationToken);
        if (resultado.ExigeRecuperacao)
            throw new IntegridadeBancoException(resultado);
    }
}

public sealed class ResultadoPreparoBanco
{
    public ResultadoPreparoBanco(
        Exception? falhaMigrate,
        RelatorioIntegridadeBanco relatorio,
        string? caminhoBackup = null,
        string? aviso = null)
    {
        FalhaMigrate = falhaMigrate;
        Relatorio = relatorio;
        CaminhoBackup = caminhoBackup;
        Aviso = aviso;
    }

    public Exception? FalhaMigrate { get; }

    public RelatorioIntegridadeBanco Relatorio { get; }

    public string? CaminhoBackup { get; }

    public string? Aviso { get; }

    public bool ExigeRecuperacao => FalhaMigrate is not null || !Relatorio.Integro;
}

public sealed class IntegridadeBancoException : Exception
{
    public IntegridadeBancoException(ResultadoPreparoBanco resultado)
        : base(MontarMensagem(resultado), resultado.FalhaMigrate)
    {
        Resultado = resultado;
    }

    public ResultadoPreparoBanco Resultado { get; }

    private static string MontarMensagem(ResultadoPreparoBanco resultado)
    {
        if (!string.IsNullOrWhiteSpace(resultado.Aviso))
            return resultado.Aviso;

        if (resultado.FalhaMigrate is null || !resultado.Relatorio.Integro)
            return resultado.Relatorio.Resumo;

        return "A migração não concluiu. O check de estrutura não alterou o banco.";
    }
}
