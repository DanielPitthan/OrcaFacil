using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Ativos;

namespace OrcaFacil.Core.Data;

/// <summary>
/// Aplica as migrations e, em seguida, só lê o arquivo para ver se a estrutura
/// bate com o model. O check em si não grava nem apaga.
/// Com a estrutura íntegra, ETF conhecido gravado como ação passa a ETF.
/// </summary>
public sealed class OpcoesPreparoBanco
{
    public string VersaoAplicativo { get; init; } = "desconhecida";
}

public static class PreparoBanco
{
    public const string AvisoArquivoIlegivel =
        "Não foi possível ler o orcafacil.db (arquivo ilegível ou corrompido). O check não alterou o arquivo e nada foi apagado.";

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
                var (relatorioBackup, falhaLeitura) = await TentarVerificarAsync(context, cancellationToken);
                return new ResultadoPreparoBanco(
                    falhaLeitura ?? ex,
                    relatorioBackup,
                    caminhoBackup: null,
                    aviso: falhaLeitura is null
                        ? "Não foi possível criar o backup consistente antes da migração. A migração não rodou e o banco não foi apagado."
                        : AvisoArquivoIlegivel);
            }
        }

        ResultadoReconciliacao reconciliacao;
        try
        {
            reconciliacao = await ReconciliadorAddCartoes.ReconciliarSeParcialAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            var (relatorioFalha, falhaLeitura) = await TentarVerificarAsync(context, cancellationToken);
            return new ResultadoPreparoBanco(
                falhaLeitura ?? ex,
                relatorioFalha,
                backup,
                falhaLeitura is null
                    ? "Não foi possível reconciliar a migration AddCartoes. A migração não foi repetida. " + ReconciliadorAddCartoes.AvisoArquivoPreservado
                    : AvisoArquivoIlegivel);
        }

        if (reconciliacao.Bloqueado)
        {
            var (relatorioParcial, falhaLeitura) = await TentarVerificarAsync(context, cancellationToken);
            if (falhaLeitura is not null)
            {
                return new ResultadoPreparoBanco(falhaLeitura, relatorioParcial, backup, AvisoArquivoIlegivel);
            }

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

        var (relatorio, falhaLeituraFinal) = await TentarVerificarAsync(context, cancellationToken);
        if (falhaLeituraFinal is not null)
        {
            return new ResultadoPreparoBanco(
                falhaMigrate ?? falhaLeituraFinal,
                relatorio,
                backup,
                AvisoArquivoIlegivel);
        }

        if (falhaMigrate is null && relatorio.Integro)
        {
            try
            {
                await CatalogoEtfConhecido.ReclassificarAsync(context, cancellationToken);
            }
            catch (Exception ex)
            {
                return new ResultadoPreparoBanco(
                    ex,
                    relatorio,
                    backup,
                    "Não foi possível reclassificar o ETF conhecido gravado como ação. A migração já tinha concluído e o banco não foi apagado.");
            }
        }

        return new ResultadoPreparoBanco(falhaMigrate, relatorio, backup);
    }

    private static async Task<(RelatorioIntegridadeBanco Relatorio, Exception? FalhaLeitura)> TentarVerificarAsync(
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await VerificadorIntegridadeBanco.VerificarAsync(context, cancellationToken), null);
        }
        catch (Exception ex)
        {
            return (VerificadorIntegridadeBanco.RelatorioIlegivel(context), ex);
        }
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
