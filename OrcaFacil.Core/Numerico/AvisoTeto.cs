using System.Globalization;

namespace OrcaFacil.Core.Numerico;

/// <summary>
/// A tecla ou a cola que passaria do teto não reescreve o texto.
/// O campo só avisa, com o teto real: "O máximo é 100."
/// </summary>
public static class AvisoTeto
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public readonly record struct Entrada(string Digitos, bool PassouDoTeto);

    public static string Mensagem(decimal teto)
        => "O máximo é " + teto.ToString("0.########", PtBr) + ".";

    public static Entrada Aplicar(
        string? atual,
        string? entrada,
        int casasDecimais,
        decimal? maximo,
        bool substituir,
        bool fracaoExplicita)
    {
        var aceito = Texto(atual, entrada, casasDecimais, maximo, substituir, fracaoExplicita);
        var livre = Texto(atual, entrada, casasDecimais, maximo: null, substituir, fracaoExplicita);
        var passou = maximo is not null && !string.Equals(aceito, livre, StringComparison.Ordinal);
        return new Entrada(aceito, passou);
    }

    private static string Texto(
        string? atual,
        string? entrada,
        int casasDecimais,
        decimal? maximo,
        bool substituir,
        bool fracaoExplicita)
    {
        if (fracaoExplicita)
            return MascaraDireitaEsquerda.AplicarFracao(atual, entrada, casasDecimais, maximo, substituir);

        return MascaraDireitaEsquerda.AplicarEntrada(atual ?? "", entrada, casasDecimais, maximo, substituir);
    }
}
