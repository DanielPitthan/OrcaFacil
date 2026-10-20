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

    public const string ChavePreferenciaAlibaba = ChavePreferencia + "Alibaba";

    public const string ChavePreferenciaAlibabaPadrao = ChavePreferencia + "AlibabaPadrao";

    public const string ValorVisto = "visto";

    /// <summary>
    /// Provedor do aviso quando não há preferência salva.
    /// O chip padrão do app pode ser outro; este é o ponto a alinhar com esse padrão.
    /// </summary>
    public const ProvedorDetectado ProvedorDoAvisoSemPreferencia = ProvedorDetectado.Alibaba;

    public static string ChaveDe(ProvedorDetectado provedor, bool semUrlGravada = false)
    {
        if (semUrlGravada && ProvedorDoAvisoSemPreferencia == ProvedorDetectado.Alibaba)
            return ChavePreferenciaAlibabaPadrao;

        return provedor switch
        {
            ProvedorDetectado.Alibaba => ChavePreferenciaAlibaba,
            ProvedorDetectado.DeepSeek => ChavePreferenciaDeepSeek,
            _ => ChavePreferencia
        };
    }

    /// <summary>
    /// Texto do aviso. Na Alibaba configurada, anexa <see cref="MensagensLeituraComprovante.ComplementoPrivacidadeAlibaba"/>.
    /// Sem preferência salva, e só então, o aviso é <see cref="MensagensLeituraComprovante.ComplementoPrivacidadeAlibabaPadrao"/>,
    /// sem a frase do provedor da URL configurada.
    /// Nos outros provedores, devolve o texto genérico.
    /// </summary>
    public static string TextoAvisoPrivacidade(ProvedorDetectado provedor) =>
        TextoAvisoPrivacidade(provedor, semUrlGravada: false);

    public static string TextoAvisoPrivacidade(ProvedorDetectado provedor, bool semUrlGravada)
    {
        if (semUrlGravada && ProvedorDoAvisoSemPreferencia == ProvedorDetectado.Alibaba)
            return MensagensLeituraComprovante.ComplementoPrivacidadeAlibabaPadrao;

        if (provedor != ProvedorDetectado.Alibaba)
            return MensagensLeituraComprovante.TextoAvisoPrivacidade;

        return MensagensLeituraComprovante.TextoAvisoPrivacidade
            + " " + MensagensLeituraComprovante.ComplementoPrivacidadeAlibaba;
    }

    public static bool JaVisto(IPreferenciasSimples preferencias) =>
        JaVisto(preferencias, ProvedorDetectado.Desconhecido);

    public static bool JaVisto(IPreferenciasSimples preferencias, ProvedorDetectado provedor, bool semUrlGravada = false)
    {
        ArgumentNullException.ThrowIfNull(preferencias);
        return string.Equals(
            preferencias.Obter(ChaveDe(provedor, semUrlGravada), string.Empty),
            ValorVisto,
            StringComparison.Ordinal);
    }

    public static void MarcarVisto(IPreferenciasSimples preferencias) =>
        MarcarVisto(preferencias, ProvedorDetectado.Desconhecido);

    public static void MarcarVisto(IPreferenciasSimples preferencias, ProvedorDetectado provedor, bool semUrlGravada = false)
    {
        ArgumentNullException.ThrowIfNull(preferencias);
        preferencias.Definir(ChaveDe(provedor, semUrlGravada), ValorVisto);
    }
}
