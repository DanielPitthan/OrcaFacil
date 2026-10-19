using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Llm;
using OrcaFacil.Tests.Tema;

namespace OrcaFacil.Tests.Llm;

public class MensagensLeituraComprovanteTests
{
    private const string Chave = "sk-ant-segredo-nao-pode-aparecer";

    [Fact]
    public void FormatoNaoConvertido_ExplicaQueALeituraNaoSaiu()
    {
        var exibicao = MensagensLeituraComprovante.FormatoNaoConvertido();

        Assert.Equal(SituacaoLeituraComprovante.Falha, exibicao.Situacao);
        Assert.Equal(MensagensLeituraComprovante.TextoFormatoNaoConvertido, exibicao.Mensagem);
        Assert.Contains("JPEG", exibicao.Mensagem, StringComparison.Ordinal);
        Assert.Contains("não foi enviada", exibicao.Mensagem, StringComparison.Ordinal);
        Assert.True(exibicao.PodeTentarDeNovo);
        Assert.False(exibicao.LevaParaInteligenciaArtificial);
        Assert.Null(exibicao.Diagnostico);
        Assert.Equal("FormatoNaoConvertido", FotoComprovantePronta.FormatoNaoConvertido().ToString());
    }

    [Fact]
    public void Carregando_TemAFraseDaEspera()
    {
        var exibicao = MensagensLeituraComprovante.Carregando();

        Assert.Equal(SituacaoLeituraComprovante.Carregando, exibicao.Situacao);
        Assert.Equal(MensagensLeituraComprovante.TextoCarregando, exibicao.Mensagem);
        Assert.Equal("Lendo o comprovante…", exibicao.Mensagem);
        Assert.False(exibicao.PodeTentarDeNovo);
        Assert.False(exibicao.LevaParaInteligenciaArtificial);
    }

