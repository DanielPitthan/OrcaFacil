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
    public void Alibaba_PreencheUrlDoTokenPlanEModelo_SemJsonSchema()
    {
        var ajuste = ChipsInteligencia.Padrao(ProvedorIa.Alibaba);

        Assert.Equal("Alibaba", ChipsInteligencia.Texto(ProvedorIa.Alibaba));
        Assert.Equal(ProvedorIa.Alibaba, ChipsInteligencia.Interpretar("Alibaba"));
        Assert.Equal("https://token-plan.maas.qwencloudapi.com/apps/anthropic", ajuste.Url);
        Assert.Equal(ChipsInteligencia.UrlAlibaba, ajuste.Url);
        Assert.Equal("deepseek-v4.1-flash-1017", ajuste.Modelo);
        Assert.Equal(PedidoMensagem.ModeloAlibaba, ajuste.Modelo);
        Assert.Equal(ChipsInteligencia.ModeloAlibaba, ajuste.Modelo);
        Assert.Null(ajuste.RotuloModelo);
        Assert.Null(ChipsInteligencia.RotuloDoModelo(ajuste.Modelo));
        Assert.DoesNotContain("/v1/messages", ajuste.Url, StringComparison.Ordinal);
        Assert.NotEqual("deepseek-flash", ajuste.Modelo);
        Assert.NotEqual("DeepSeek Flash V4", ajuste.Modelo);
        Assert.NotEqual(ChipsInteligencia.UrlDeepSeek, ajuste.Url);
        Assert.Equal("AjusteChipIa", ajuste.ToString());
        Assert.DoesNotContain("qwencloudapi.com", ajuste.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("deepseek-v4.1-flash-1017", ajuste.ToString(), StringComparison.Ordinal);
        Assert.Contains("Token Plan (sk-sp-)", ChipsInteligencia.TextoAjudaAlibaba, StringComparison.Ordinal);
        Assert.DoesNotContain("Token Plan (sk-ws-)", ChipsInteligencia.TextoAjudaAlibaba, StringComparison.Ordinal);
        Assert.Contains(ChipsInteligencia.UrlAlibaba, ChipsInteligencia.TextoAjudaAlibaba, StringComparison.Ordinal);
        Assert.Contains("pay-as-you-go (sk-)", ChipsInteligencia.TextoAjudaAlibaba, StringComparison.Ordinal);
        Assert.Contains(ChipsInteligencia.UrlAlibabaAvulso, ChipsInteligencia.TextoAjudaAlibaba, StringComparison.Ordinal);
        Assert.Contains("qwen3.7-flash", ChipsInteligencia.TextoAjudaAlibaba, StringComparison.Ordinal);
        Assert.Contains("qwen3.7-plus", ChipsInteligencia.TextoAjudaAlibaba, StringComparison.Ordinal);
        Assert.Contains("qwen3-vl-plus", ChipsInteligencia.TextoAjudaAlibaba, StringComparison.Ordinal);
        Assert.DoesNotContain("output_config", ChipsInteligencia.TextoAjudaAlibaba, StringComparison.Ordinal);
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
            [ProvedorIa.Anthropic, ProvedorIa.Alibaba],
            ChipsInteligencia.Provedores);
        Assert.DoesNotContain(ProvedorIa.DeepSeek, ChipsInteligencia.Provedores);
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
    public void Painel_PlaceholderUsaUrlAlibaba_SemUrlLiteral()
    {
        var painel = File.ReadAllText(CaminhoPainel());

        Assert.Contains("Placeholder=\"@ChipsInteligencia.UrlAlibaba\"", painel, StringComparison.Ordinal);
        Assert.Contains("@ChipsInteligencia.TextoAjudaAlibaba", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("https://", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("DeepSeek", painel, StringComparison.Ordinal);
        Assert.Contains("ChipsInteligencia.Efetiva(", painel, StringComparison.Ordinal);
        Assert.Contains("ChipsInteligencia.Provedores", painel, StringComparison.Ordinal);

        var cliente = File.ReadAllText(CaminhoCore("ClienteMensagens.cs"));
        var pedido = File.ReadAllText(CaminhoCore("PedidoMensagem.cs"));
        Assert.DoesNotContain("qwencloudapi.com", cliente, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("qwencloudapi.com", pedido, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("output_config", pedido, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData(null, "", "")]
    [InlineData(null, "   ", "   ")]
    public void SemConfiguracaoSalva_PadraoEfetivoEhAlibaba(string? provedor, string? url, string? modelo)
    {
        var efetiva = ChipsInteligencia.Efetiva(ChipsInteligencia.Interpretar(provedor), url, modelo);
        var padrao = ChipsInteligencia.Padrao(ProvedorIa.Alibaba);

        Assert.Equal(ProvedorIa.Alibaba, efetiva.Provedor);
        Assert.Equal(padrao.Url, efetiva.Url);
        Assert.Equal(padrao.Modelo, efetiva.Modelo);
        Assert.Equal(ChipsInteligencia.UrlAlibaba, efetiva.Url);
        Assert.Equal("https://token-plan.maas.qwencloudapi.com/apps/anthropic", efetiva.Url);
        Assert.Equal(PedidoMensagem.ModeloAlibaba, efetiva.Modelo);
        Assert.Equal("deepseek-v4.1-flash-1017", efetiva.Modelo);
        Assert.NotEqual(ProvedorIa.DeepSeek, efetiva.Provedor);
        Assert.NotEqual(ChipsInteligencia.UrlDeepSeek, efetiva.Url);
        Assert.NotEqual(PedidoMensagem.ModeloDeepSeek, efetiva.Modelo);
        Assert.True(MensagensLeituraComprovante.TemUrlModeloEChave(efetiva.Url, efetiva.Modelo, "chave"));
        Assert.Equal("ConfiguracaoIaEfetiva", efetiva.ToString());
        Assert.DoesNotContain("qwencloudapi.com", efetiva.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("deepseek", efetiva.ToString(), StringComparison.OrdinalIgnoreCase);
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
        Assert.NotEqual(PedidoMensagem.ModeloDeepSeek, efetiva.Modelo);
        if (!string.Equals(url, ChipsInteligencia.UrlAlibaba, StringComparison.Ordinal))
            Assert.NotEqual(ChipsInteligencia.UrlAlibaba, efetiva.Url);
        if (!string.Equals(modelo, PedidoMensagem.ModeloAlibaba, StringComparison.Ordinal))
            Assert.NotEqual(PedidoMensagem.ModeloAlibaba, efetiva.Modelo);
        Assert.False(
            efetiva.Provedor == ProvedorIa.Alibaba &&
            efetiva.Url == ChipsInteligencia.UrlAlibaba &&
            efetiva.Modelo == PedidoMensagem.ModeloAlibaba &&
            (url != ChipsInteligencia.UrlAlibaba || modelo != PedidoMensagem.ModeloAlibaba));
        Assert.Equal(ChipsInteligencia.Texto(provedor), memoria.Obter(ChipsInteligencia.ChaveProvedor, ""));
        Assert.Equal(url, memoria.Obter(ChipsInteligencia.ChaveUrl, ""));
        Assert.Equal(modelo, memoria.Obter(ChipsInteligencia.ChaveModelo, ""));
    }

    [Theory]
    [InlineData("https://api.anthropic.com", "claude-haiku-5-5")]
    [InlineData("", "qwen-vl-max")]
    [InlineData("https://dashscope.aliyuncs.com", "")]
    [InlineData("https://token-plan.maas.qwencloudapi.com/apps/anthropic", "qwen3.7-plus")]
    [InlineData("https://maas.qwencloudapi.com/apps/anthropic", "deepseek-v4.1-flash-1017")]
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
        Assert.NotEqual(ProvedorIa.DeepSeek, efetiva.Provedor);
        Assert.NotEqual(ProvedorIa.Alibaba, efetiva.Provedor);
        if (!string.Equals(url, ChipsInteligencia.UrlAlibaba, StringComparison.Ordinal))
            Assert.NotEqual(ChipsInteligencia.UrlAlibaba, efetiva.Url);
        if (!string.Equals(modelo, PedidoMensagem.ModeloAlibaba, StringComparison.Ordinal))
            Assert.NotEqual(PedidoMensagem.ModeloAlibaba, efetiva.Modelo);
        Assert.NotEqual(ChipsInteligencia.UrlDeepSeek, efetiva.Url);
        Assert.False(memoria.Contem(ChipsInteligencia.ChaveProvedor));
        Assert.Equal(url, memoria.Obter(ChipsInteligencia.ChaveUrl, "trocada"));
        Assert.Equal(modelo, memoria.Obter(ChipsInteligencia.ChaveModelo, "trocado"));
    }

    [Fact]
    public void PreferenciaDeepSeekSalva_NaoViraAlibaba()
    {
        var memoria = new PreferenciasEmMemoria();
        memoria.Definir(ChipsInteligencia.ChaveProvedor, "DeepSeek");
        memoria.Definir(ChipsInteligencia.ChaveUrl, ChipsInteligencia.UrlDeepSeek);
        memoria.Definir(ChipsInteligencia.ChaveModelo, PedidoMensagem.ModeloDeepSeek);

        var efetiva = ChipsInteligencia.Efetiva(
            ChipsInteligencia.Interpretar(memoria.Obter(ChipsInteligencia.ChaveProvedor, "")),
            memoria.Obter(ChipsInteligencia.ChaveUrl, ""),
            memoria.Obter(ChipsInteligencia.ChaveModelo, ""));

        Assert.Equal(ProvedorIa.DeepSeek, efetiva.Provedor);
        Assert.Equal(ChipsInteligencia.UrlDeepSeek, efetiva.Url);
        Assert.Equal(PedidoMensagem.ModeloDeepSeek, efetiva.Modelo);
        Assert.NotEqual(ProvedorIa.Alibaba, efetiva.Provedor);
        Assert.NotEqual(ChipsInteligencia.UrlAlibaba, efetiva.Url);
        Assert.NotEqual(PedidoMensagem.ModeloAlibaba, efetiva.Modelo);
        Assert.Equal("DeepSeek", memoria.Obter(ChipsInteligencia.ChaveProvedor, ""));
    }

    private static string CaminhoPainel() =>
        Path.Combine(
            EncontrarRaiz(),
            "OrcaFacil",
            "Components",
            "Pages",
            "Configuracoes",
            "InteligenciaArtificialPanel.razor");

    private static string CaminhoCore(string arquivo) =>
        Path.Combine(EncontrarRaiz(), "OrcaFacil.Core", "Llm", arquivo);

    private static string EncontrarRaiz()
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
                return dir.FullName;

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
