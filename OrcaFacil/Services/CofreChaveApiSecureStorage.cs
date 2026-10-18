using OrcaFacil.Core.Llm;

namespace OrcaFacil.Services;

/// <summary>
/// Chave da API só no SecureStorage. Não grava em Preferences e não entra em log nem em ToString.
/// A leitura passa por <see cref="LeituraChaveApi"/>: lixo comprovado pede RemoveAll;
/// falha transitória preserva a chave. Cancelamento do token segue sem apagar.
/// </summary>
public sealed class CofreChaveApiSecureStorage : ICofreChaveApi
{
    public const string ChaveArmazenamento = "Ia.ChaveApi";

    public override string ToString() => "CofreChaveApiSecureStorage";

    public Task<string?> LerAsync(CancellationToken cancellationToken = default)
        => LeituraChaveApi.LerAsync(LerDoSecureStorage, RemoverTudo, cancellationToken);

    public Task GuardarAsync(string chave, CancellationToken cancellationToken = default)
    {
        TextoChaveApi.RecusarInvalida(chave);
        cancellationToken.ThrowIfCancellationRequested();
        return SecureStorage.Default.SetAsync(ChaveArmazenamento, chave);
    }

    /// <summary>
    /// <c>Preferences.Clear</c> não toca neste arquivo.
    /// <c>Remove</c> devolve se a entrada saiu; <c>RemoveAll</c> limpa o arquivo cifrado.
    /// O resultado só é sucesso se uma leitura seguinte, sem apagar, não achar a chave.
    /// A exceção não sobe e não entra em log.
    /// </summary>
    public Task<bool> RemoverTudo()
        => RemocaoChaveApi.ConcluirAsync(ApagarNoSecureStorage, LerValorSemApagar);

    private static Task<bool> ApagarNoSecureStorage()
    {
        var removeu = false;
        try
        {
            removeu = SecureStorage.Default.Remove(ChaveArmazenamento);
        }
        catch (Exception)
        {
            removeu = false;
        }

        var limpouArquivo = false;
        try
        {
            SecureStorage.Default.RemoveAll();
            limpouArquivo = true;
        }
        catch (Exception)
        {
            limpouArquivo = false;
        }

        return Task.FromResult(removeu && limpouArquivo);
    }

    private static Task<string?> LerValorSemApagar()
        => SecureStorage.Default.GetAsync(ChaveArmazenamento);

    private static Task<string?> LerDoSecureStorage(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SecureStorage.Default.GetAsync(ChaveArmazenamento);
    }
}
