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
    /// Apaga a chave. No app, chama <c>SecureStorage.Remove</c> e <c>RemoveAll</c>
    /// e confirma com uma leitura que não apaga nada.
    /// <c>true</c> só quando essa leitura mostra que a chave não está.
    /// <c>false</c> quando ela ficou ou a ausência não pôde ser confirmada.
    /// Não devolve a chave nem o texto da exceção. Pode ser chamado de novo.
    /// </summary>
    Task<bool> RemoverTudo();
}
