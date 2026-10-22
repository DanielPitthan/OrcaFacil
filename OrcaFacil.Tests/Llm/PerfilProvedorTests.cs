using OrcaFacil.Core.Llm;

namespace OrcaFacil.Tests.Llm;

public class PerfilProvedorTests
{
    [Theory]
    [InlineData("https://api.deepseek.com/anthropic", ProvedorDetectado.DeepSeek, ModoThinking.Desligar)]
    [InlineData("https://api.deepseek.com.", ProvedorDetectado.DeepSeek, ModoThinking.Desligar)]
    [InlineData("https://api.deepseek.com./anthropic", ProvedorDetectado.DeepSeek, ModoThinking.Desligar)]
    [InlineData("https://foo.api.deepseek.com/anthropic", ProvedorDetectado.DeepSeek, ModoThinking.Desligar)]
    [InlineData("https://API.DeepSeek.Com./v1", ProvedorDetectado.DeepSeek, ModoThinking.Desligar)]
    [InlineData("https://api.deepseek.com", ProvedorDetectado.DeepSeek, ModoThinking.Desligar)]
    [InlineData("https://api.deepseek.com/", ProvedorDetectado.DeepSeek, ModoThinking.Desligar)]
    [InlineData("  https://api.deepseek.com/anthropic/  ", ProvedorDetectado.DeepSeek, ModoThinking.Desligar)]
    [InlineData("https://API.DEEPSEEK.COM/Anthropic", ProvedorDetectado.DeepSeek, ModoThinking.Desligar)]
    [InlineData("https://Api.DeepSeek.Com:8443/anthropic", ProvedorDetectado.DeepSeek, ModoThinking.Desligar)]
    [InlineData("https://api.anthropic.com", ProvedorDetectado.Anthropic, ModoThinking.NaoEnviar)]
    [InlineData("https://API.ANTHROPIC.COM/v1", ProvedorDetectado.Anthropic, ModoThinking.NaoEnviar)]
    [InlineData("https://dashscope.aliyuncs.com", ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar)]
    [InlineData("https://exemplo.test/api.deepseek.com", ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar)]
    [InlineData("https://api.deepseek.com.evil.com", ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar)]
    [InlineData("https://api.deepseek.com.evil.com/anthropic", ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar)]
    [InlineData("https://not-api.deepseek.com", ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar)]
    [InlineData(null, ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar)]
    [InlineData("", ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar)]
    [InlineData("   ", ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar)]
    [InlineData("nao-e-url", ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar)]
    [InlineData("http://api.deepseek.com", ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar)]
    [InlineData("https://user:segredo@api.deepseek.com", ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar)]
    [InlineData("https://api.deepseek.com?x=1", ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar)]
    [InlineData("https://api.deepseek.com#frag", ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar)]
    [InlineData("https://qwencloudapi.com.evil.com", ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar)]
    [InlineData("https://qwencloudapi.com.evil.com./apps/anthropic", ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar)]
    [InlineData("https://notqwencloudapi.com/apps/anthropic", ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar)]
    [InlineData("https://notqwencloudapi.com./apps/anthropic", ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar)]
    [InlineData("https://maas.qwencloudapi.com.evil.com/apps/anthropic", ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar)]
    public void DaUrl_ComparaOHostExato(string? url, ProvedorDetectado provedor, ModoThinking thinking)
    {
        var perfil = PerfilProvedor.DaUrl(url);

        Assert.Equal(new PerfilProvedor(provedor, thinking), perfil);
        Assert.Equal(provedor, perfil.Provedor);
        Assert.Equal(thinking, perfil.Thinking);
    }

    [Theory]
    [InlineData("https://token-plan.maas.qwencloudapi.com/apps/anthropic")]
    [InlineData("https://token-plan.maas.qwencloudapi.com./apps/anthropic")]
    [InlineData("https://qwencloudapi.com./apps/anthropic")]
    [InlineData("https://maas.qwencloudapi.com/apps/anthropic")]
    [InlineData("https://foo.bar.qwencloudapi.com")]
    [InlineData("https://QWENCLOUDAPI.COM/apps/anthropic")]
    [InlineData("https://Token-Plan.Maas.QwenCloudApi.Com:8443/apps/anthropic")]
    [InlineData("  https://token-plan.maas.qwencloudapi.com/apps/anthropic/  ")]
    public void DaUrl_SubdominioQwenCloud_DesligaThinking(string url)
    {
        var perfil = PerfilProvedor.DaUrl(url);

        Assert.Equal(ProvedorDetectado.QwenCloud, perfil.Provedor);
        Assert.Equal(ModoThinking.Desligar, perfil.Thinking);
        Assert.True(PerfilProvedor.EhQwenCloud(PedidoMensagem.CriarUri(url)!.Host));
    }

    [Fact]
    public void DaUrl_HttpDeepSeek_SoDesligaComPermiteHttp()
    {
        const string url = "http://api.deepseek.com/anthropic";

        Assert.Equal(
            new PerfilProvedor(ProvedorDetectado.Desconhecido, ModoThinking.NaoEnviar),
            PerfilProvedor.DaUrl(url));
        Assert.Equal(
            new PerfilProvedor(ProvedorDetectado.DeepSeek, ModoThinking.Desligar),
            PerfilProvedor.DaUrl(url, permiteHttp: true));
        Assert.Equal(
            new PerfilProvedor(ProvedorDetectado.DeepSeek, ModoThinking.Desligar),
            PerfilProvedor.DaUrl("http://foo.api.deepseek.com.", permiteHttp: true));
    }

    [Theory]
    [InlineData("token-plan.maas.qwencloudapi.com.", true)]
    [InlineData("token-plan.maas.qwencloudapi.com", true)]
    [InlineData("QWENCLOUDAPI.COM.", true)]
    [InlineData("foo.qwencloudapi.com.", true)]
    [InlineData("  maas.qwencloudapi.com.  ", true)]
    [InlineData("qwencloudapi.com.evil.com.", false)]
    [InlineData("notqwencloudapi.com.", false)]
    [InlineData("qwencloudapi.com.evil.com", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(".", false)]
    public void EhQwenCloud_PontoFinalCasaIgualAoHostSemPonto(string? host, bool casa)
    {
        Assert.Equal(casa, PerfilProvedor.EhQwenCloud(host));
    }
}
