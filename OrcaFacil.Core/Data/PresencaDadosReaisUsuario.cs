using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Armazenamento;

namespace OrcaFacil.Core.Data;

/// <summary>O que a leitura do banco vivo conseguiu afirmar.</summary>
public enum LeituraDadosReais
{
    /// <summary>Vazio, categoria padrão, demonstração semeada ou só o perfil mínimo. O backup recusa.</summary>
    SemDadosReais,

    /// <summary><see cref="PoliticaSemeadura"/> classificou <see cref="SituacaoConteudoBanco.DadoReal"/>. O backup pede confirmação.</summary>
    ComDadosReais,

    /// <summary>O arquivo não pôde ser lido. Não trate como vazio.</summary>
    Indeterminado
}

/// <summary>
/// Lê o banco vivo para o backup. Usa
/// <see cref="PoliticaSemeadura.ClassificarConteudoAsync"/>: vazio e só demonstração
/// recusam a troca do espelho; dado real, inclusive log, pede confirmação.
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
        return await PoliticaSemeadura.ClassificarConteudoAsync(ctx, cancellationToken) == SituacaoConteudoBanco.DadoReal
            ? LeituraDadosReais.ComDadosReais
            : LeituraDadosReais.SemDadosReais;
    }
}
