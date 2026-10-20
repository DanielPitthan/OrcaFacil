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
/// DeepSeek e Anthropic comparam o host exato. QwenCloud só passa por <see cref="EhQwenCloud"/>.
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

        if (EhQwenCloud(uri.Host))
            return new(ProvedorDetectado.QwenCloud, ModoThinking.Desligar);

        if (string.Equals(uri.Host, HostAnthropic, StringComparison.OrdinalIgnoreCase))
            return new(ProvedorDetectado.Anthropic, ModoThinking.NaoEnviar);

        return new(ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar);
    }

    /// <summary>
    /// Único critério de host da QwenCloud. Casa o ápice e qualquer subdomínio.
    /// O ponto final de um FQDN (<c>token-plan.maas.qwencloudapi.com.</c>) não muda o nome.
    /// Um host que só contém o texto (qwencloudapi.com.evil.com, notqwencloudapi.com) fica de fora.
    /// </summary>
    public static bool EhQwenCloud(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        var nome = host.Trim().TrimEnd('.');
        return nome.Equals(HostQwenCloud, StringComparison.OrdinalIgnoreCase)
            || nome.EndsWith("." + HostQwenCloud, StringComparison.OrdinalIgnoreCase);
    }
}
