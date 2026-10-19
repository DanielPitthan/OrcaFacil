using OrcaFacil.Core.Tema;

namespace OrcaFacil.Core.Llm;

/// <summary>
/// Lembra, em Preferences, que a pessoa já viu o aviso da primeira leitura.
/// O valor é só a marca de visto. A chave da API não entra aqui.
/// </summary>
public static class AvisoPrivacidadeComprovante
{
    public const string ChavePreferencia = "Cesta.Leitura.AvisoPrivacidade";

    public const string ValorVisto = "visto";

    public static bool JaVisto(IPreferenciasSimples preferencias)
    {
        ArgumentNullException.ThrowIfNull(preferencias);
        return string.Equals(
            preferencias.Obter(ChavePreferencia, string.Empty),
            ValorVisto,
            StringComparison.Ordinal);
    }

    public static void MarcarVisto(IPreferenciasSimples preferencias)
    {
        ArgumentNullException.ThrowIfNull(preferencias);
        preferencias.Definir(ChavePreferencia, ValorVisto);
    }
}
