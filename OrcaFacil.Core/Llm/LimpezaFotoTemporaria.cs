namespace OrcaFacil.Core.Llm;

/// <summary>
/// Lista só o arquivo que o fluxo guardou e os diretórios ancestrais dele
/// dentro do cache ou da pasta temporária. Não varre essas raízes.
/// CSV, espelho e qualquer outro arquivo ficam de fora. <c>perfil/</c> nunca entra.
/// </summary>
public static class LimpezaFotoTemporaria
{
    /// <summary>
    /// O arquivo do fluxo, depois as pastas ancestrais, da mais funda para a mais rasa.
    /// A raiz do cache, a raiz temporária e <c>perfil/</c> não entram.
    /// Caminho nulo, vazio ou fora desses dois lugares devolve lista vazia.
    /// </summary>
    public static IReadOnlyList<string> ListarRemocao(
        string? caminhoDoFluxo,
        string? pastaPerfil,
        string? pastaCache,
        string? pastaTemporaria)
    {
        if (!PodeApagar(caminhoDoFluxo, pastaPerfil, pastaCache, pastaTemporaria))
            return [];

        string cheio;
        try
        {
            cheio = Path.GetFullPath(caminhoDoFluxo!);
        }
        catch (Exception)
        {
            return [];
        }

        if (EhRaizDeVolume(cheio) || EhRaizProtegida(cheio, pastaPerfil, pastaCache, pastaTemporaria))
            return [];

        var lista = new List<string> { cheio };
        var atual = Path.GetDirectoryName(cheio);
        while (!string.IsNullOrEmpty(atual))
        {
            string paiCheio;
            try
            {
                paiCheio = Path.GetFullPath(atual);
            }
            catch (Exception)
            {
                break;
            }

            if (EhRaizProtegida(paiCheio, pastaPerfil, pastaCache, pastaTemporaria))
                break;
            if (!PodeApagar(paiCheio, pastaPerfil, pastaCache, pastaTemporaria))
                break;

            lista.Add(paiCheio);
            var acima = Path.GetDirectoryName(paiCheio);
            if (acima is null || MesmoCaminho(acima, paiCheio))
                break;

            atual = acima;
        }

        return lista;
    }

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

    private static bool EhRaizDeVolume(string caminho)
    {
        string raiz;
        try
        {
            raiz = Path.GetPathRoot(caminho) ?? "";
        }
        catch (Exception)
        {
            return false;
        }

        return !string.IsNullOrEmpty(raiz) && MesmoCaminho(caminho, raiz);
    }

    private static bool EhRaizProtegida(
        string caminho,
        string? pastaPerfil,
        string? pastaCache,
        string? pastaTemporaria) =>
        MesmoCaminho(caminho, pastaPerfil)
        || MesmoCaminho(caminho, pastaCache)
        || MesmoCaminho(caminho, pastaTemporaria);

    private static bool MesmoCaminho(string? esquerda, string? direita)
    {
        if (string.IsNullOrWhiteSpace(esquerda) || string.IsNullOrWhiteSpace(direita))
            return false;

        try
        {
            return string.Equals(Normalizar(esquerda), Normalizar(direita), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Caminho cheio sem separador no fim. A raiz do volume permanece, para <c>/</c> não virar vazio.
    /// </summary>
    private static string Normalizar(string caminho)
    {
        var cheio = Path.GetFullPath(caminho);
        var raiz = Path.GetPathRoot(cheio);
        if (!string.IsNullOrEmpty(raiz) && string.Equals(cheio, raiz, StringComparison.OrdinalIgnoreCase))
            return raiz;

        return cheio.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
