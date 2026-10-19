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

    [Fact]
    public void AuthorizationEApiKey_PerdemOValorInteiroEmQualquerCaixa()
    {
        const string bearer = "sk-bearer-nao-listado";
        const string basic = "dXNlcjpwYXNzZWNyZXRv";
        const string chave = "sk-x-api-key-nao-listado";
        var corpo = string.Join(
            '\n',
            "Authorization: Bearer " + bearer,
            "AUTHORIZATION: BASIC " + basic,
            "X-API-KEY: " + chave,
            "seguiu");

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
