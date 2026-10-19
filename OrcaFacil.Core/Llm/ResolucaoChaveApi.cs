namespace OrcaFacil.Core.Llm;

/// <summary>
/// Chave usada no teste de conexão: a digitada, ou a salva no cofre.
/// Não engole cancelamento. Ausência de verdade é que vira "Informe a chave da API."
/// </summary>
public static class ResolucaoChaveApi
{
    public static async Task<string?> ResolverAsync(
        ICofreChaveApi cofre,
        string? chaveDigitada,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cofre);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(chaveDigitada))
            return chaveDigitada.Trim();

        var salva = await cofre.LerAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return string.IsNullOrWhiteSpace(salva) ? null : salva.Trim();
    }
}
