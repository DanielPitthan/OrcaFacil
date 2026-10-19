namespace OrcaFacil.Core.Llm;

/// <summary>
/// Fecha uma remoção do cofre. <c>Remove</c> pode devolver <c>true</c> e o arquivo
/// cifrado continuar no disco. O sucesso só existe quando uma leitura seguinte,
/// que não apaga nada, mostra que a chave não está mais.
/// </summary>
public static class RemocaoChaveApi
{
    public static async Task<bool> ConcluirAsync(
        Func<Task<bool>> remover,
        Func<Task<string?>> lerSemApagar)
    {
        ArgumentNullException.ThrowIfNull(remover);
        ArgumentNullException.ThrowIfNull(lerSemApagar);

        var remocaoAceita = false;
        try
        {
            remocaoAceita = await remover();
        }
        catch (Exception)
        {
            // A exceção pode citar a chave. Ela não sobe: a leitura decide.
            remocaoAceita = false;
        }

        string? restante;
        try
        {
            restante = await lerSemApagar();
        }
        catch (Exception)
        {
            // Sem confirmar a ausência, a limpeza não pode dizer que a chave sumiu.
            return false;
        }

        var ausente = string.IsNullOrEmpty(restante);
        if (!ausente)
            return false;

        // Remove devolve false quando a entrada já não existia. A leitura vazia confirma.
        return remocaoAceita || ausente;
    }
}
