using OrcaFacil.Core.Llm;

namespace OrcaFacil.Tests.Llm;

public class PerfilProvedorTests
{
    [Theory]
    [InlineData("https://api.deepseek.com/anthropic", ProvedorDetectado.DeepSeek, ModoThinking.Desligar)]
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
    public void DaUrl_ComparaOHostExato(string? url, ProvedorDetectado provedor, ModoThinking thinking)
    {
        var perfil = PerfilProvedor.DaUrl(url);

        Assert.Equal(new PerfilProvedor(provedor, thinking), perfil);
        Assert.Equal(provedor, perfil.Provedor);
        Assert.Equal(thinking, perfil.Thinking);
    }
}
