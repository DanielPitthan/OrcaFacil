namespace OrcaFacil.Core.Llm;

public enum ModoThinking
{
    NaoEnviar,
    Desligar
}

public enum ProvedorDetectado
{
    Anthropic,
    DeepSeek,
    Desconhecido,
    QwenCloud
}

/// <summary>
/// Perfil puro da URL. O host vem de <see cref="PedidoMensagem.CriarUri"/>.
/// DeepSeek e Anthropic comparam o host exato. QwenCloud casa o domínio e os subdomínios.
/// A comparação ignora maiúsculas. O thinking segue o host, não o chip.
/// A URL do pedido é a que o app gravou; este tipo não escolhe endereço.
/// </summary>
public sealed record PerfilProvedor(ProvedorDetectado Provedor, ModoThinking Thinking)
{
    private const string HostDeepSeek = "api.deepseek.com";
    private const string HostAnthropic = "api.anthropic.com";
    private const string HostQwenCloud = "qwencloudapi.com";

    public static PerfilProvedor DaUrl(string? urlBase)
    {
        var uri = PedidoMensagem.CriarUri(urlBase);
        if (uri is null)
            return new(ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar);

        if (string.Equals(uri.Host, HostDeepSeek, StringComparison.OrdinalIgnoreCase))
            return new(ProvedorDetectado.DeepSeek, ModoThinking.Desligar);

        if (EhHostQwenCloud(uri.Host))
            return new(ProvedorDetectado.QwenCloud, ModoThinking.Desligar);

        if (string.Equals(uri.Host, HostAnthropic, StringComparison.OrdinalIgnoreCase))
            return new(ProvedorDetectado.Anthropic, ModoThinking.NaoEnviar);

        return new(ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar);
    }

    /// <summary>
    /// Casa o ápice e qualquer subdomínio. Um host que só contém o texto
    /// (qwencloudapi.com.evil.com, notqwencloudapi.com) fica de fora.
    /// </summary>
    private static bool EhHostQwenCloud(string host) =>
        host.Equals(HostQwenCloud, StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith("." + HostQwenCloud, StringComparison.OrdinalIgnoreCase);
}
