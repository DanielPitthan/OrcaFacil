using OrcaFacil.Core.Llm;

namespace OrcaFacil.Services;

/// <summary>
/// Chave da API só no SecureStorage. Não grava em Preferences e não entra em log nem em ToString.
/// </summary>
public sealed class CofreChaveApiSecureStorage : ICofreChaveApi
{
    public const string ChaveArmazenamento = "Ia.ChaveApi";

    public override string ToString() => "CofreChaveApiSecureStorage";

    public Task<string?> LerAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SecureStorage.Default.GetAsync(ChaveArmazenamento);
    }

    public Task GuardarAsync(string chave, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chave);
        if (chave.Contains('\r') || chave.Contains('\n'))
            throw new ArgumentException("A chave da API não pode ter quebra de linha.", nameof(chave));

        cancellationToken.ThrowIfCancellationRequested();
        return SecureStorage.Default.SetAsync(ChaveArmazenamento, chave);
    }
}
