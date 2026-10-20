using System.Globalization;
using System.Text;

namespace OrcaFacil.Core.B3;

internal static class TextoB3
{
    public static string NormalizarChave(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return "";

        var semAcento = RemoverAcentos(texto.Trim().ToLowerInvariant());
        var sb = new StringBuilder(semAcento.Length);
        var espacoPendente = false;
        foreach (var caractere in semAcento)
        {
            if (char.IsWhiteSpace(caractere))
            {
                if (sb.Length > 0)
                    espacoPendente = true;
                continue;
            }

            if (espacoPendente)
            {
                sb.Append(' ');
                espacoPendente = false;
            }

            sb.Append(caractere);
        }

        return sb.ToString();
    }

    public static string RemoverAcentos(string texto)
    {
        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        foreach (var caractere in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
                sb.Append(caractere);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
