using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Llm;
using OrcaFacil.Tests.Tema;

namespace OrcaFacil.Tests.Llm;

public class MensagensLeituraComprovanteTests
{
    private const string Chave = "sk-ant-segredo-nao-pode-aparecer";

    [Fact]
    public void FotoAcimaDoTeto_ExplicaQueALeituraNaoSaiu()
    {
        var exibicao = MensagensLeituraComprovante.FotoAcimaDoTeto();

        Assert.Equal(MensagensLeituraComprovante.TextoFotoAcimaDoTeto, exibicao.Mensagem);
        Assert.Contains("20 MB", exibicao.Mensagem, StringComparison.Ordinal);
        Assert.Contains("não foi enviada", exibicao.Mensagem, StringComparison.Ordinal);
        Assert.True(exibicao.PodeTentarDeNovo);
        Assert.Null(exibicao.Diagnostico);
    }

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

    [Theory]
    [InlineData(TipoResultadoMensagem.NaoAutorizado)]
    [InlineData(TipoResultadoMensagem.PedidoInvalido)]
    public void ChaveAusente_MostraInformeAChave_ENaoARecusa(TipoResultadoMensagem tipo)
    {
        var resultado = ResultadoMensagem.Falha(tipo, ResultadoMensagem.TextoChaveNaoInformada);

        var exibicao = MensagensLeituraComprovante.De(resultado, null);

        Assert.Equal(ResultadoMensagem.TextoChaveNaoInformada, exibicao.Mensagem);
        Assert.Equal("Informe a chave da API.", exibicao.Mensagem);
        Assert.NotEqual(ResultadoMensagem.TextoNaoAutorizado, exibicao.Mensagem);
        Assert.NotEqual(MensagensLeituraComprovante.TextoSemUrlModeloOuChave, exibicao.Mensagem);
        Assert.Equal(SituacaoLeituraComprovante.SemUrlModeloOuChave, exibicao.Situacao);
        Assert.True(exibicao.LevaParaInteligenciaArtificial);
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
    public void AbaInteligencia_SegueATabela_ENaoUmIndiceFixo()
    {
        var abas = MensagensLeituraComprovante.Abas;

        Assert.Equal(
            ["notificacoes", "armazenamento", MensagensLeituraComprovante.ConsultaAba],
            abas.Select(aba => aba.Consulta).ToArray());
        Assert.Equal(
            ["Notificações", "Armazenamento", "Inteligência artificial"],
            abas.Select(aba => aba.Titulo).ToArray());
        Assert.Equal(abas.Count - 1, MensagensLeituraComprovante.IndiceDoAtalho("ia"));
        Assert.Equal(MensagensLeituraComprovante.IndiceDoAtalho("ia"), MensagensLeituraComprovante.IndiceDoAtalho("IA"));
        Assert.Null(MensagensLeituraComprovante.IndiceDoAtalho("notificacoes"));
        Assert.Null(MensagensLeituraComprovante.IndiceDoAtalho("armazenamento"));
        Assert.Null(MensagensLeituraComprovante.IndiceDoAtalho(null));
        Assert.Null(MensagensLeituraComprovante.IndiceDoAtalho("   "));
        Assert.Equal(-1, MensagensLeituraComprovante.IndiceNaTabela(abas, "inexistente"));

        var iaPrimeiro = new AbaConfiguracao[]
        {
            new(MensagensLeituraComprovante.ConsultaAba, "Inteligência artificial"),
            new("notificacoes", "Notificações"),
            new("armazenamento", "Armazenamento")
        };
        var iaNoMeio = new AbaConfiguracao[]
        {
            new("notificacoes", "Notificações"),
            new(MensagensLeituraComprovante.ConsultaAba, "Inteligência artificial"),
            new("armazenamento", "Armazenamento")
        };

        Assert.Equal(0, MensagensLeituraComprovante.IndiceDoAtalho(iaPrimeiro, "ia"));
        Assert.Equal(1, MensagensLeituraComprovante.IndiceDoAtalho(iaNoMeio, "IA"));
        Assert.Null(MensagensLeituraComprovante.IndiceDoAtalho(iaPrimeiro, "notificacoes"));
        Assert.Null(MensagensLeituraComprovante.IndiceDoAtalho([], "ia"));

        var raiz = EncontrarRaiz();
        var razor = File.ReadAllText(Path.Combine(
            raiz,
            "OrcaFacil",
            "Components",
            "Pages",
            "Configuracoes",
            "Configuracoes.razor"));
        var mensagens = File.ReadAllText(Path.Combine(
            raiz,
            "OrcaFacil.Core",
            "Llm",
            "MensagensLeituraComprovante.cs"));

        Assert.Equal(abas.Select(aba => aba.Titulo).ToArray(), TitulosDeAbas(razor));
        Assert.Contains("MensagensLeituraComprovante.IndiceDoAtalho(AbaConsulta)", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("IndiceAbaInteligencia", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("IndiceAbaInteligencia", mensagens, StringComparison.Ordinal);
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
        Assert.Equal("A resposta foi cortada por limite de tokens.", exibicao.Mensagem);
        Assert.Contains("A resposta foi cortada por limite de tokens.", exibicao.Diagnostico!.Texto, StringComparison.Ordinal);
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
        Assert.Equal("visto", AvisoPrivacidadeComprovante.ValorVisto);
        Assert.DoesNotContain(Chave, MensagensLeituraComprovante.TextoAvisoPrivacidade, StringComparison.Ordinal);
        Assert.DoesNotContain("Chave", AvisoPrivacidadeComprovante.ChaveAvisoProvedorExterno, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(AvisoPrivacidadeComprovante.ChaveAvisoProvedorExterno, ChipsInteligencia.ChavesPreferencia);
        Assert.DoesNotContain(AvisoPrivacidadeComprovante.ChavePreferencia, ChipsInteligencia.ChavesPreferencia);
        Assert.DoesNotContain(AvisoPrivacidadeComprovante.ChavePreferenciaDeepSeek, ChipsInteligencia.ChavesPreferencia);

        AvisoPrivacidadeComprovante.MarcarVisto(preferencias);

        Assert.True(AvisoPrivacidadeComprovante.JaVisto(preferencias));
        Assert.Equal("visto", preferencias.ValorOuNulo(AvisoPrivacidadeComprovante.ChaveAvisoProvedorExterno));
        Assert.DoesNotContain(Chave, preferencias.ValorOuNulo(AvisoPrivacidadeComprovante.ChaveAvisoProvedorExterno)!, StringComparison.Ordinal);
    }

    [Fact]
    public void TextoAvisoPrivacidade_EOmesmoEmQualquerCaso()
    {
        const string aviso = "Aviso, seus dados serão enviados para um provedor externo.";

        Assert.Equal(aviso, MensagensLeituraComprovante.TextoAvisoPrivacidade);
        Assert.Equal(aviso, AvisoPrivacidadeComprovante.TextoAvisoPrivacidade());
        Assert.DoesNotContain("Como não há URL gravada", aviso, StringComparison.Ordinal);
        Assert.DoesNotContain("China", aviso, StringComparison.Ordinal);
        Assert.DoesNotContain("Alibaba", aviso, StringComparison.Ordinal);
        Assert.DoesNotContain("DeepSeek", aviso, StringComparison.Ordinal);
        Assert.DoesNotContain("provedor da URL configurada", aviso, StringComparison.Ordinal);
        Assert.DoesNotContain(Chave, aviso, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", aviso, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("x-api-key", aviso, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChaveNova_NaoReaproveitaAsAnteriores()
    {
        var preferencias = new PreferenciasEmMemoria();

        Assert.Equal("Cesta.Leitura.AvisoPrivacidade", AvisoPrivacidadeComprovante.ChavePreferencia);
        Assert.Equal(
            AvisoPrivacidadeComprovante.ChavePreferencia + "DeepSeek",
            AvisoPrivacidadeComprovante.ChavePreferenciaDeepSeek);
        Assert.Equal("Cesta.Leitura.AvisoPrivacidadeDeepSeek", AvisoPrivacidadeComprovante.ChavePreferenciaDeepSeek);
        Assert.Equal("Cesta.Leitura.AvisoProvedorExterno", AvisoPrivacidadeComprovante.ChaveAvisoProvedorExterno);
        Assert.NotEqual(AvisoPrivacidadeComprovante.ChavePreferencia, AvisoPrivacidadeComprovante.ChaveAvisoProvedorExterno);
        Assert.NotEqual(AvisoPrivacidadeComprovante.ChavePreferenciaDeepSeek, AvisoPrivacidadeComprovante.ChaveAvisoProvedorExterno);

        preferencias.Definir(AvisoPrivacidadeComprovante.ChavePreferencia, AvisoPrivacidadeComprovante.ValorVisto);
        preferencias.Definir(AvisoPrivacidadeComprovante.ChavePreferenciaDeepSeek, AvisoPrivacidadeComprovante.ValorVisto);

        Assert.False(AvisoPrivacidadeComprovante.JaVisto(preferencias));
        Assert.False(preferencias.Contem(AvisoPrivacidadeComprovante.ChaveAvisoProvedorExterno));
    }

    [Fact]
    public void MarcarVisto_GravaSoAChaveNova()
    {
        var preferencias = new PreferenciasEmMemoria();

        AvisoPrivacidadeComprovante.MarcarVisto(preferencias);

        Assert.True(AvisoPrivacidadeComprovante.JaVisto(preferencias));
        Assert.Equal("visto", preferencias.ValorOuNulo(AvisoPrivacidadeComprovante.ChaveAvisoProvedorExterno));
        Assert.False(preferencias.Contem(AvisoPrivacidadeComprovante.ChavePreferencia));
        Assert.False(preferencias.Contem(AvisoPrivacidadeComprovante.ChavePreferenciaDeepSeek));
        Assert.DoesNotContain(Chave, preferencias.ValorOuNulo(AvisoPrivacidadeComprovante.ChaveAvisoProvedorExterno)!, StringComparison.Ordinal);
    }

    [Fact]
    public void RazorDoAviso_UsaOMetodo_SemOTextoLiteral()
    {
        var raiz = EncontrarRaiz();
        var dialog = File.ReadAllText(Path.Combine(
            raiz, "OrcaFacil", "Components", "Pages", "Cesta", "AvisoPrivacidadeComprovanteDialog.razor"));
        var pagina = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Pages", "Cesta", "CestaPage.razor"));
        var confirmar = ExtrairBloco(pagina, "Task<bool> ConfirmarAvisoAsync()");
        var aviso = File.ReadAllText(Path.Combine(raiz, "OrcaFacil.Core", "Llm", "AvisoPrivacidadeComprovante.cs"));
        var mensagens = File.ReadAllText(Path.Combine(raiz, "OrcaFacil.Core", "Llm", "MensagensLeituraComprovante.cs"));
        const string texto = "Aviso, seus dados serão enviados para um provedor externo.";

        Assert.Contains("AvisoPrivacidadeComprovante.TextoAvisoPrivacidade()", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain(texto, dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("sede na China", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("Alibaba", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("DeepSeek", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("Como não há URL gravada", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("provedor da URL configurada", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("SemUrlGravada", dialog, StringComparison.Ordinal);
        Assert.Contains("TextoAvisoPrivacidade() =>", aviso, StringComparison.Ordinal);
        Assert.Contains("MensagensLeituraComprovante.TextoAvisoPrivacidade;", aviso, StringComparison.Ordinal);
        Assert.DoesNotContain("ComplementoPrivacidade", aviso, StringComparison.Ordinal);
        Assert.DoesNotContain("semUrlGravada", aviso, StringComparison.Ordinal);
        Assert.DoesNotContain("ProvedorDetectado", aviso, StringComparison.Ordinal);
        Assert.DoesNotContain("Alibaba", aviso, StringComparison.Ordinal);
        Assert.DoesNotContain("AvisoPrivacidadeAlibaba", aviso, StringComparison.Ordinal);
        Assert.DoesNotContain("Como não há URL gravada", aviso, StringComparison.Ordinal);
        Assert.Contains(texto, mensagens, StringComparison.Ordinal);
        Assert.DoesNotContain("ComplementoPrivacidade", mensagens, StringComparison.Ordinal);
        Assert.DoesNotContain("Como não há URL gravada", mensagens, StringComparison.Ordinal);
        Assert.DoesNotContain("sede na China", mensagens, StringComparison.Ordinal);
        Assert.Contains("AvisoPrivacidadeComprovante.JaVisto(Preferencias)", confirmar, StringComparison.Ordinal);
        Assert.Contains("AvisoPrivacidadeComprovante.MarcarVisto(Preferencias)", confirmar, StringComparison.Ordinal);
        Assert.DoesNotContain("JaVisto(Preferencias,", confirmar, StringComparison.Ordinal);
        Assert.DoesNotContain("MarcarVisto(Preferencias,", confirmar, StringComparison.Ordinal);
        Assert.DoesNotContain("ChavePreferencia", confirmar, StringComparison.Ordinal);
        Assert.DoesNotContain("AvisoProvedorExterno", confirmar, StringComparison.Ordinal);
        Assert.DoesNotContain("semUrlGravada", confirmar, StringComparison.Ordinal);
        Assert.DoesNotContain("DaUrl", confirmar, StringComparison.Ordinal);
        Assert.DoesNotContain("Efetiva(", confirmar, StringComparison.Ordinal);
        Assert.DoesNotContain(texto, confirmar, StringComparison.Ordinal);
        Assert.DoesNotContain("Como não há URL gravada", confirmar, StringComparison.Ordinal);
        Assert.DoesNotContain("Alibaba", confirmar, StringComparison.Ordinal);
        Assert.DoesNotContain("sede na China", confirmar, StringComparison.Ordinal);
        Assert.DoesNotContain(".Host", confirmar, StringComparison.Ordinal);
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

    private static string[] TitulosDeAbas(string razor)
    {
        const string marca = "<MudTabPanel Text=\"";
        var titulos = new List<string>();
        var indice = 0;
        while (true)
        {
            indice = razor.IndexOf(marca, indice, StringComparison.Ordinal);
            if (indice < 0)
                return titulos.ToArray();

            var inicio = indice + marca.Length;
            var fim = razor.IndexOf('"', inicio);
            Assert.True(fim > inicio);
            titulos.Add(razor[inicio..fim]);
            indice = fim;
        }
    }

    private static string ExtrairBloco(string fonte, string assinatura)
    {
        var indice = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(indice >= 0, assinatura);
        var abre = fonte.IndexOf('{', indice);
        var profundidade = 0;
        for (var i = abre; i < fonte.Length; i++)
        {
            if (fonte[i] == '{')
                profundidade++;
            else if (fonte[i] == '}')
            {
                profundidade--;
                if (profundidade == 0)
                    return fonte[abre..(i + 1)];
            }
        }

        throw new InvalidOperationException("Bloco sem fechamento.");
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
