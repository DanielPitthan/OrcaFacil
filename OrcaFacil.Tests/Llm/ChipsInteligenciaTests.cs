using System.Reflection;
using OrcaFacil.Core.Llm;

namespace OrcaFacil.Tests.Llm;

public class ChipsInteligenciaTests
{
    [Fact]
    public void ChavesDePreferencia_SaoUrlModeloProvedorEMaxTokens_SemAChaveDaApi()
    {
        Assert.Equal("Config.Ia.Url", ChipsInteligencia.ChaveUrl);
        Assert.Equal("Config.Ia.Modelo", ChipsInteligencia.ChaveModelo);
        Assert.Equal("Config.Ia.Provedor", ChipsInteligencia.ChaveProvedor);
        Assert.Equal("Config.Ia.MaxTokens", ChipsInteligencia.ChaveMaxTokens);
        Assert.Equal(
            [
                ChipsInteligencia.ChaveUrl,
                ChipsInteligencia.ChaveModelo,
                ChipsInteligencia.ChaveProvedor,
                ChipsInteligencia.ChaveMaxTokens
            ],
            ChipsInteligencia.ChavesPreferencia);

        foreach (var chave in ChipsInteligencia.ChavesPreferencia)
        {
            Assert.DoesNotContain("chave", chave, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("api-key", chave, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", chave, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DeepSeek", chave, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Anthropic_PreencheUrlModeloERotuloHaiku()
    {
        var ajuste = ChipsInteligencia.Padrao(ProvedorIa.Anthropic);

        Assert.Equal("Anthropic", ChipsInteligencia.Texto(ProvedorIa.Anthropic));
        Assert.Equal(ProvedorIa.Anthropic, ChipsInteligencia.Interpretar("Anthropic"));
        Assert.Equal("https://api.anthropic.com", ajuste.Url);
        Assert.Equal(ChipsInteligencia.UrlAnthropic, ajuste.Url);
        Assert.Equal("claude-haiku-5-5", ajuste.Modelo);
        Assert.Equal(PedidoMensagem.ModeloAnthropic, ajuste.Modelo);
        Assert.Equal("Haiku 5.5", ajuste.RotuloModelo);
        Assert.Equal("Haiku 5.5", ChipsInteligencia.RotuloDoModelo(ajuste.Modelo));
        Assert.Equal("AjusteChipIa", ajuste.ToString());
        Assert.DoesNotContain("api.anthropic.com", ajuste.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("claude-haiku-5-5", ajuste.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Haiku", ajuste.ToString(), StringComparison.Ordinal);
        AssertSemCampoDeChave(ajuste);
    }

    [Fact]
    public void Alibaba_DeixaUrlEditavelEModeloLivre_SemDeepSeekFlash()
    {
        var ajuste = ChipsInteligencia.Padrao(ProvedorIa.Alibaba);

        Assert.Equal("Alibaba", ChipsInteligencia.Texto(ProvedorIa.Alibaba));
        Assert.Equal(ProvedorIa.Alibaba, ChipsInteligencia.Interpretar("Alibaba"));
        Assert.Null(ajuste.Url);
        Assert.Null(ajuste.Modelo);
        Assert.Null(ajuste.RotuloModelo);
        Assert.Null(ChipsInteligencia.RotuloDoModelo(ajuste.Modelo));
        Assert.NotEqual("DeepSeek Flash V4", ajuste.Modelo);
        Assert.NotEqual("DeepSeek Flash V4", ajuste.Url);
        Assert.NotEqual("DeepSeek Flash V4", ajuste.RotuloModelo);
        Assert.DoesNotContain("DeepSeek", ajuste.ToString(), StringComparison.Ordinal);
        AssertSemCampoDeChave(ajuste);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("anthropic")]
    [InlineData("DeepSeek Flash V4")]
    public void Interpretar_TextoDesconhecido_NaoViraProvedor(string? texto)
    {
        Assert.Null(ChipsInteligencia.Interpretar(texto));
    }

    [Fact]
    public void Chip_NaoAlteraOutroProvedor()
    {
        Assert.Equal([ProvedorIa.Anthropic, ProvedorIa.Alibaba], ChipsInteligencia.Provedores);
        Assert.NotEqual(
            ChipsInteligencia.Padrao(ProvedorIa.Anthropic),
            ChipsInteligencia.Padrao(ProvedorIa.Alibaba));
    }

    private static void AssertSemCampoDeChave(AjusteChipIa ajuste)
    {
        var nomes = ajuste.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(p => p.Name)
            .OrderBy(nome => nome, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(["Modelo", "RotuloModelo", "Url"], nomes);
        Assert.DoesNotContain(nomes, nome => nome.Contains("Chave", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(nomes, nome => nome.Contains("Key", StringComparison.OrdinalIgnoreCase));
    }
}
