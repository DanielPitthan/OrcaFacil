namespace OrcaFacil.Core.Llm;

/// <summary>
/// Chave da API fora de Preferences. A implementação do app usa o SecureStorage.
/// Não entra em log, snackbar, ToString nem URL.
/// <see cref="RemoverTudo"/> é o que "Limpar tudo" chama: <c>Preferences.Clear</c> não alcança esse arquivo.
/// </summary>
public interface ICofreChaveApi
{
    /// <summary>
    /// Lê a chave. A implementação do app trata falha de leitura como ausência
    /// e propaga <see cref="OperationCanceledException"/> do token.
    /// </summary>
    Task<string?> LerAsync(CancellationToken cancellationToken = default);

    Task GuardarAsync(string chave, CancellationToken cancellationToken = default);

    /// <summary>
    /// Apaga a chave. No app, chama <c>SecureStorage.Remove</c> e <c>RemoveAll</c>.
    /// <c>true</c> quando as duas chamadas concluíram. <c>false</c> quando alguma falhou
    /// e a chave pode ter permanecido. Não devolve a chave nem o texto da exceção.
    /// </summary>
    bool RemoverTudo();
}
