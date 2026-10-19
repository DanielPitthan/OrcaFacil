using OrcaFacil.Core.Llm;

namespace OrcaFacil.Tests.Tema;

/// <summary>Cofre sem chave, para testes de tema que não exercitam a limpeza do SecureStorage.</summary>
internal sealed class CofreChaveVazio : ICofreChaveApi
{
    public static CofreChaveVazio Instancia { get; } = new();

    private CofreChaveVazio()
    {
    }

    public Task<string?> LerAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<string?>(null);
    }

    public Task GuardarAsync(string chave, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TextoChaveApi.RecusarInvalida(chave);
        return Task.CompletedTask;
    }

    public Task<bool> RemoverTudo() => Task.FromResult(true);
}
