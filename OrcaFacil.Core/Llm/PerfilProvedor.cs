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
    Desconhecido
}

/// <summary>
/// Perfil puro da URL. O host vem de <see cref="PedidoMensagem.CriarUri"/>.
/// A comparação é exata e ignora maiúsculas. O thinking segue o host, não o chip.
/// </summary>
public sealed record PerfilProvedor(ProvedorDetectado Provedor, ModoThinking Thinking)
{
    private const string HostDeepSeek = "api.deepseek.com";
    private const string HostAnthropic = "api.anthropic.com";

    public static PerfilProvedor DaUrl(string? urlBase)
    {
        var uri = PedidoMensagem.CriarUri(urlBase);
        if (uri is null)
            return new(ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar);

        if (string.Equals(uri.Host, HostDeepSeek, StringComparison.OrdinalIgnoreCase))
            return new(ProvedorDetectado.DeepSeek, ModoThinking.Desligar);

        if (string.Equals(uri.Host, HostAnthropic, StringComparison.OrdinalIgnoreCase))
            return new(ProvedorDetectado.Anthropic, ModoThinking.NaoEnviar);

        return new(ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar);
    }
}
