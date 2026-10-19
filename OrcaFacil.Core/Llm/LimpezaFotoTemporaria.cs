namespace OrcaFacil.Core.Llm;

/// <summary>
/// Decide se um arquivo temporário da câmera ou da galeria pode ser apagado.
/// Cache e pasta temporária entram. <c>perfil/</c> nunca entra, nem uma pasta irmã.
/// </summary>
public static class LimpezaFotoTemporaria
{
    public static bool PodeApagar(string? caminho, string? pastaPerfil, string? pastaCache, string? pastaTemporaria)
    {
        if (string.IsNullOrWhiteSpace(caminho))
            return false;

        if (EstaDentro(caminho, pastaPerfil))
            return false;

        return EstaDentro(caminho, pastaCache) || EstaDentro(caminho, pastaTemporaria);
    }

    public static bool EstaDentro(string? caminho, string? pasta)
    {
        if (string.IsNullOrWhiteSpace(caminho) || string.IsNullOrWhiteSpace(pasta))
            return false;

        string cheio;
        string raiz;
        try
        {
            cheio = Path.GetFullPath(caminho);
            raiz = Path.GetFullPath(pasta);
        }
        catch (Exception)
        {
            return false;
        }

        var prefixo = raiz.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return string.Equals(cheio, raiz, StringComparison.OrdinalIgnoreCase)
            || cheio.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase);
    }
}
