namespace OrcaFacil.Core.Llm;

/// <summary>
/// Leitura da chave em volta de <c>SecureStorage.GetAsync</c>.
/// Backup restaurado sem a chave do Keystore pode lançar: a falha vira ausência
/// e pede <c>RemoveAll</c>. O app segue sem a chave.
/// <see cref="OperationCanceledException"/> do token não apaga a chave e não vira ausência.
/// </summary>
public static class LeituraChaveApi
{
    public static async Task<string?> LerAsync(
        Func<CancellationToken, Task<string?>> ler,
        Action removerTudo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ler);
        ArgumentNullException.ThrowIfNull(removerTudo);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var valor = await ler(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!ValorCorrompido(valor))
                return valor;

            Esquecer(removerTudo);
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            Esquecer(removerTudo);
            return null;
        }
    }

    /// <summary>
    /// Valor que este app não grava. Quebra de linha ou nulo depois de um restore
    /// é lixo cifrado que não estourou exceção.
    /// </summary>
    private static bool ValorCorrompido(string? valor)
    {
        if (string.IsNullOrEmpty(valor))
            return false;

        return valor.Contains('\0') || valor.Contains('\r') || valor.Contains('\n');
    }

    private static void Esquecer(Action removerTudo)
    {
        try
        {
            removerTudo();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // A tela abre mesmo se o cofre ilegível também recusar a limpeza.
        }
    }
}
