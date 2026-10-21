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

    public static string AplicarFracao(string? atual, string? entrada, int casasDecimais, decimal? maximo, bool substituir)
    {
        var casas = NormalizarCasas(casasDecimais);
        if (substituir)
        {
            var recebido = CanonicoDeTexto(entrada, casas);
            return AceitaFracao(recebido, casas, maximo) ? recebido : atual ?? "";
        }

        var resultado = atual ?? "";
        if (string.IsNullOrEmpty(entrada))
            return resultado;

        foreach (var caractere in entrada)
        {
            var proximo = AcrescentarFracao(resultado, caractere, casas);
            if (proximo is null)
                continue;
            if (!AceitaFracao(proximo, casas, maximo))
                break;
            resultado = proximo;
        }

        return resultado;
    }

    public static decimal ValorFracao(string? canonico)
    {
        if (string.IsNullOrEmpty(canonico))
            return 0m;

        var texto = canonico.EndsWith(',') ? canonico[..^1] : canonico;
        if (texto.Length == 0 || texto == "0")
            return 0m;

        var virgula = texto.IndexOf(',');
        var inteiro = virgula < 0 ? texto : texto[..virgula];
        var fracao = virgula < 0 ? "" : texto[(virgula + 1)..];
        var valor = inteiro.Length == 0 ? 0m : decimal.Parse(inteiro, NumberStyles.None, CultureInfo.InvariantCulture);
        if (fracao.Length == 0)
            return valor;

        return valor + decimal.Parse(fracao, NumberStyles.None, CultureInfo.InvariantCulture) / Potencia10(fracao.Length);
    }

    public static string CanonicoFracao(decimal valor, int casasDecimais)
    {
        var casas = NormalizarCasas(casasDecimais);
        var escalado = decimal.Round(Abs(valor) * Potencia10(casas), 0, MidpointRounding.AwayFromZero);
        if (escalado == 0 || casas == 0)
            return escalado == 0 ? "0" : decimal.Truncate(escalado).ToString("0", CultureInfo.InvariantCulture);

        var texto = decimal.Truncate(escalado).ToString("0", CultureInfo.InvariantCulture).PadLeft(casas + 1, '0');
        var inteiro = texto[..^casas].TrimStart('0');
        if (inteiro.Length == 0)
            inteiro = "0";
        var fracao = texto[^casas..].TrimEnd('0');
        return fracao.Length == 0 ? inteiro : inteiro + "," + fracao;
    }

    public static string ExibirFracao(string? canonico)
    {
        if (string.IsNullOrEmpty(canonico))
            return "0";

        var virgula = canonico.IndexOf(',');
        var inteiro = virgula < 0 ? canonico : canonico[..virgula];
        if (inteiro.Length == 0)
            inteiro = "0";

        var texto = SepararMilhar(inteiro);
        return virgula < 0 ? texto : texto + "," + canonico[(virgula + 1)..];
    }

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

    private static string? AcrescentarFracao(string atual, char caractere, int casas)
    {
        var temVirgula = atual.Contains(',');
        if (caractere is ',' or '.')
        {
            if (temVirgula || casas == 0)
                return null;
            return atual.Length == 0 ? "0," : atual + ",";
        }

        if (!char.IsDigit(caractere))
            return null;

        if (!temVirgula)
        {
            if (atual is "" or "0")
                return caractere == '0' ? "0" : caractere.ToString();
            return atual + caractere;
        }

        var fracao = atual[(atual.IndexOf(',') + 1)..];
        if (fracao.Length >= casas)
            return null;

        return atual + caractere;
    }

    private static bool AceitaFracao(string canonico, int casas, decimal? maximo)
    {
        if (canonico.Length == 0)
            return true;

        var virgula = canonico.IndexOf(',');
        var inteiro = virgula < 0 ? canonico : canonico[..virgula];
        var fracao = virgula < 0 ? "" : canonico[(virgula + 1)..];
        if (inteiro.Length > MaximoDigitos || fracao.Length > casas)
            return false;

        var valor = ValorFracao(canonico);
        return maximo is null || valor <= maximo.Value;
    }

    private static string CanonicoDeTexto(string? texto, int casas)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return "";

        var filtrado = new StringBuilder(texto.Length);
        foreach (var caractere in texto)
        {
            if (char.IsDigit(caractere) || caractere is ',' or '.')
                filtrado.Append(caractere);
        }

        var cru = filtrado.ToString();
        if (cru.Length == 0)
            return "";

        if (cru.Contains(','))
        {
            var semMilhar = cru.Replace(".", "", StringComparison.Ordinal);
            var partes = semMilhar.Split(',');
            if (partes.Length > 2)
                return "";

            var inteiro = NormalizarInteiro(partes[0]);
            var fracao = partes.Length > 1 ? partes[1] : "";
            if (fracao.Length > casas)
                fracao = fracao[..casas];
            if (inteiro.Length == 0)
                inteiro = "0";
            return partes.Length > 1 ? inteiro + "," + fracao : inteiro;
        }

        if (cru.Contains('.'))
        {
            if (EhMilhar(cru))
                return NormalizarInteiro(cru.Replace(".", "", StringComparison.Ordinal));

            var ponto = cru.IndexOf('.');
            if (ponto == cru.LastIndexOf('.'))
            {
                var inteiro = NormalizarInteiro(cru[..ponto]);
                var fracao = cru[(ponto + 1)..];
                if (fracao.Length > casas)
                    fracao = fracao[..casas];
                if (inteiro.Length == 0)
                    inteiro = "0";
                return fracao.Length == 0 ? inteiro : inteiro + "," + fracao;
            }

            return "";
        }

        return NormalizarInteiro(cru);
    }

    private static string NormalizarInteiro(string digitos)
    {
        var inicio = 0;
        while (inicio < digitos.Length - 1 && digitos[inicio] == '0')
            inicio++;

        return inicio >= digitos.Length ? "" : digitos[inicio..];
    }

    private static bool EhMilhar(string texto)
    {
        var partes = texto.Split('.');
        if (partes.Length < 2 || partes[0].Length is < 1 or > 3)
            return false;

        for (var i = 0; i < partes.Length; i++)
        {
            if (partes[i].Length == 0 || partes[i].Any(caractere => !char.IsDigit(caractere)))
                return false;
            if (i > 0 && partes[i].Length != 3)
                return false;
        }

        return true;
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
