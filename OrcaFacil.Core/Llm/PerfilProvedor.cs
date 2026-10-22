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
/// A tabela abaixo é a regra: ápice, subdomínio e o ponto final do FQDN.
/// Anthropic compara o host exato. Proxy ou host desconhecido não entra.
/// A comparação ignora maiúsculas. O thinking do teste de conexão segue o host, não o chip.
/// O pedido com ferramenta forçada desliga o thinking à parte, em <see cref="PedidoMensagem"/>.
/// A URL do pedido é a que o app gravou; este tipo não escolhe endereço.
/// </summary>
public sealed record PerfilProvedor(ProvedorDetectado Provedor, ModoThinking Thinking)
{
    private const string HostDeepSeek = "api.deepseek.com";
    private const string HostAnthropic = "api.anthropic.com";
    private const string HostQwenCloud = "qwencloudapi.com";

    /// <summary>Host, provedor, thinking e se qualquer subdomínio entra.</summary>
    private static readonly (string Host, ProvedorDetectado Provedor, ModoThinking Thinking, bool Subdominio)[] Hosts =
    [
        (HostDeepSeek, ProvedorDetectado.DeepSeek, ModoThinking.Desligar, true),
        (HostQwenCloud, ProvedorDetectado.QwenCloud, ModoThinking.Desligar, true),
        (HostAnthropic, ProvedorDetectado.Anthropic, ModoThinking.NaoEnviar, false),
    ];

    public static PerfilProvedor DaUrl(string? urlBase) => DaUrl(urlBase, permiteHttp: false);

    /// <summary>
    /// <paramref name="permiteHttp"/> só abre http no teste. Sem ele, http não é DeepSeek
    /// e o thinking do perfil continua de fora.
    /// </summary>
    public static PerfilProvedor DaUrl(string? urlBase, bool permiteHttp)
    {
        var uri = PedidoMensagem.CriarUri(urlBase, permiteHttp);
        if (uri is null)
            return new(ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar);

        foreach (var (host, provedor, thinking, subdominio) in Hosts)
        {
            if (CasaHost(uri.Host, host, subdominio))
                return new(provedor, thinking);
        }

        return new(ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar);
    }

    /// <summary>
    /// Único critério de host da QwenCloud. Casa o ápice e qualquer subdomínio.
    /// O ponto final de um FQDN (<c>token-plan.maas.qwencloudapi.com.</c>) não muda o nome.
    /// Um host que só contém o texto (qwencloudapi.com.evil.com, notqwencloudapi.com) fica de fora.
    /// </summary>
    public static bool EhQwenCloud(string? host) => CasaHost(host, HostQwenCloud, subdominio: true);

    private static bool CasaHost(string? host, string esperado, bool subdominio)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        var nome = host.Trim().TrimEnd('.');
        return nome.Equals(esperado, StringComparison.OrdinalIgnoreCase)
            || (subdominio && nome.EndsWith("." + esperado, StringComparison.OrdinalIgnoreCase));
    }
}
