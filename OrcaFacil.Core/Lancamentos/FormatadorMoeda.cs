using System.Globalization;

namespace OrcaFacil.Core.Lancamentos;

/// <summary>Como o valor aparece. A magnitude nunca entra no formato de moeda com sinal da cultura.</summary>
public enum SinalMoeda
{
    /// <summary>R$ 100,00. Compra na fatura e saldo que não é negativo.</summary>
    Neutro,

    /// <summary>− R$ 100,00. Saída, com o sinal separado do símbolo.</summary>
    Saida,

    /// <summary>+ R$ 100,00. Crédito, receita ou estorno.</summary>
    Credito
}

/// <summary>
/// Único lugar dos prefixos de moeda. A cultura pt-BR formata negativo como "-R$";
/// prefixar isso de novo produziria "− -R$". Aqui o número é sempre a magnitude.
/// </summary>
public static class FormatadorMoeda
{
    public const string PrefixoSaida = "− ";
    public const string PrefixoCredito = "+ ";

    public static string Formatar(decimal magnitude, SinalMoeda sinal, CultureInfo cultura)
    {
        var texto = Math.Abs(magnitude).ToString("C", cultura);
        return sinal switch
        {
            SinalMoeda.Credito => PrefixoCredito + texto,
            SinalMoeda.Saida => PrefixoSaida + texto,
            _ => texto
        };
    }

    /// <summary>Negativo vira saída; zero e positivo ficam sem prefixo.</summary>
    public static string FormatarSaldo(decimal valor, CultureInfo cultura)
        => valor < 0
            ? Formatar(valor, SinalMoeda.Saida, cultura)
            : Formatar(valor, SinalMoeda.Neutro, cultura);

    /// <summary>Líquido do dia: negativo é saída, positivo é crédito, zero é neutro.</summary>
    public static string FormatarLiquido(decimal valor, CultureInfo cultura)
        => valor < 0
            ? Formatar(valor, SinalMoeda.Saida, cultura)
            : valor > 0
                ? Formatar(valor, SinalMoeda.Credito, cultura)
                : Formatar(valor, SinalMoeda.Neutro, cultura);

    public static string FormatarLancamento(decimal magnitude, bool receita, CultureInfo cultura)
        => Formatar(magnitude, receita ? SinalMoeda.Credito : SinalMoeda.Saida, cultura);

    /// <summary>Estorno mostra crédito; compra fica na magnitude, sem sinal colado.</summary>
    public static string FormatarItemFatura(decimal magnitude, bool ehEstorno, CultureInfo cultura)
        => Formatar(magnitude, ehEstorno ? SinalMoeda.Credito : SinalMoeda.Neutro, cultura);
}
