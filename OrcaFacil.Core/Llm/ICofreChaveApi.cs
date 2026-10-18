namespace OrcaFacil.Core.Llm;

/// <summary>
/// Chave da API fora de Preferences. A implementação do app usa o SecureStorage.
/// Não entra em log, snackbar, ToString nem URL. A limpeza total não a remove daqui.
/// </summary>
public interface ICofreChaveApi
{
    Task<string?> LerAsync(CancellationToken cancellationToken = default);

    Task GuardarAsync(string chave, CancellationToken cancellationToken = default);
}
