namespace OrcaFacil.Core.Llm;

/// <summary>
/// O que este app aceita gravar no cofre. Quebra de linha e caractere nulo
/// são lixo: a leitura seguinte os trata como chave corrompida e pede para apagar.
/// </summary>
public static class TextoChaveApi
{
    public const string MensagemInvalida =
        "A chave da API não pode ter quebra de linha nem caractere nulo.";

    public static bool TemLixo(string chave)
        => chave.Contains('\0') || chave.Contains('\r') || chave.Contains('\n');

    public static void RecusarInvalida(string chave)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chave);
        if (TemLixo(chave))
            throw new ArgumentException(MensagemInvalida, nameof(chave));
    }
}
