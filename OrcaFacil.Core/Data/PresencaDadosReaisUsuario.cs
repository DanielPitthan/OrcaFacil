using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Armazenamento;

namespace OrcaFacil.Core.Data;

/// <summary>O que a leitura do banco vivo conseguiu afirmar.</summary>
public enum LeituraDadosReais
{
    /// <summary>Vazio, ou só com categoria padrão do sistema.</summary>
    SemDadosReais,

    /// <summary>Há registro que <see cref="PoliticaSemeadura.HaDadoDoUsuarioAsync"/> trata como dado do usuário.</summary>
    ComDadosReais,

    /// <summary>O arquivo não pôde ser lido. Não trate como vazio.</summary>
    Indeterminado
}

/// <summary>
/// Lê o banco vivo para o backup. O critério é
/// <see cref="PoliticaSemeadura.HaDadoDoUsuarioAsync"/>, o mesmo da abertura:
/// categoria padrão do sistema não conta e log de evento conta.
/// </summary>
public static class PresencaDadosReaisUsuario
{
    public static async Task<LeituraDadosReais> ClassificarAsync(
        string caminhoBanco,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(caminhoBanco) || !File.Exists(caminhoBanco))
            return LeituraDadosReais.SemDadosReais;

        try
        {
            var opcoes = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={caminhoBanco};Mode=ReadWrite;Pooling=False")
                .Options;
            await using var ctx = new AppDbContext(opcoes);
            return await ClassificarAsync(ctx, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return LeituraDadosReais.Indeterminado;
        }
    }

    public static async Task<LeituraDadosReais> ClassificarAsync(
        AppDbContext ctx,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        return await PoliticaSemeadura.HaDadoDoUsuarioAsync(ctx, cancellationToken)
            ? LeituraDadosReais.ComDadosReais
            : LeituraDadosReais.SemDadosReais;
    }
}
