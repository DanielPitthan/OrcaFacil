using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OrcaFacil.Core.B3;

/// <summary>
/// SHA-256 da linha, em hex minúsculo.
/// O texto é Data(yyyy-MM-dd)|Tipo|Ticker|Quantidade|ValorOperacao|Instituicao|Linha.
/// Decimais usam <see cref="CultureInfo.InvariantCulture"/>. Nulo vira vazio.
/// </summary>
public static class HashLinhaExtratoB3
{
    public static string Texto(LinhaExtratoB3 linha)
    {
        ArgumentNullException.ThrowIfNull(linha);
        return string.Join('|',
            linha.Data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            linha.Tipo.ToString(),
            linha.Ticker,
            Formatar(linha.Quantidade),
            Formatar(linha.ValorOperacao),
            linha.Instituicao,
            linha.Linha.ToString(CultureInfo.InvariantCulture));
    }

    public static string Calcular(LinhaExtratoB3 linha)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(Texto(linha)));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string Formatar(decimal? valor)
        => valor?.ToString(CultureInfo.InvariantCulture) ?? "";
}
