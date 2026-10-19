using OrcaFacil.Core.Data;

namespace OrcaFacil.Core.Armazenamento.Espelho;

/// <summary>
/// Manda o detalhe da falha ao log. Se o texto da exceção repete a senha,
/// o log recebe só uma frase fixa e a exceção não é anexada.
/// </summary>
internal static class RegistroFalhaEspelho
{
    public const string Detalhe = "Falha ao publicar o espelho.";

    public const string DetalheOmitido =
        "Falha ao publicar o espelho. O detalhe foi omitido porque poderia repetir a senha.";

    public static void Registrar(IRegistroLog? log, Exception excecao, params string?[] segredos)
    {
        ArgumentNullException.ThrowIfNull(excecao);
        if (log is null)
            return;

        try
        {
            if (RepeteSegredo(excecao, segredos))
                log.Erro(null, DetalheOmitido, []);
            else
                log.Erro(excecao, Detalhe, []);
        }
        catch (Exception)
        {
        }
    }

    private static bool RepeteSegredo(Exception excecao, string?[] segredos)
    {
        string texto;
        try
        {
            texto = excecao.ToString();
        }
        catch (Exception)
        {
            texto = "";
        }

        foreach (var segredo in segredos)
        {
            if (!string.IsNullOrEmpty(segredo) && texto.Contains(segredo, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
