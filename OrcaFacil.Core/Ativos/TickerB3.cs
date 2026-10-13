using System.Text.RegularExpressions;

namespace OrcaFacil.Core.Ativos;

/// <summary>Ticker da B3: 4 letras, 1 ou 2 dígitos e sufixo F opcional (fracionário).</summary>
public static partial class TickerB3
{
    public const string MensagemInvalido =
        "Informe um ticker da B3: 4 letras, 1 ou 2 números e, se for fracionário, a letra F (ex.: PETR4, MXRF11, PETR4F).";

    public static string Normalizar(string? ticker) => ticker?.Trim().ToUpperInvariant() ?? string.Empty;

    public static bool EhValido(string? ticker) => Padrao().IsMatch(Normalizar(ticker));

    [GeneratedRegex("^[A-Z]{4}\\d{1,2}F?$", RegexOptions.CultureInvariant)]
    private static partial Regex Padrao();
}
