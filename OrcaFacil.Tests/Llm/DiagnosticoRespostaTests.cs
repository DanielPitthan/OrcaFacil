using System.Text;
using OrcaFacil.Core.Llm;

namespace OrcaFacil.Tests.Llm;

public class DiagnosticoRespostaTests
{
    private const string Token = "sk-ant-ficticio-alibaba-9f3c2a7b1e";

    [Fact]
    public void TextoELog_NaoMostramTokenCabecalhoImagemNemCaminho()
    {
        var base64 = new string('A', 220) + new string('b', 20);
        var corpo = string.Join(
            '\n',
            "erro do provedor",
            "Authorization: Bearer " + Token,
            "x-api-key: " + Token,
            "anthropic-version: 2023-06-01",
            "token solto " + Token,
            "data:image/jpeg;base64," + base64,
            "resto " + new string('C', 180));

        var diagnostico = DiagnosticoResposta.Criar(
            200,
            "https://usuario-secreto:" + Token + "@dashscope.aliyuncs.com:8443/compatible-mode/v1/messages?token=" + Token + "#frag",
            "deepseek-v4-flash",
            "RespostaInvalida",
            "A resposta veio fora do formato esperado.",
            "campo ausente: itens[0].preco",
            "end_turn",
            "stop",
            corpo,
            corpoIncompleto: false,
            [Token, base64]);

        var log = new StringBuilder();
        RegistroDiagnosticoLeitura.Registrar(diagnostico, texto => log.Append(texto));

        Assert.Equal(diagnostico.Texto, log.ToString());
        Assert.Equal("https://dashscope.aliyuncs.com:8443", diagnostico.Origem);
        Assert.Contains("Status HTTP: 200", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("Endpoint: https://dashscope.aliyuncs.com:8443", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("Modelo: deepseek-v4-flash", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("Tipo: RespostaInvalida", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("Mensagem: A resposta veio fora do formato esperado.", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("Motivo: campo ausente: itens[0].preco", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("stop_reason: end_turn", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("finish_reason: stop", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("erro do provedor", diagnostico.Texto, StringComparison.Ordinal);
        AssertNaoVaza(diagnostico.Texto);
        AssertNaoVaza(log.ToString());
        Assert.DoesNotContain(base64, diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("usuario-secreto", diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("/compatible-mode", diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("?token=", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Equal("DiagnosticoResposta", diagnostico.ToString());
        Assert.DoesNotContain(Token, diagnostico.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void CorpoEscapado_NaoVazaTokenForaDaListaNemBase64Curto()
    {
        const string token = "sk-outro-nao-listado-9f3c";
        var base64 = new string('Q', 40);
        var corpo = "{\\\"x-api-key\\\":\\\"" + token + "\\\",\\\"img\\\":\\\"data:image\\/jpeg;base64," + base64 + "\\\"}";

        var diagnostico = DiagnosticoResposta.Criar(
            400,
            "https://api.anthropic.com/v1/messages",
            "deepseek-v4-flash",
            "PedidoInvalido",
            "O pedido foi recusado.",
            "JSON inválido",
            "end_turn",
            null,
            corpo,
            corpoIncompleto: false,
            segredos: null);

        var log = new StringBuilder();
        RegistroDiagnosticoLeitura.Registrar(diagnostico, texto => log.Append(texto));

        var dialog = File.ReadAllText(Caminho("OrcaFacil", "Components", "Pages", "Cesta", "DiagnosticoLeituraDialog.razor"));
        var painel = File.ReadAllText(Caminho("OrcaFacil", "Components", "Pages", "Cesta", "LeituraComprovantePainel.razor"));
        Assert.Contains("@Texto", dialog, StringComparison.Ordinal);
        Assert.Contains("Clipboard.Default.SetTextAsync(Texto)", dialog, StringComparison.Ordinal);
        Assert.Contains("RegistroDiagnosticoLeitura.Registrar", painel, StringComparison.Ordinal);

        Assert.Equal(diagnostico.Texto, log.ToString());
        Assert.DoesNotContain(token, diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(base64, diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("x-api-key", diagnostico.Texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:image", diagnostico.Texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(token, log.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(base64, log.ToString(), StringComparison.Ordinal);
        Assert.Contains(DiagnosticoResposta.MarcaCabecalho, diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains(DiagnosticoResposta.MarcaBase64, diagnostico.Texto, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(@"data:image\u002Fjpeg")]
    [InlineData(@"DATA:IMAGE\u002fJPEG")]
    public void BarraUnicode_NaoVazaBase64Curto(string prefixo)
    {
        var base64 = new string('Q', 40);
        var corpo = prefixo + ";base64," + base64;

        var diagnostico = DiagnosticoResposta.Criar(
            400,
            "https://api.anthropic.com/v1/messages",
            "deepseek-v4-flash",
            "PedidoInvalido",
            "O pedido foi recusado.",
            "JSON inválido",
            "end_turn",
            null,
            corpo,
            corpoIncompleto: false,
            segredos: null);

        var log = new StringBuilder();
        RegistroDiagnosticoLeitura.Registrar(diagnostico, texto => log.Append(texto));

        var dialog = File.ReadAllText(Caminho("OrcaFacil", "Components", "Pages", "Cesta", "DiagnosticoLeituraDialog.razor"));
        var painel = File.ReadAllText(Caminho("OrcaFacil", "Components", "Pages", "Cesta", "LeituraComprovantePainel.razor"));
        Assert.Contains("@Texto", dialog, StringComparison.Ordinal);
        Assert.Contains("Clipboard.Default.SetTextAsync(Texto)", dialog, StringComparison.Ordinal);
        Assert.Contains("RegistroDiagnosticoLeitura.Registrar", painel, StringComparison.Ordinal);

        Assert.Equal(diagnostico.Texto, log.ToString());
        AssertNaoVazaBarraUnicode(diagnostico.Texto, base64);
        AssertNaoVazaBarraUnicode(log.ToString(), base64);
        Assert.Contains(DiagnosticoResposta.MarcaBase64, diagnostico.Texto, StringComparison.Ordinal);
    }

    public static IEnumerable<object[]> QuebrasNoMeioDaCredencial()
    {
        foreach (var escape in new[] { @"\u000a", @"\u000d", @"\u2028" })
        {
            yield return new object[] { "Authorization: Basic abc", escape };
            yield return new object[] { "Authorization: Bearer abc", escape };
            yield return new object[] { "x-api-key: abc", escape };
        }
    }

    [Theory]
    [MemberData(nameof(QuebrasNoMeioDaCredencial))]
    public void QuebraUnicode_NaoVazaCaudaDaCredencial(string prefixo, string escape)
    {
        const string cauda = "CAUDA-SECRETA-99";
        var corpo = prefixo + escape + cauda;

        var diagnostico = DiagnosticoResposta.Criar(
            401,
            "https://api.anthropic.com/v1/messages",
            "claude-haiku-5-5",
            "NaoAutorizado",
            "A chave da API foi recusada.",
            null,
            null,
            null,
            corpo,
            corpoIncompleto: false,
            segredos: null);

        var log = new StringBuilder();
        RegistroDiagnosticoLeitura.Registrar(diagnostico, texto => log.Append(texto));

        var dialog = File.ReadAllText(Caminho("OrcaFacil", "Components", "Pages", "Cesta", "DiagnosticoLeituraDialog.razor"));
        var painel = File.ReadAllText(Caminho("OrcaFacil", "Components", "Pages", "Cesta", "LeituraComprovantePainel.razor"));
        Assert.Contains("@Texto", dialog, StringComparison.Ordinal);
        Assert.Contains("Clipboard.Default.SetTextAsync(Texto)", dialog, StringComparison.Ordinal);
        Assert.Contains("RegistroDiagnosticoLeitura.Registrar", painel, StringComparison.Ordinal);

        Assert.Equal(diagnostico.Texto, log.ToString());
        Assert.DoesNotContain(cauda, diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(cauda, log.ToString(), StringComparison.Ordinal);
        Assert.Contains(DiagnosticoResposta.MarcaCabecalho, diagnostico.Texto, StringComparison.Ordinal);
    }

    [Fact]
    public void ControleUnicode_DepoisDoDesescape_NaoSobrevive()
    {
        var diagnostico = DiagnosticoResposta.Criar(
            200,
            "https://api.anthropic.com",
            "haiku\u2028Endpoint: https://evil.test",
            "Falha",
            "O provedor não concluiu a leitura.",
            "motivo",
            "end_turn",
            "stop",
            @"erro\u0000linha\u2028fim\u2029\u005Cu0000\u005Cu2028\u005Cu2029",
            false,
            segredos: null);

        Assert.DoesNotContain("\0", diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(@"\u0000", diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(@"\u005C", diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("\u2028", diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(@"\u2028", diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("\u2029", diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(@"\u2029", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("erro", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("linha", diagnostico.Texto, StringComparison.Ordinal);
        var endpoints = diagnostico.Texto.Split('\n').Where(linha => linha.StartsWith("Endpoint:", StringComparison.Ordinal)).ToArray();
        Assert.Single(endpoints);
        AssertDialogoCopiaELog(diagnostico);
    }

    [Fact]
    public void AuthorizationEApiKey_PerdemOValorInteiroEmQualquerCaixa()
    {
        const string bearer = "sk-bearer-nao-listado";
        const string basic = "dXNlcjpwYXNzZWNyZXRv";
        const string chave = "sk-x-api-key-nao-listado";
        var corpo = string.Join(
            '\n',
            "seguiu",
            "Authorization: Bearer " + bearer,
            "AUTHORIZATION: BASIC " + basic,
            "X-API-KEY: " + chave);

        var diagnostico = DiagnosticoResposta.Criar(
            401,
            "https://api.anthropic.com",
            "claude-haiku-5-5",
            "NaoAutorizado",
            "A chave da API foi recusada.",
            null,
            null,
            null,
            corpo,
            false,
            segredos: null);

        Assert.DoesNotContain(bearer, diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(basic, diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(chave, diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer", diagnostico.Texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Basic", diagnostico.Texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("x-api-key", diagnostico.Texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("seguiu", diagnostico.Texto, StringComparison.Ordinal);
    }

    [Fact]
    public void Controles_NaoForjamLinhaDeEndpoint()
    {
        var diagnostico = DiagnosticoResposta.Criar(
            200,
            "https://api.anthropic.com/v1/messages",
            "haiku\nEndpoint: https://evil.test",
            "Falha",
            "O provedor não concluiu a leitura.",
            "motivo\r\nEndpoint: https://evil.test",
            "end\u001b[0m\0turn",
            "stop\nEndpoint: https://evil.test",
            "corpo\nEndpoint: https://evil.test",
            false,
            segredos: null);

        var linhas = diagnostico.Texto.Split('\n');
        var endpoints = linhas.Where(linha => linha.StartsWith("Endpoint:", StringComparison.Ordinal)).ToArray();
        Assert.Single(endpoints);
        Assert.StartsWith("Endpoint: https://api.anthropic.com", endpoints[0], StringComparison.Ordinal);
        Assert.DoesNotContain("\r", diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("\0", diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("\u001b", diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("\nEndpoint: https://evil.test", diagnostico.Texto, StringComparison.Ordinal);
    }

    [Fact]
    public void CorpoNoLimite_NaoParteParSubstituto()
    {
        var corpo = new string('á', DiagnosticoResposta.LimiteCorpo - 1) + "😀" + "z";
        var diagnostico = DiagnosticoResposta.Criar(
            200,
            "https://api.anthropic.com",
            "claude-haiku-5-5",
            "RespostaInvalida",
            "A resposta veio fora do formato esperado.",
            "JSON inválido",
            null,
            null,
            corpo,
            false,
            segredos: null);

        Assert.True(diagnostico.CorpoTruncado);
        Assert.Equal(DiagnosticoResposta.LimiteCorpo - 1, diagnostico.Corpo!.Length);
        Assert.Equal('á', diagnostico.Corpo[^1]);
        Assert.DoesNotContain(diagnostico.Corpo, caractere => char.IsSurrogate(caractere));
        Assert.EndsWith(DiagnosticoResposta.MarcaTruncado, diagnostico.Texto, StringComparison.Ordinal);
    }

    [Fact]
    public void CamposCurtos_TambemRespeitamOCorteESurrogate()
    {
        var longo = new string('á', DiagnosticoResposta.LimiteCorpo - 1) + "😀" + "z";
        var diagnostico = DiagnosticoResposta.Criar(
            200,
            "https://api.anthropic.com",
            longo,
            "RespostaInvalida",
            "A resposta veio fora do formato esperado.",
            longo,
            longo,
            longo,
            "ok",
            false,
            segredos: null);

        Assert.Equal(DiagnosticoResposta.LimiteCorpo - 1, diagnostico.Modelo!.Length);
        Assert.Equal(DiagnosticoResposta.LimiteCorpo - 1, diagnostico.Motivo!.Length);
        Assert.Equal(DiagnosticoResposta.LimiteCorpo - 1, diagnostico.StopReason!.Length);
        Assert.Equal(DiagnosticoResposta.LimiteCorpo - 1, diagnostico.FinishReason!.Length);
        Assert.Equal('á', diagnostico.Motivo[^1]);
        Assert.DoesNotContain(diagnostico.Motivo, caractere => char.IsSurrogate(caractere));
        Assert.DoesNotContain(diagnostico.Texto, caractere => char.IsSurrogate(caractere));
    }

    [Fact]
    public void Base64Longo_SemDataImageNemSegredo_EMascarado()
    {
        var bloco = new string('A', 80) + new string('b', 80) + new string('C', 50);
        var diagnostico = DiagnosticoResposta.Criar(
            200,
            "https://api.anthropic.com",
            "claude-haiku-5-5",
            "RespostaInvalida",
            "A resposta veio fora do formato esperado.",
            "JSON inválido",
            null,
            null,
            "antes " + bloco + " depois",
            false,
            segredos: null);

        Assert.Contains(DiagnosticoResposta.MarcaBase64, diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(bloco, diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("antes", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("depois", diagnostico.Texto, StringComparison.Ordinal);
    }

    [Fact]
    public void CorpoLongo_CortaEmOitoMilEMarcaTruncado()
    {
        var corpo = string.Concat(Enumerable.Repeat("erro de leitura. ", 600));
        var diagnostico = DiagnosticoResposta.Criar(
            null,
            "https://api.anthropic.com/v1/messages",
            "claude-haiku-5-5",
            "RespostaInvalida",
            "A resposta veio fora do formato esperado.",
            "JSON inválido",
            null,
            null,
            corpo,
            corpoIncompleto: false,
            segredos: null);

        Assert.Equal("https://api.anthropic.com", diagnostico.Origem);
        Assert.True(diagnostico.CorpoTruncado);
        Assert.EndsWith(DiagnosticoResposta.MarcaTruncado, diagnostico.Texto, StringComparison.Ordinal);
        Assert.Equal(DiagnosticoResposta.LimiteCorpo, diagnostico.Corpo!.Length);
        Assert.DoesNotContain("/v1/messages", diagnostico.Texto, StringComparison.Ordinal);
    }

    [Fact]
    public void Origem_OmitePortaPadraoUsuarioConsultaECaminho()
    {
        Assert.Equal("https://api.exemplo.test", DiagnosticoResposta.SomenteOrigem("https://user:segredo@api.exemplo.test:443/v1/messages?x=1"));
        Assert.Equal("http://127.0.0.1:5000", DiagnosticoResposta.SomenteOrigem("http://127.0.0.1:5000/v1/messages"));
        Assert.Equal("https://[::1]", DiagnosticoResposta.SomenteOrigem("https://[::1]/v1/messages"));
        Assert.Null(DiagnosticoResposta.SomenteOrigem("nao-e-url"));
        Assert.Null(DiagnosticoResposta.SomenteOrigem((string?)null));
    }

    [Fact]
    public void Painel_AbreDialogoCopiaERegistraOMesmoTexto()
    {
        var painel = File.ReadAllText(Caminho("OrcaFacil", "Components", "Pages", "Cesta", "LeituraComprovantePainel.razor"));
        var dialog = File.ReadAllText(Caminho("OrcaFacil", "Components", "Pages", "Cesta", "DiagnosticoLeituraDialog.razor"));

        Assert.Contains("Ver erro detalhado", painel, StringComparison.Ordinal);
        Assert.Contains("Tentar de novo", painel, StringComparison.Ordinal);
        Assert.Contains("RegistroDiagnosticoLeitura.Registrar", painel, StringComparison.Ordinal);
        Assert.Contains("Logger.LogWarning(\"Diagnóstico da leitura do comprovante: {Diagnostico}\", texto)", painel, StringComparison.Ordinal);
        Assert.Contains("x => x.Texto, texto", painel, StringComparison.Ordinal);
        Assert.Contains("Clipboard.Default.SetTextAsync(Texto)", dialog, StringComparison.Ordinal);
        Assert.Contains("@Texto", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("x-api-key", painel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", dialog, StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<object[]> SeparadoresDeChave()
    {
        foreach (var separador in Separadores)
            yield return new object[] { separador };
    }

    public static IEnumerable<object[]> CabecalhosPartidos()
    {
        foreach (var separador in Separadores)
        {
            yield return new object[] { "Authori", "zation: Basic ", separador };
            yield return new object[] { "x-api", "-key: ", separador };
        }
    }

    public static IEnumerable<object[]> Base64ForaDoContinuo()
    {
        yield return new object[] { Bloco76("\n") };
        yield return new object[] { Bloco76("\r\n") };
        yield return new object[] { string.Concat(Enumerable.Repeat("Ab-_", 55)) };
    }

    [Theory]
    [MemberData(nameof(SeparadoresDeChave))]
    public void ChavePartida_NaoVazaNoDialogoNaCopiaNemNoLog(string separador)
    {
        const string segredo = "sk-ant-ficticio-9f3c2a7bZZ";
        var meio = segredo.Length / 2;
        var esquerda = segredo[..meio];
        var direita = segredo[meio..];
        var diagnostico = Diagnosticar("token " + esquerda + separador + direita + " fim", segredo);

        var log = AssertDialogoCopiaELog(diagnostico);
        Assert.DoesNotContain(esquerda, diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(direita, diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(esquerda, log, StringComparison.Ordinal);
        Assert.DoesNotContain(direita, log, StringComparison.Ordinal);
        Assert.Contains("***", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("fim", diagnostico.Texto, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(CabecalhosPartidos))]
    public void CabecalhoPartido_NaoVazaNoDialogoNaCopiaNemNoLog(string esquerda, string direita, string separador)
    {
        const string cauda = "CAUDA-9F3C-ZZ";
        var diagnostico = Diagnosticar(esquerda + separador + direita + cauda);

        var log = AssertDialogoCopiaELog(diagnostico);
        Assert.DoesNotContain(cauda, diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(cauda, log, StringComparison.Ordinal);
        Assert.Contains(DiagnosticoResposta.MarcaCabecalho, diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains(DiagnosticoResposta.MarcaCabecalho, log, StringComparison.Ordinal);
    }

    [Fact]
    public void ChavePercentEncoded_NaoVazaNoDialogoNaCopiaNemNoLog()
    {
        const string segredo = "sk-ant-ficticio_9f3c";
        const string codificada = "sk%2Dant%2Dficticio%5F9f3c";
        const string mista = "sk%2dant-ficticio_9f3c";
        var diagnostico = Diagnosticar("solto " + codificada + " e " + mista, segredo);

        var log = AssertDialogoCopiaELog(diagnostico);
        Assert.DoesNotContain(segredo, diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(codificada, diagnostico.Texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(mista, diagnostico.Texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(segredo, log, StringComparison.Ordinal);
        Assert.DoesNotContain(codificada, log, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(mista, log, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("solto", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("***", diagnostico.Texto, StringComparison.Ordinal);
    }

    [Fact]
    public void SegredoCurto_NaoVazaNoDialogoNaCopiaNemNoLog()
    {
        const string segredo = "s3gr3d";
        Assert.InRange(segredo.Length, 1, 7);
        var diagnostico = DiagnosticoResposta.Criar(
            401,
            "https://api.anthropic.com/v1/messages",
            "claude-haiku-5-5",
            "NaoAutorizado",
            "A chave " + segredo + " foi recusada.",
            null,
            null,
            null,
            "vazou " + segredo + " no corpo",
            false,
            [segredo]);

        var log = AssertDialogoCopiaELog(diagnostico);
        Assert.DoesNotContain(segredo, diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(segredo, log, StringComparison.Ordinal);
        Assert.Contains("vazou", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("foi recusada", diagnostico.Texto, StringComparison.Ordinal);
    }

    [Fact]
    public void SegredoCurto_SoMascaraTokenIsolado()
    {
        Assert.InRange("api".Length, 1, 7);
        Assert.InRange("haiku".Length, 1, 7);
        Assert.InRange("500".Length, 1, 7);
        Assert.InRange("abc123x".Length, 1, 7);
        Assert.InRange("abc123".Length, 1, 7);

        const string alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        var linha = alfabeto + alfabeto[..12];
        var corpo = "total 1500 e o codigo 500 acabou\n{\"token\":\"abc123x\"}\nsenha=abc123x\n"
            + "{'s':'abc123'}\nk:abc123\n{\"s\":\"abc123\"}\nk=abc123\npreabc123pos\n"
            + string.Join("\n", linha, linha, linha)
            + "\nO total da nota";

        var diagnostico = DiagnosticoResposta.Criar(
            401,
            "https://api.anthropic.com/v1/messages",
            "claude-haiku-5-5",
            "NaoAutorizado",
            "A chave da API foi recusada.",
            null,
            null,
            null,
            corpo,
            false,
            ["api", "haiku", "500", "abc123x", "abc123"]);

        var log = AssertDialogoCopiaELog(diagnostico);
        Assert.Contains("https://api.anthropic.com", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("claude-haiku-5-5", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("1500", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("O total da nota", diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("codigo 500", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("codigo ***", diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123x", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("{\"token\":\"***\"}", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("senha=***", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("{'s':'***'}", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("k:***", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("{\"s\":\"***\"}", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("k=***", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("preabc123pos", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("https://api.anthropic.com", log, StringComparison.Ordinal);
        Assert.Contains("claude-haiku-5-5", log, StringComparison.Ordinal);
        Assert.Contains("1500", log, StringComparison.Ordinal);
        Assert.Contains("O total da nota", log, StringComparison.Ordinal);
        Assert.DoesNotContain("codigo 500", log, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123x", log, StringComparison.Ordinal);
        Assert.Contains("{'s':'***'}", log, StringComparison.Ordinal);
        Assert.Contains("k:***", log, StringComparison.Ordinal);
        Assert.Contains("{\"s\":\"***\"}", log, StringComparison.Ordinal);
        Assert.Contains("k=***", log, StringComparison.Ordinal);
        Assert.Contains("preabc123pos", log, StringComparison.Ordinal);
        Assert.DoesNotContain("{'s':'abc123'}", diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("{'s':'abc123'}", log, StringComparison.Ordinal);
        Assert.DoesNotContain(linha[..40], diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(linha[..40], log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(48)]
    [InlineData(76)]
    public void Base64UltimaLinhaCurta_TambemEMascarada(int tamanhoFinal)
    {
        const string alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        var linha = alfabeto + alfabeto[..12];
        var final = tamanhoFinal == 76 ? linha : new string('9', tamanhoFinal);
        var bloco = string.Join("\n", linha, linha, final);
        var diagnostico = Diagnosticar("antes\n" + bloco + "\nO total depois");

        var log = AssertDialogoCopiaELog(diagnostico);
        Assert.Contains(DiagnosticoResposta.MarcaBase64, diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains(DiagnosticoResposta.MarcaBase64, log, StringComparison.Ordinal);
        Assert.DoesNotContain(linha[..40], diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(final, diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(linha[..40], log, StringComparison.Ordinal);
        Assert.DoesNotContain(final, log, StringComparison.Ordinal);
        Assert.Contains("antes", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("O total depois", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("O total depois", log, StringComparison.Ordinal);
    }

    [Fact]
    public void Base64_NaoEngoleOPrimeiroTokenDaProsa()
    {
        const string alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        var linha = alfabeto + alfabeto[..12];
        var bloco = string.Join("\n", linha, linha, linha);
        var diagnostico = Diagnosticar(bloco + "\nO total da nota");

        var log = AssertDialogoCopiaELog(diagnostico);
        Assert.Contains(DiagnosticoResposta.MarcaBase64, diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("O total da nota", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("O total da nota", log, StringComparison.Ordinal);
        Assert.DoesNotContain(linha[..40], diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(linha[..40], log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\n", "=", " ")]
    [InlineData("\n", "=", "\"")]
    [InlineData("\n", "=", "'")]
    [InlineData("\n", "==", " ")]
    [InlineData("\n", "==", "\"")]
    [InlineData("\n", "==", "'")]
    [InlineData("\r\n", "=", "\r\n")]
    [InlineData("\r\n", "==", "\r\n")]
    public void Base64ComPadding_TambemEMascarado(string quebra, string padding, string fim)
    {
        const string alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        var linha = alfabeto + alfabeto[..12];
        var final = "abcd" + padding;
        var bloco = string.Join(quebra, linha, linha, final);
        var diagnostico = Diagnosticar("antes" + quebra + bloco + fim + "O total");
        var prosa = fim.IndexOfAny(['\r', '\n']) >= 0 ? "O total" : fim + "O total";

        var log = AssertDialogoCopiaELog(diagnostico);
        Assert.Contains(DiagnosticoResposta.MarcaBase64, diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains(DiagnosticoResposta.MarcaBase64, log, StringComparison.Ordinal);
        Assert.DoesNotContain(final, diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(padding, diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(final, log, StringComparison.Ordinal);
        Assert.DoesNotContain(padding, log, StringComparison.Ordinal);
        Assert.Contains(prosa, diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains(prosa, log, StringComparison.Ordinal);

        var fechado = Diagnosticar(string.Join("\n", linha, linha, linha) + "\nO total da nota");
        var logFechado = AssertDialogoCopiaELog(fechado);
        Assert.Contains("O total da nota", fechado.Texto, StringComparison.Ordinal);
        Assert.Contains("O total da nota", logFechado, StringComparison.Ordinal);
        Assert.DoesNotContain(linha[..40], fechado.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(linha[..40], logFechado, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, " ")]
    [InlineData(1, "\"")]
    [InlineData(1, "'")]
    [InlineData(48, " ")]
    [InlineData(48, "\"")]
    [InlineData(48, "'")]
    [InlineData(76, " ")]
    [InlineData(76, "\"")]
    [InlineData(76, "'")]
    public void Base64Cauda_SeguidaDeEspacoOuAspas(int tamanho, string fim)
    {
        const string alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        var linha = alfabeto + alfabeto[..12];
        var final = new string('9', tamanho);
        var bloco = string.Join("\n", linha, linha, final);
        var diagnostico = Diagnosticar("antes\n" + bloco + fim + "O total");

        var log = AssertDialogoCopiaELog(diagnostico);
        Assert.Contains(DiagnosticoResposta.MarcaBase64, diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains(DiagnosticoResposta.MarcaBase64, log, StringComparison.Ordinal);
        Assert.DoesNotContain(final, diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(final, log, StringComparison.Ordinal);
        Assert.DoesNotContain(linha[..40], diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(linha[..40], log, StringComparison.Ordinal);
        Assert.Contains("antes", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains(fim + "O total", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains(fim + "O total", log, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Base64ForaDoContinuo))]
    public void Base64EmLinhasOuUrlSafe_NaoVazaNoDialogoNaCopiaNemNoLog(string bloco)
    {
        var diagnostico = Diagnosticar("antes\n" + bloco + "\nO total depois");
        var amostra = bloco.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal);

        var log = AssertDialogoCopiaELog(diagnostico);
        Assert.Contains(DiagnosticoResposta.MarcaBase64, diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains(DiagnosticoResposta.MarcaBase64, log, StringComparison.Ordinal);
        Assert.DoesNotContain(amostra[..40], diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(amostra[^40..], diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(amostra[..40], log, StringComparison.Ordinal);
        Assert.DoesNotContain(amostra[^40..], log, StringComparison.Ordinal);
        Assert.Contains("antes", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("O total depois", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("O total depois", log, StringComparison.Ordinal);
    }

    [Fact]
    public void NoveCamadasDeEscape_NaoReintroduzControleNemEndpoint()
    {
        var quebra = CamadasDeEscape(@"\u000a", 9);
        var diagnostico = DiagnosticoResposta.Criar(
            200,
            "https://api.anthropic.com/v1/messages",
            "haiku" + quebra + "Endpoint: https://evil.test",
            "Falha",
            "O provedor não concluiu a leitura.",
            "motivo",
            "end_turn",
            "stop",
            "inicio" + CamadasDeEscape(@"\u0000", 9) + CamadasDeEscape(@"\u2028", 9) + CamadasDeEscape(@"\u2029", 9) + quebra + "Endpoint: https://evil.test",
            false,
            segredos: null);

        var log = AssertDialogoCopiaELog(diagnostico);
        Assert.DoesNotContain("\0", diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("\0", log, StringComparison.Ordinal);
        Assert.DoesNotContain("\u2028", diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("\u2028", log, StringComparison.Ordinal);
        Assert.DoesNotContain("\u2029", diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("\u2029", log, StringComparison.Ordinal);
        Assert.Contains("inicio", diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("haiku", diagnostico.Texto, StringComparison.Ordinal);
        var endpoints = diagnostico.Texto.Split('\n').Where(linha => linha.StartsWith("Endpoint:", StringComparison.Ordinal)).ToArray();
        Assert.Single(endpoints);
        Assert.StartsWith("Endpoint: https://api.anthropic.com", endpoints[0], StringComparison.Ordinal);
        Assert.DoesNotContain("\nEndpoint: https://evil.test", diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("\nEndpoint: https://evil.test", log, StringComparison.Ordinal);
    }

    private static readonly string[] Separadores =
    [
        "\n",
        "\r",
        " ",
        "\t",
        "\u200B",
        "\u200C",
        "\u200D",
        "\uFEFF",
        "\u2060",
        "\u2028",
        "\u2029",
        "\\u000a",
        "\\u000d",
        "\\u200b",
    ];

    private static DiagnosticoResposta Diagnosticar(string corpo, params string[] segredos) =>
        DiagnosticoResposta.Criar(
            401,
            "https://api.anthropic.com/v1/messages",
            "claude-haiku-5-5",
            "NaoAutorizado",
            "A chave da API foi recusada.",
            null,
            null,
            null,
            corpo,
            false,
            segredos);

    private static string AssertDialogoCopiaELog(DiagnosticoResposta diagnostico)
    {
        var log = new StringBuilder();
        RegistroDiagnosticoLeitura.Registrar(diagnostico, texto => log.Append(texto));

        var dialog = File.ReadAllText(Caminho("OrcaFacil", "Components", "Pages", "Cesta", "DiagnosticoLeituraDialog.razor"));
        var painel = File.ReadAllText(Caminho("OrcaFacil", "Components", "Pages", "Cesta", "LeituraComprovantePainel.razor"));
        Assert.Contains("@Texto", dialog, StringComparison.Ordinal);
        Assert.Contains("Clipboard.Default.SetTextAsync(Texto)", dialog, StringComparison.Ordinal);
        Assert.Contains("RegistroDiagnosticoLeitura.Registrar", painel, StringComparison.Ordinal);
        Assert.Equal(diagnostico.Texto, log.ToString());
        return log.ToString();
    }

    private static string Bloco76(string quebra)
    {
        const string alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        var linha = alfabeto + alfabeto[..12];
        return string.Join(quebra, linha, linha, linha);
    }

    private static string CamadasDeEscape(string unidade, int camadas)
    {
        var atual = unidade;
        for (var i = 1; i < camadas; i++)
            atual = atual.Replace("\\", @"\u005C", StringComparison.Ordinal);

        return atual;
    }

    private static void AssertNaoVazaBarraUnicode(string texto, string base64)
    {
        Assert.DoesNotContain(base64, texto, StringComparison.Ordinal);
        Assert.DoesNotContain("data:image", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"\u002", texto, StringComparison.Ordinal);
    }

    private static void AssertNaoVaza(string texto)
    {
        Assert.DoesNotContain(Token, texto, StringComparison.Ordinal);
        Assert.DoesNotContain("x-api-key", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("anthropic-version", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:image", texto, StringComparison.OrdinalIgnoreCase);
    }

    private static string Caminho(params string[] partes)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var caminho = Path.Combine(new[] { dir.FullName }.Concat(partes).ToArray());
            if (File.Exists(caminho))
                return caminho;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar " + string.Join('/', partes));
    }
}
