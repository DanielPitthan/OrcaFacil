using Microsoft.EntityFrameworkCore;

namespace OrcaFacil.Core.Data;

/// <summary>
/// Aplica as migrations e, em seguida, só lê o arquivo para ver se a estrutura
/// bate com o model. O check em si não grava nem apaga.
/// </summary>
public static class PreparoBanco
{
    public static async Task<ResultadoPreparoBanco> MigrarEVerificarAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

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
        return new ResultadoPreparoBanco(falhaMigrate, relatorio);
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
    public ResultadoPreparoBanco(Exception? falhaMigrate, RelatorioIntegridadeBanco relatorio)
    {
        FalhaMigrate = falhaMigrate;
        Relatorio = relatorio;
    }

    public Exception? FalhaMigrate { get; }

    public RelatorioIntegridadeBanco Relatorio { get; }

    public bool ExigeRecuperacao => FalhaMigrate is not null || !Relatorio.Integro;
}

public sealed class IntegridadeBancoException : Exception
{
    public IntegridadeBancoException(ResultadoPreparoBanco resultado)
        : base(resultado.FalhaMigrate is null || !resultado.Relatorio.Integro
            ? resultado.Relatorio.Resumo
            : "A migração não concluiu. O check de estrutura não alterou o banco.", resultado.FalhaMigrate)
    {
        Resultado = resultado;
    }

    public ResultadoPreparoBanco Resultado { get; }
}
