using System.Globalization;
using System.Text;

namespace OrcaFacil.Core.Numerico;

/// <summary>
/// Máscara numérica da direita para a esquerda, como nos aplicativos bancários:
/// os dígitos entram no fim e as casas decimais permanecem fixas.
/// </summary>
public static class MascaraDireitaEsquerda
{
    public const int MaximoDigitos = 15;

    public static string SomenteDigitos(string? texto)
    {
        if (string.IsNullOrEmpty(texto))
            return "";

        var digitos = new StringBuilder(texto.Length);
        foreach (var caractere in texto)
        {
            if (char.IsDigit(caractere))
                digitos.Append(caractere);
        }

        var inicio = 0;
        while (inicio < digitos.Length && digitos[inicio] == '0')
            inicio++;

        return digitos.ToString(inicio, digitos.Length - inicio);
    }

    public static string DigitosDe(decimal valor, int casasDecimais)
    {
        var casas = NormalizarCasas(casasDecimais);
        var escalado = decimal.Round(Abs(valor) * Potencia10(casas), 0, MidpointRounding.AwayFromZero);
        if (escalado == 0)
            return "";

        return decimal.Truncate(escalado).ToString("0", CultureInfo.InvariantCulture);
    }

    public static decimal ValorDe(string? digitos, int casasDecimais)
    {
        var casas = NormalizarCasas(casasDecimais);
        if (string.IsNullOrEmpty(digitos))
            return 0m;

        var escalado = decimal.Parse(digitos, NumberStyles.None, CultureInfo.InvariantCulture);
        return escalado / Potencia10(casas);
    }

    public static bool TentarValor(string? digitos, int casasDecimais, decimal? maximo, out decimal valor)
    {
        valor = 0m;
        var casas = NormalizarCasas(casasDecimais);
        if (string.IsNullOrEmpty(digitos))
            return true;

        if (digitos.Length > MaximoDigitos)
            return false;

        if (!decimal.TryParse(digitos, NumberStyles.None, CultureInfo.InvariantCulture, out var escalado))
            return false;

        valor = escalado / Potencia10(casas);
        return maximo is null || valor <= maximo.Value;
    }

    public static string AplicarEntrada(string digitosAtuais, string? entrada, int casasDecimais, decimal? maximo, bool substituir)
    {
        if (substituir)
        {
            var recebidos = SomenteDigitos(entrada);
            return TentarValor(recebidos, casasDecimais, maximo, out _) ? recebidos : digitosAtuais;
        }

        var resultado = digitosAtuais ?? "";
        if (string.IsNullOrEmpty(entrada))
            return resultado;

        foreach (var caractere in entrada)
        {
            if (!char.IsDigit(caractere))
                continue;

            if (caractere == '0' && resultado.Length == 0)
                continue;

            var proximo = resultado + caractere;
            if (!TentarValor(proximo, casasDecimais, maximo, out _))
                break;

            resultado = proximo;
        }

        return resultado;
    }

    public static string Apagar(string? digitos)
        => string.IsNullOrEmpty(digitos) ? "" : digitos[..^1];

    public static string Formatar(decimal valor, int casasDecimais)
    {
        var casas = NormalizarCasas(casasDecimais);
        var absoluto = Abs(valor);
        var escalado = decimal.Round(absoluto * Potencia10(casas), 0, MidpointRounding.AwayFromZero);
        var texto = decimal.Truncate(escalado).ToString("0", CultureInfo.InvariantCulture);

        if (casas == 0)
            return SepararMilhar(texto);

        texto = texto.PadLeft(casas + 1, '0');
        var inteiro = texto[..^casas];
        var fracao = texto[^casas..];
        return SepararMilhar(inteiro) + "," + fracao;
    }

    public static T Para<T>(decimal? valor)
    {
        if (valor is null)
            return default!;

        return (T)(object)valor.Value;
    }

    private static int NormalizarCasas(int casasDecimais) => Math.Clamp(casasDecimais, 0, 8);

    private static decimal Abs(decimal valor) => valor < 0 ? -valor : valor;

    private static decimal Potencia10(int casas)
    {
        decimal potencia = 1m;
        for (var i = 0; i < casas; i++)
            potencia *= 10m;
        return potencia;
    }

    private static string SepararMilhar(string inteiro)
    {
        if (inteiro.Length <= 3)
            return inteiro;

        var resultado = new StringBuilder(inteiro.Length + inteiro.Length / 3);
        var primeiroGrupo = inteiro.Length % 3;
        if (primeiroGrupo > 0)
            resultado.Append(inteiro.AsSpan(0, primeiroGrupo));

        for (var i = primeiroGrupo; i < inteiro.Length; i += 3)
        {
            if (resultado.Length > 0)
                resultado.Append('.');
            resultado.Append(inteiro.AsSpan(i, 3));
        }

        return resultado.ToString();
    }
}
