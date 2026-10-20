namespace OrcaFacil.Core.Categorias;

/// <summary>
/// A cor da categoria só muda no OK da paleta.
/// Arrastar, Cancelar, Escape, clique no véu e voltar devolvem a cor original.
/// </summary>
public static class DecisaoCorPaleta
{
    public static string Resolver(string original, string? escolhida, bool confirmou)
    {
        if (!confirmou || string.IsNullOrWhiteSpace(escolhida))
            return original;

        return escolhida;
    }
}
