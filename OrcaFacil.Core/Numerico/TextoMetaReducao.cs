using System.Globalization;

namespace OrcaFacil.Core.Numerico;

/// <summary>
/// Meta de redução na tela: até uma casa em pt-BR, sem zero à direita (12,5, 0,5, 10).
/// </summary>
public static class TextoMetaReducao
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Percentual(decimal valor)
        => valor.ToString("0.#", PtBr);
}
