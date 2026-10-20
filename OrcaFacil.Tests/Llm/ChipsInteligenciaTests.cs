using System.Reflection;
using OrcaFacil.Core.Llm;
using OrcaFacil.Tests.Tema;

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
    public void DeepSeek_PreencheUrlModeloERotuloFlash()
    {
        var ajuste = ChipsInteligencia.Padrao(ProvedorIa.DeepSeek);

        Assert.Equal("DeepSeek", ChipsInteligencia.Texto(ProvedorIa.DeepSeek));
        Assert.Equal(ProvedorIa.DeepSeek, ChipsInteligencia.Interpretar("DeepSeek"));
        Assert.Equal("https://api.deepseek.com/anthropic", ajuste.Url);
        Assert.Equal(ChipsInteligencia.UrlDeepSeek, ajuste.Url);
        Assert.Equal("deepseek-flash", PedidoMensagem.ModeloDeepSeek);
        Assert.Equal(PedidoMensagem.ModeloDeepSeek, ajuste.Modelo);
        Assert.Equal(ChipsInteligencia.ModeloDeepSeek, ajuste.Modelo);
        Assert.Equal("DeepSeek Flash", ajuste.RotuloModelo);
        Assert.Equal("DeepSeek Flash", ChipsInteligencia.RotuloDeepSeek);
        Assert.Equal("DeepSeek Flash", ChipsInteligencia.RotuloDoModelo("deepseek-flash"));
        Assert.Equal("DeepSeek Flash", ChipsInteligencia.RotuloDoModelo("  deepseek-flash  "));
        Assert.Equal("AjusteChipIa", ajuste.ToString());
        Assert.DoesNotContain("api.deepseek.com", ajuste.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("deepseek-flash", ajuste.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("DeepSeek Flash", ajuste.ToString(), StringComparison.Ordinal);
        AssertSemCampoDeChave(ajuste);
    }

    [Fact]
    public void Chip_NaoAlteraOutroProvedor()
    {
        Assert.Equal(
            [ProvedorIa.Anthropic, ProvedorIa.Alibaba, ProvedorIa.DeepSeek],
            Enum.GetValues<ProvedorIa>());
        Assert.Equal(
            [ProvedorIa.DeepSeek, ProvedorIa.Anthropic, ProvedorIa.Alibaba],
            ChipsInteligencia.Provedores);
        Assert.Equal(ProvedorIa.Alibaba, ChipsInteligencia.Interpretar("Alibaba"));
        Assert.Equal("Alibaba", ChipsInteligencia.Texto(ProvedorIa.Alibaba));
        Assert.NotEqual(
            ChipsInteligencia.Padrao(ProvedorIa.DeepSeek),
            ChipsInteligencia.Padrao(ProvedorIa.Anthropic));
        Assert.NotEqual(
            ChipsInteligencia.Padrao(ProvedorIa.Anthropic),
            ChipsInteligencia.Padrao(ProvedorIa.Alibaba));
        Assert.NotEqual(
            ChipsInteligencia.Padrao(ProvedorIa.DeepSeek),
            ChipsInteligencia.Padrao(ProvedorIa.Alibaba));
    }

    [Fact]
    public void Painel_PlaceholderUsaUrlDeepSeek_SemUrlLiteral()
    {
        var painel = File.ReadAllText(CaminhoPainel());

        Assert.Contains("Placeholder=\"@ChipsInteligencia.UrlDeepSeek\"", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("https://api.anthropic.com", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("https://api.deepseek.com", painel, StringComparison.Ordinal);
        Assert.Contains("ChipsInteligencia.Efetiva(", painel, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData(null, "", "")]
    [InlineData(null, "   ", "   ")]
    public void SemConfiguracaoSalva_PadraoEfetivoEhDeepSeek(string? provedor, string? url, string? modelo)
    {
        var efetiva = ChipsInteligencia.Efetiva(ChipsInteligencia.Interpretar(provedor), url, modelo);
        var padrao = ChipsInteligencia.Padrao(ProvedorIa.DeepSeek);

        Assert.Equal(ProvedorIa.DeepSeek, efetiva.Provedor);
        Assert.Equal(padrao.Url, efetiva.Url);
        Assert.Equal(padrao.Modelo, efetiva.Modelo);
        Assert.Equal(ChipsInteligencia.UrlDeepSeek, efetiva.Url);
        Assert.Equal(PedidoMensagem.ModeloDeepSeek, efetiva.Modelo);
        Assert.True(MensagensLeituraComprovante.TemUrlModeloEChave(efetiva.Url, efetiva.Modelo, "chave"));
        Assert.Equal("ConfiguracaoIaEfetiva", efetiva.ToString());
        Assert.DoesNotContain("api.deepseek.com", efetiva.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ProvedorIa.Anthropic, "https://api.anthropic.com", "claude-haiku-5-5")]
    [InlineData(ProvedorIa.Alibaba, "", "")]
    [InlineData(ProvedorIa.Alibaba, "https://dashscope.aliyuncs.com", "qwen-vl-max")]
    public void PreferenciaSalva_NaoViraDeepSeek(ProvedorIa provedor, string url, string modelo)
    {
        var memoria = new PreferenciasEmMemoria();
        memoria.Definir(ChipsInteligencia.ChaveProvedor, ChipsInteligencia.Texto(provedor));
        memoria.Definir(ChipsInteligencia.ChaveUrl, url);
        memoria.Definir(ChipsInteligencia.ChaveModelo, modelo);

        var efetiva = ChipsInteligencia.Efetiva(
            ChipsInteligencia.Interpretar(memoria.Obter(ChipsInteligencia.ChaveProvedor, "")),
            memoria.Obter(ChipsInteligencia.ChaveUrl, ""),
            memoria.Obter(ChipsInteligencia.ChaveModelo, ""));

        Assert.Equal(provedor, efetiva.Provedor);
        Assert.Equal(url, efetiva.Url);
        Assert.Equal(modelo, efetiva.Modelo);
        Assert.NotEqual(ProvedorIa.DeepSeek, efetiva.Provedor);
        Assert.NotEqual(ChipsInteligencia.UrlDeepSeek, efetiva.Url);
        Assert.Equal(ChipsInteligencia.Texto(provedor), memoria.Obter(ChipsInteligencia.ChaveProvedor, ""));
        Assert.Equal(url, memoria.Obter(ChipsInteligencia.ChaveUrl, ""));
        Assert.Equal(modelo, memoria.Obter(ChipsInteligencia.ChaveModelo, ""));
    }

    [Theory]
    [InlineData("https://api.anthropic.com", "claude-haiku-5-5")]
    [InlineData("", "qwen-vl-max")]
    [InlineData("https://dashscope.aliyuncs.com", "")]
    public void ProvedorNuloComUrlOuModelo_NaoViraDeepSeek(string url, string modelo)
    {
        var memoria = new PreferenciasEmMemoria();
        memoria.Definir(ChipsInteligencia.ChaveUrl, url);
        memoria.Definir(ChipsInteligencia.ChaveModelo, modelo);

        var efetiva = ChipsInteligencia.Efetiva(
            ChipsInteligencia.Interpretar(memoria.Obter(ChipsInteligencia.ChaveProvedor, "")),
            memoria.Obter(ChipsInteligencia.ChaveUrl, ""),
            memoria.Obter(ChipsInteligencia.ChaveModelo, ""));

        Assert.Null(efetiva.Provedor);
        Assert.Equal(url, efetiva.Url);
        Assert.Equal(modelo, efetiva.Modelo);
        Assert.False(memoria.Contem(ChipsInteligencia.ChaveProvedor));
        Assert.Equal(url, memoria.Obter(ChipsInteligencia.ChaveUrl, "trocada"));
        Assert.Equal(modelo, memoria.Obter(ChipsInteligencia.ChaveModelo, "trocado"));
    }

    private static string CaminhoPainel()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var caminho = Path.Combine(
                dir.FullName,
                "OrcaFacil",
                "Components",
                "Pages",
                "Configuracoes",
                "InteligenciaArtificialPanel.razor");
            if (File.Exists(caminho))
                return caminho;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar InteligenciaArtificialPanel.razor.");
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
