using OrcaFacil.Core.Tema;

namespace OrcaFacil.Core.Llm;

/// <summary>
/// Lembra, em Preferences, que a pessoa já viu o aviso da primeira leitura.
/// O valor é só a marca de visto. A chave da API não entra aqui.
/// </summary>
public static class AvisoPrivacidadeComprovante
{
    /// <summary>Marca antiga do aviso genérico. O aviso atual não lê nem grava esta chave.</summary>
    public const string ChavePreferencia = "Cesta.Leitura.AvisoPrivacidade";

    /// <summary>Marca antiga do aviso do DeepSeek. O aviso atual não lê nem grava esta chave.</summary>
    public const string ChavePreferenciaDeepSeek = ChavePreferencia + "DeepSeek";

    /// <summary>Marca do aviso único de provedor externo. Quem já dispensou um aviso antigo vê este uma vez.</summary>
    public const string ChaveAvisoProvedorExterno = "Cesta.Leitura.AvisoProvedorExterno";

    public const string ValorVisto = "visto";

    /// <summary>O mesmo texto para qualquer provedor, com ou sem URL gravada.</summary>
    public static string TextoAvisoPrivacidade() =>
        MensagensLeituraComprovante.TextoAvisoPrivacidade;

    public static bool JaVisto(IPreferenciasSimples preferencias)
    {
        ArgumentNullException.ThrowIfNull(preferencias);
        return string.Equals(
            preferencias.Obter(ChaveAvisoProvedorExterno, string.Empty),
            ValorVisto,
            StringComparison.Ordinal);
    }

    public static void MarcarVisto(IPreferenciasSimples preferencias)
    {
        ArgumentNullException.ThrowIfNull(preferencias);
        preferencias.Definir(ChaveAvisoProvedorExterno, ValorVisto);
    }
}
