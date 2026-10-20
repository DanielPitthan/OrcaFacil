using OrcaFacil.Core.Tema;

namespace OrcaFacil.Core.Llm;

/// <summary>
/// Lembra, em Preferences, que a pessoa já viu o aviso da primeira leitura.
/// O valor é só a marca de visto. A chave da API não entra aqui.
/// </summary>
public static class AvisoPrivacidadeComprovante
{
    public const string ChavePreferencia = "Cesta.Leitura.AvisoPrivacidade";

    /// <summary>Quem já viu o aviso antigo torna a vê-lo uma vez quando o provedor é o DeepSeek.</summary>
    public const string ChavePreferenciaDeepSeek = ChavePreferencia + "DeepSeek";

    public const string ValorVisto = "visto";

    public static string ChaveDe(ProvedorDetectado provedor) =>
        provedor == ProvedorDetectado.DeepSeek ? ChavePreferenciaDeepSeek : ChavePreferencia;

    /// <summary>
    /// Texto do aviso. No DeepSeek, anexa <see cref="MensagensLeituraComprovante.ComplementoPrivacidadeDeepSeek"/>.
    /// Nos outros provedores, devolve o texto genérico.
    /// </summary>
    public static string TextoAvisoPrivacidade(ProvedorDetectado provedor) =>
        provedor == ProvedorDetectado.DeepSeek
            ? MensagensLeituraComprovante.TextoAvisoPrivacidade + " " + MensagensLeituraComprovante.ComplementoPrivacidadeDeepSeek
            : MensagensLeituraComprovante.TextoAvisoPrivacidade;

    public static bool JaVisto(IPreferenciasSimples preferencias) =>
        JaVisto(preferencias, ProvedorDetectado.Desconhecido);

    public static bool JaVisto(IPreferenciasSimples preferencias, ProvedorDetectado provedor)
    {
        ArgumentNullException.ThrowIfNull(preferencias);
        return string.Equals(
            preferencias.Obter(ChaveDe(provedor), string.Empty),
            ValorVisto,
            StringComparison.Ordinal);
    }

    public static void MarcarVisto(IPreferenciasSimples preferencias) =>
        MarcarVisto(preferencias, ProvedorDetectado.Desconhecido);

    public static void MarcarVisto(IPreferenciasSimples preferencias, ProvedorDetectado provedor)
    {
        ArgumentNullException.ThrowIfNull(preferencias);
        preferencias.Definir(ChaveDe(provedor), ValorVisto);
    }
}
