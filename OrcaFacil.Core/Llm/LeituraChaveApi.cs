namespace OrcaFacil.Core.Llm;

/// <summary>
/// Leitura da chave em volta de <c>SecureStorage.GetAsync</c>.
/// Lixo comprovado (<c>\0</c>, quebra de linha ou falha permanente de decriptação)
/// pede <c>RemoveAll</c>. Falha transitória vira ausência só nesta leitura e preserva a chave.
/// <see cref="OperationCanceledException"/> do token não apaga a chave e não vira ausência.
/// </summary>
public static class LeituraChaveApi
{
    private static readonly string[] NomesPermanentes =
    [
        "GeneralSecurityException",
        "BadPaddingException",
        "AEADBadTagException",
        "InvalidProtocolBufferException",
        "KeyPermanentlyInvalidatedException",
        "KeyStoreException"
    ];

    public static async Task<string?> LerAsync(
        Func<CancellationToken, Task<string?>> ler,
        Func<Task<bool>> removerTudo,
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

            await Esquecer(removerTudo);
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            if (FalhaPermanenteDeDecriptacao(ex))
                await Esquecer(removerTudo);

            return null;
        }
    }

    /// <summary>
    /// <c>true</c> para <see cref="System.Security.Cryptography.CryptographicException"/>
    /// e para o equivalente que o SecureStorage encontra quando a chave do Keystore mudou
    /// (<c>GeneralSecurityException</c>, <c>BadPaddingException</c>, <c>KeyStoreException</c>
    /// que não é indisponibilidade, e os vizinhos de decriptação).
    /// <see cref="System.IO.IOException"/>, timeout e Keystore indisponível devolvem <c>false</c>.
    /// </summary>
    public static bool FalhaPermanenteDeDecriptacao(Exception excecao)
    {
        ArgumentNullException.ThrowIfNull(excecao);

        foreach (var atual in Cadeia(excecao))
        {
            if (atual is OperationCanceledException)
                return false;

            if (EhTransitoria(atual))
                return false;

            if (atual is System.Security.Cryptography.CryptographicException || NomePermanente(atual))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Valor que este app não grava. Quebra de linha ou nulo depois de um restore
    /// é lixo cifrado que não estourou exceção. Nulo e vazio são ausência, não lixo.
    /// </summary>
    private static bool ValorCorrompido(string? valor)
    {
        if (string.IsNullOrEmpty(valor))
            return false;

        return TextoChaveApi.TemLixo(valor);
    }

    private static bool EhTransitoria(Exception ex)
    {
        if (ex is IOException or TimeoutException)
            return true;

        var transitoria = ex.GetType().GetProperty("IsTransientFailure");
        if (transitoria?.PropertyType == typeof(bool) && transitoria.GetValue(ex) is true)
            return true;

        if (ex.GetType().Name is "KeyStoreException")
        {
            var mensagem = ex.Message ?? "";
            if (mensagem.Contains("unavailable", StringComparison.OrdinalIgnoreCase)
                || mensagem.Contains("indispon", StringComparison.OrdinalIgnoreCase)
                || mensagem.Contains("temporarily", StringComparison.OrdinalIgnoreCase)
                || mensagem.Contains("temporari", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool NomePermanente(Exception ex)
        => NomesPermanentes.Contains(ex.GetType().Name);

    private static IEnumerable<Exception> Cadeia(Exception excecao)
    {
        var pilha = new Stack<Exception>();
        var vistos = new HashSet<Exception>();
        pilha.Push(excecao);

        while (pilha.Count > 0)
        {
            var atual = pilha.Pop();
            if (!vistos.Add(atual))
                continue;

            yield return atual;

            if (atual is AggregateException agregada)
            {
                for (var i = agregada.InnerExceptions.Count - 1; i >= 0; i--)
                    pilha.Push(agregada.InnerExceptions[i]);
            }
            else if (atual.InnerException is not null)
            {
                pilha.Push(atual.InnerException);
            }
        }
    }

    private static async Task Esquecer(Func<Task<bool>> removerTudo)
    {
        try
        {
            _ = await removerTudo();
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
