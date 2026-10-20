using System.Globalization;
using System.Text;

namespace OrcaFacil.Core.B3;

/// <summary>
/// Lê quantidade, preço e valor como vieram no extrato. O parse é <see cref="decimal"/>
/// na cultura pt-BR, sem passar por double e sem arredondar.
/// Vazio ou traço viram nulo.
/// </summary>
internal static class NumeroPtBr
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    public static bool TentarOpcional(string? texto, out decimal? valor)
    {
        valor = null;
        var limpo = Limpar(texto);
        if (limpo.Length == 0)
            return true;

        if (!decimal.TryParse(limpo, NumberStyles.Number, Cultura, out var numero))
            return false;

        valor = numero;
        return true;
    }

    private static string Limpar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return "";

        var sb = new StringBuilder(texto.Length);
        var bruto = texto.Trim();
        for (var i = 0; i < bruto.Length; i++)
        {
            var caractere = bruto[i];
            if ((caractere is 'R' or 'r') && i + 1 < bruto.Length && bruto[i + 1] == '$')
            {
                i++;
                continue;
            }

            if (char.IsWhiteSpace(caractere) || caractere == '\u00A0')
                continue;

            sb.Append(caractere);
        }

        var resultado = sb.ToString();
        return resultado is "-" or "—" or "–" ? "" : resultado;
    }
}
