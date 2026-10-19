namespace OrcaFacil.Core.Llm;

/// <summary>
/// Olha o corpo de um erro só para classificar. Não devolve o corpo, a chave nem a imagem.
/// </summary>
public static class DiagnosticoErroProvedor
{
    /// <summary>
    /// Verdadeiro quando o provedor recusou a foto porque o modelo não aceita imagem.
    /// </summary>
    public static bool IndicaModeloSemVisao(string? corpo)
    {
        if (string.IsNullOrWhiteSpace(corpo))
            return false;

        var texto = corpo.ToLowerInvariant();
        if (Contem(texto, "image content blocks are not supported"))
            return true;
        if (Contem(texto, "does not support image") ||
            Contem(texto, "do not support image") ||
            Contem(texto, "doesn't support image") ||
            Contem(texto, "don't support image"))
            return true;
        if (Contem(texto, "não suporta imagem") ||
            Contem(texto, "nao suporta imagem") ||
            Contem(texto, "não suportam imagem") ||
            Contem(texto, "nao suportam imagem"))
            return true;
        if (Contem(texto, "does not support vision") || Contem(texto, "vision is not supported"))
            return true;
        if (Contem(texto, "unsupported image") || Contem(texto, "images are not supported"))
            return true;

        var multimodal = Contem(texto, "multimodal") || Contem(texto, "multi-modal");
        var semSuporte = Contem(texto, "not support") || Contem(texto, "não suport") || Contem(texto, "nao suport");
        return multimodal && semSuporte;
    }

    private static bool Contem(string texto, string trecho) =>
        texto.Contains(trecho, StringComparison.Ordinal);
}
