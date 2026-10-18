using OrcaFacil.Core.Llm;

namespace OrcaFacil.Services;

/// <summary>
/// Chave da API só no SecureStorage. Não grava em Preferences e não entra em log nem em ToString.
/// A leitura passa por <see cref="LeituraChaveApi"/>: falha vira ausência e pede RemoveAll;
/// cancelamento do token segue sem apagar a chave.
/// </summary>
public sealed class CofreChaveApiSecureStorage : ICofreChaveApi
{
    public const string ChaveArmazenamento = "Ia.ChaveApi";

    public override string ToString() => "CofreChaveApiSecureStorage";

    public Task<string?> LerAsync(CancellationToken cancellationToken = default)
        => LeituraChaveApi.LerAsync(LerDoSecureStorage, RemoverTudo, cancellationToken);

    public Task GuardarAsync(string chave, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chave);
        if (chave.Contains('\r') || chave.Contains('\n'))
            throw new ArgumentException("A chave da API não pode ter quebra de linha.", nameof(chave));

        cancellationToken.ThrowIfCancellationRequested();
        return SecureStorage.Default.SetAsync(ChaveArmazenamento, chave);
    }

    /// <summary>
    /// <c>Preferences.Clear</c> não toca neste arquivo.
    /// <c>Remove</c> tira a chave; <c>RemoveAll</c> limpa o cofre quando o arquivo está ilegível.
    /// </summary>
    public void RemoverTudo()
    {
        Executar(static () => { _ = SecureStorage.Default.Remove(ChaveArmazenamento); });
        Executar(static () => SecureStorage.Default.RemoveAll());
    }

    private static Task<string?> LerDoSecureStorage(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SecureStorage.Default.GetAsync(ChaveArmazenamento);
    }

    private static void Executar(Action acao)
    {
        try
        {
            acao();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Sem log: a exceção do cofre pode carregar detalhe da chave.
        }
    }
}
