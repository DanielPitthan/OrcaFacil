namespace OrcaFacil.Core.Armazenamento.Espelho;

/// <summary>
/// A senha memorizada vale para a pasta que estava escolhida.
/// Uma URI diferente esquece a senha. A mesma URI, o cancelamento ou repetir a escolha mantêm.
/// </summary>
public static class SenhaDaPastaEspelho
{
    public static bool AoConfirmarEscolha(IDefinicaoSenhaEspelho definicao, string? uriAnterior, string? uriNova)
    {
        ArgumentNullException.ThrowIfNull(definicao);
        if (string.Equals(uriAnterior, uriNova, StringComparison.Ordinal))
            return false;

        definicao.Esquecer();
        return true;
    }
}