    [Theory]
    [InlineData(null, "claude-haiku-5-5", "chave")]
    [InlineData("   ", "claude-haiku-5-5", "chave")]
    [InlineData("http://api.anthropic.com", "claude-haiku-5-5", "chave")]
    [InlineData("https://api.anthropic.com", null, "chave")]
    [InlineData("https://api.anthropic.com", "  ", "chave")]
    [InlineData("https://api.anthropic.com", "claude-haiku-5-5", null)]
    [InlineData("https://api.anthropic.com", "claude-haiku-5-5", "  ")]
    public void SemUrlModeloOuChave_LevaParaAAba(string? url, string? modelo, string? chave)
    {
        Assert.False(MensagensLeituraComprovante.TemUrlModeloEChave(url, modelo, chave));

        var exibicao = MensagensLeituraComprovante.SemConfiguracao();

        Assert.Equal(SituacaoLeituraComprovante.SemUrlModeloOuChave, exibicao.Situacao);
        Assert.Equal(MensagensLeituraComprovante.TextoSemUrlModeloOuChave, exibicao.Mensagem);
        Assert.Contains("Inteligência artificial", exibicao.Mensagem, StringComparison.Ordinal);
        Assert.True(exibicao.LevaParaInteligenciaArtificial);
        Assert.True(exibicao.PodeTentarDeNovo);
        Assert.DoesNotContain("R$", exibicao.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain(Chave, exibicao.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void SemRede_TempoEsgotado_E401_UsamAsFrasesDoCliente()
    {
        var semRede = Exibir(TipoResultadoMensagem.SemRede, ResultadoMensagem.TextoSemRede);
        var tempo = Exibir(TipoResultadoMensagem.Timeout, ResultadoMensagem.TextoTimeout);
        var recusada = Exibir(TipoResultadoMensagem.NaoAutorizado, ResultadoMensagem.TextoNaoAutorizado);

        Assert.Equal(SituacaoLeituraComprovante.SemRede, semRede.Situacao);
        Assert.Equal("Não foi possível conectar ao provedor.", semRede.Mensagem);
        Assert.Equal(SituacaoLeituraComprovante.TempoEsgotado, tempo.Situacao);
        Assert.Equal("A leitura demorou demais e foi interrompida.", tempo.Mensagem);
        Assert.Equal(SituacaoLeituraComprovante.NaoAutorizado, recusada.Situacao);
        Assert.Equal("A chave da API foi recusada.", recusada.Mensagem);
        Assert.DoesNotContain("401", recusada.Mensagem, StringComparison.Ordinal);
        Assert.All(new[] { semRede, tempo, recusada }, item =>
        {
            Assert.True(item.PodeTentarDeNovo);
            Assert.False(item.LevaParaInteligenciaArtificial);
            Assert.DoesNotContain("R$", item.Mensagem, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void SemPermissao403_TemFrasePropria_DiferenteDo401()
    {
        var recusada = Exibir(TipoResultadoMensagem.NaoAutorizado, "corpo 401 " + Chave);
        var semPermissao = Exibir(TipoResultadoMensagem.SemPermissao, "corpo 403 " + Chave);

        Assert.Equal(SituacaoLeituraComprovante.NaoAutorizado, recusada.Situacao);
        Assert.Equal(ResultadoMensagem.TextoNaoAutorizado, recusada.Mensagem);
        Assert.Equal(SituacaoLeituraComprovante.SemPermissao, semPermissao.Situacao);
        Assert.Equal(ResultadoMensagem.TextoSemPermissao, semPermissao.Mensagem);
        Assert.NotEqual(recusada.Mensagem, semPermissao.Mensagem);
        Assert.False(semPermissao.LevaParaInteligenciaArtificial);
        Assert.True(semPermissao.PodeTentarDeNovo);
        Assert.DoesNotContain("403", semPermissao.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("401", semPermissao.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain(Chave, semPermissao.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("corpo", semPermissao.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void FotoGrandeDemais_PedeOutra_ENaoLevaAChave()
    {
        var exibicao = MensagensLeituraComprovante.FotoGrandeDemais();

        Assert.Equal(MensagensLeituraComprovante.TextoFotoGrandeDemais, exibicao.Mensagem);
        Assert.Equal("A foto ficou grande demais. Escolha outra.", exibicao.Mensagem);
        Assert.True(exibicao.PodeTentarDeNovo);
        Assert.False(exibicao.LevaParaInteligenciaArtificial);
        Assert.DoesNotContain(Chave, exibicao.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void AbaInteligencia_UsaIndiceNomeado()
    {
        Assert.Equal(2, MensagensLeituraComprovante.IndiceAbaInteligencia);

        var razor = File.ReadAllText(Path.Combine(
            EncontrarRaiz(),
            "OrcaFacil",
            "Components",
            "Pages",
            "Configuracoes",
            "Configuracoes.razor"));

        Assert.Contains("MensagensLeituraComprovante.IndiceAbaInteligencia", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("_aba = 2", razor, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TipoResultadoMensagem.Limite, "O provedor limitou o uso agora. R$ 20 em 15 s")]
    [InlineData(TipoResultadoMensagem.Sobrecarga, "O provedor está sobrecarregado. Custa R$ 9,90")]
    public void Limite429E529_UsamAFraseSemValorEmReais(TipoResultadoMensagem tipo, string suja)
    {
        var resultado = ResultadoMensagem.Falha(tipo, suja, TimeSpan.FromSeconds(15));

        var exibicao = MensagensLeituraComprovante.De(resultado, null);

        Assert.Equal(SituacaoLeituraComprovante.LimiteProvedor, exibicao.Situacao);
        Assert.Equal("O provedor limitou o uso agora.", exibicao.Mensagem);
        Assert.Equal(ResultadoMensagem.TextoLimite, exibicao.Mensagem);
        Assert.DoesNotContain("R$", exibicao.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("15", exibicao.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("9,90", exibicao.Mensagem, StringComparison.Ordinal);
        Assert.True(exibicao.PodeTentarDeNovo);
        Assert.Equal($"ExibicaoLeitura({SituacaoLeituraComprovante.LimiteProvedor})", exibicao.ToString());
    }

    [Fact]
    public void RespostaCortada_MantemODiagnosticoSanitizado()
    {
        const string token = "sk-ant-ficticio-alibaba-9f3c2a7b1e";
        var diagnostico = DiagnosticoResposta.Criar(
            200,
            "https://dashscope.aliyuncs.com/v1/messages",
            "deepseek-v4-flash",
            nameof(TipoResultadoMensagem.RespostaInvalida),
            ResultadoMensagem.TextoRespostaCortada,
            ResultadoMensagem.TextoRespostaCortada,
            "max_tokens",
            null,
            "corpo " + token,
            false,
            [token]);
        var resultado = ResultadoMensagem.Falha(
            TipoResultadoMensagem.RespostaInvalida,
            ResultadoMensagem.TextoRespostaCortada,
            diagnostico: diagnostico);

        var exibicao = MensagensLeituraComprovante.De(resultado, null);

        Assert.Equal(SituacaoLeituraComprovante.RespostaForaDoSchema, exibicao.Situacao);
        Assert.Equal("resposta cortada", exibicao.Mensagem);
        Assert.Same(diagnostico, exibicao.Diagnostico);
        Assert.DoesNotContain(token, exibicao.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain(token, exibicao.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(token, exibicao.Diagnostico!.Texto, StringComparison.Ordinal);
        Assert.True(exibicao.PodeTentarDeNovo);
    }

    [Fact]
    public void RespostaForaDoSchema_TemAFraseFixa()
    {
        var exibicao = Exibir(TipoResultadoMensagem.RespostaInvalida, ResultadoMensagem.TextoRespostaInvalida + " " + Chave);

        Assert.Equal(SituacaoLeituraComprovante.RespostaForaDoSchema, exibicao.Situacao);
        Assert.Equal("A resposta veio fora do formato esperado.", exibicao.Mensagem);
        Assert.DoesNotContain(Chave, exibicao.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain(Chave, exibicao.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void OkSemComprovanteNormalizado_TambemERespostaForaDoSchema()
    {
        var lido = Cupom(legivel: true, 10m, Item("Arroz", 10m));

        var exibicao = MensagensLeituraComprovante.De(ResultadoMensagem.Ok(lido), null);

        Assert.Equal(SituacaoLeituraComprovante.RespostaForaDoSchema, exibicao.Situacao);
        Assert.Null(exibicao.Resumo);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    [InlineData(false, 0)]
    public void IlegivelOuSemItens_NaoViraResumo(bool legivel, int quantidade)
    {
        var itens = Enumerable.Range(0, quantidade)
            .Select(i => Item("Item " + i, 2m))
            .ToArray();
        var lido = Cupom(legivel, 2m, itens);
        var normalizado = NormalizadorComprovante.Normalizar(lido, Catalogo());

        var exibicao = MensagensLeituraComprovante.De(ResultadoMensagem.Ok(lido), normalizado);

        Assert.Equal(SituacaoLeituraComprovante.IlegivelOuSemItens, exibicao.Situacao);
        Assert.Equal("O comprovante está ilegível ou não tem itens.", exibicao.Mensagem);
        Assert.Null(exibicao.Resumo);
        Assert.True(exibicao.PodeTentarDeNovo);
        Assert.DoesNotContain("R$", exibicao.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void ModeloSemVisao_SugereTrocarOModelo()
    {
        var exibicao = Exibir(TipoResultadoMensagem.ModeloSemVisao, "modelo " + Chave + " image content");

        Assert.Equal(SituacaoLeituraComprovante.ModeloSemVisao, exibicao.Situacao);
        Assert.Equal(ResultadoMensagem.TextoModeloSemVisao, exibicao.Mensagem);
        Assert.Contains("Troque o modelo", exibicao.Mensagem, StringComparison.Ordinal);
        Assert.Contains("Inteligência artificial", exibicao.Mensagem, StringComparison.Ordinal);
        Assert.True(exibicao.LevaParaInteligenciaArtificial);
        Assert.True(exibicao.PodeTentarDeNovo);
        Assert.DoesNotContain(Chave, exibicao.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("content blocks", exibicao.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Pronta_ResumeItensTotalEQueAPreviaVemASeguir()
    {
        var lido = Cupom(legivel: true, 12.5m, Item("Arroz", 10m), Item("Feijão", 2.5m));
        var normalizado = NormalizadorComprovante.Normalizar(lido, Catalogo());

        var exibicao = MensagensLeituraComprovante.De(ResultadoMensagem.Ok(lido), normalizado);

        Assert.Equal(SituacaoLeituraComprovante.Pronta, exibicao.Situacao);
        Assert.Equal(MensagensLeituraComprovante.Resumo(normalizado), exibicao.Resumo);
        Assert.Contains("2 itens", exibicao.Resumo, StringComparison.Ordinal);
        Assert.Contains("R$ 12,50", exibicao.Resumo, StringComparison.Ordinal);
        Assert.Contains("A prévia editável vem a seguir.", exibicao.Resumo, StringComparison.Ordinal);
        Assert.DoesNotContain(Chave, exibicao.Resumo, StringComparison.Ordinal);
        Assert.False(exibicao.PodeTentarDeNovo);
        Assert.NotNull(normalizado);
        Assert.Equal(2, normalizado.Linhas.Count);
    }

    [Fact]
    public void Pronta_ComUmItem_UsaOSingular()
    {
        var lido = Cupom(legivel: true, 4m, Item("Leite", 4m));
        var normalizado = NormalizadorComprovante.Normalizar(lido, Catalogo());

        Assert.Contains("1 item.", MensagensLeituraComprovante.Resumo(normalizado), StringComparison.Ordinal);
    }

    [Fact]
    public void Pronta_TotalDivergente_MostraCupomESoma()
    {
        var lido = Cupom(legivel: true, 15m, Item("Arroz", 10m));
        var normalizado = NormalizadorComprovante.Normalizar(lido, Catalogo());

        var resumo = MensagensLeituraComprovante.Resumo(normalizado);

        Assert.Contains("Total do cupom R$ 15,00", resumo, StringComparison.Ordinal);
        Assert.Contains("Soma das linhas R$ 10,00", resumo, StringComparison.Ordinal);
        Assert.Contains(MensagensLeituraComprovante.TextoPreviaASeguir, resumo, StringComparison.Ordinal);
    }

    [Fact]
    public void AvisoDePrivacidade_FicaEmPreferences_SemAChave()
    {
        var preferencias = new PreferenciasEmMemoria();

        Assert.False(AvisoPrivacidadeComprovante.JaVisto(preferencias));
        Assert.DoesNotContain(AvisoPrivacidadeComprovante.ChavePreferencia, ChipsInteligencia.ChavesPreferencia);
        Assert.DoesNotContain("Chave", AvisoPrivacidadeComprovante.ChavePreferencia, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Chave, MensagensLeituraComprovante.TextoAvisoPrivacidade, StringComparison.Ordinal);
        Assert.Equal("visto", AvisoPrivacidadeComprovante.ValorVisto);

        AvisoPrivacidadeComprovante.MarcarVisto(preferencias);

        Assert.True(AvisoPrivacidadeComprovante.JaVisto(preferencias));
        Assert.Equal("visto", preferencias.ValorOuNulo(AvisoPrivacidadeComprovante.ChavePreferencia));
        Assert.DoesNotContain(Chave, preferencias.ValorOuNulo(AvisoPrivacidadeComprovante.ChavePreferencia)!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("The model does not support image content blocks.")]
    [InlineData("Image content blocks are not supported by this model.")]
    [InlineData("current model does not support multimodal")]
    [InlineData("o modelo não suporta imagem")]
    public void Diagnostico_ReconheceModeloSemVisao_SemDevolverOCorpo(string corpo)
    {
        Assert.True(DiagnosticoErroProvedor.IndicaModeloSemVisao(corpo + " " + Chave));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("corpo-secreto sk-ant-segredo-nao-pode-aparecer dGVzdGU=")]
    [InlineData("bad request")]
    [InlineData("invalid image size")]
    public void Diagnostico_NaoConfundeOutroErroComFaltaDeVisao(string? corpo)
    {
        Assert.False(DiagnosticoErroProvedor.IndicaModeloSemVisao(corpo));
    }

    private static string EncontrarRaiz()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null)
        {
            if (File.Exists(Path.Combine(diretorio.FullName, "OrcaFacil.slnx")))
                return diretorio.FullName;

            diretorio = diretorio.Parent;
        }

        throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }

    private static ExibicaoLeituraComprovante Exibir(TipoResultadoMensagem tipo, string mensagem) =>
        MensagensLeituraComprovante.De(ResultadoMensagem.Falha(tipo, mensagem), null);

    private static ComprovanteLido Cupom(bool legivel, decimal? total, params ItemComprovanteLido[] itens) =>
        new("Mercado", new DateOnly(2026, 10, 19), total, legivel, itens);

    private static ItemComprovanteLido Item(string descricao, decimal preco) =>
        new(descricao, "", false, preco, null, null, null, null);

    private static List<ProdutoCesta> Catalogo() => [];
}
