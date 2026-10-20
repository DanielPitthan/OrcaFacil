using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using OrcaFacil.Core.Llm;

namespace OrcaFacil.Tests.Llm;

public class ClienteMensagensTests
{
    private const string Base = "https://api.anthropic.com";
    private const string Chave = "chave-teste";
    private const string Imagem = "dGVzdGU=";
    private const string Midia = "image/jpeg";

    [Fact]
    public async Task Pedido_PostaMessagesComSystemImagemFerramentaEEscolhaFixa()
    {
        var captura = new CapturaHandler(_ => Resposta(HttpStatusCode.OK, CorpoValido()));
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.LerComprovanteAsync(
            Base + "/",
            Chave,
            PedidoMensagem.ModeloAnthropic,
            Midia,
            Imagem);

        Assert.True(resultado.Sucesso);
        Assert.Equal(1, captura.Chamadas);
        Assert.Equal("POST", captura.Metodo);
        Assert.Equal("https://api.anthropic.com/v1/messages", captura.Uri);
        Assert.Equal(Chave, captura.Chave);
        Assert.Equal("2023-06-01", captura.Versao);
        Assert.Equal(ClienteMensagens.VersaoAnthropic, captura.Versao);
        Assert.StartsWith("application/json", captura.TipoConteudo, StringComparison.Ordinal);
        Assert.DoesNotContain(Chave, captura.Uri, StringComparison.Ordinal);
        Assert.DoesNotContain(Chave, captura.Corpo, StringComparison.Ordinal);

        using var doc = JsonDocument.Parse(captura.Corpo);
        var raiz = doc.RootElement;
        Assert.Equal("system", raiz.EnumerateObject().First().Name);
        Assert.Equal(PedidoMensagem.Sistema, raiz.GetProperty("system").GetString());
        Assert.Contains(PedidoMensagem.NomeFerramenta, raiz.GetProperty("system").GetString(), StringComparison.Ordinal);
        Assert.Equal("claude-haiku-5-5", PedidoMensagem.ModeloAnthropic);
        Assert.Equal(PedidoMensagem.ModeloAnthropic, raiz.GetProperty("model").GetString());
        Assert.Equal(PedidoMensagem.MaxTokens, raiz.GetProperty("max_tokens").GetInt32());
        Assert.False(raiz.TryGetProperty("thinking", out _));
        Assert.DoesNotContain("\"thinking\"", captura.Corpo, StringComparison.Ordinal);
        AssertSemAmostragem(raiz);

        var mensagens = raiz.GetProperty("messages");
        Assert.Equal(1, mensagens.GetArrayLength());
        Assert.Equal("user", mensagens[0].GetProperty("role").GetString());
        var imagem = mensagens[0].GetProperty("content").EnumerateArray()
            .Single(b => b.GetProperty("type").GetString() == "image");
        var fonte = imagem.GetProperty("source");
        Assert.Equal("base64", fonte.GetProperty("type").GetString());
        Assert.Equal(Midia, fonte.GetProperty("media_type").GetString());
        Assert.Equal(Imagem, fonte.GetProperty("data").GetString());

        var ferramenta = raiz.GetProperty("tools")[0];
        Assert.Equal(PedidoMensagem.NomeFerramenta, ferramenta.GetProperty("name").GetString());
        var esquema = ferramenta.GetProperty("input_schema");
        Assert.Equal("object", esquema.GetProperty("type").GetString());
        Assert.Equal(new[] { "legivel", "itens" }, Nomes(esquema.GetProperty("required")));
        var propriedades = esquema.GetProperty("properties");
        Assert.Equal(
            new[] { "loja", "data", "total_cupom", "legivel", "itens" },
            propriedades.EnumerateObject().Select(p => p.Name).ToArray());
        var item = propriedades.GetProperty("itens").GetProperty("items");
        Assert.Equal(
            new[] { "descricao_lida", "produto", "reconhecido", "preco" },
            Nomes(item.GetProperty("required")));
        Assert.Equal(
            new[] { "alta", "media", "baixa" },
            Nomes(item.GetProperty("properties").GetProperty("confianca").GetProperty("enum")));

        var escolha = raiz.GetProperty("tool_choice");
        Assert.Equal("tool", escolha.GetProperty("type").GetString());
        Assert.Equal(PedidoMensagem.NomeFerramenta, escolha.GetProperty("name").GetString());
    }

    [Fact]
    public async Task ChaveTrocaACadaPedido_ENaoFicaNoCliente()
    {
        var captura = new CapturaHandler(_ => Resposta(HttpStatusCode.OK, CorpoValido()));
        using var cliente = new ClienteMensagens(captura);

        await cliente.LerComprovanteAsync(Base, "chave-um", PedidoMensagem.ModeloAnthropic, Midia, Imagem);
        await cliente.LerComprovanteAsync(Base, "chave-dois", PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(["chave-um", "chave-dois"], captura.Chaves);
        Assert.Equal(Timeout.InfiniteTimeSpan, cliente.TimeoutDoTransporte);
    }

    [Theory]
    [InlineData(401, TipoResultadoMensagem.NaoAutorizado, ResultadoMensagem.TextoNaoAutorizado)]
    [InlineData(403, TipoResultadoMensagem.SemPermissao, ResultadoMensagem.TextoSemPermissao)]
    [InlineData(429, TipoResultadoMensagem.Limite, ResultadoMensagem.TextoLimite)]
    [InlineData(529, TipoResultadoMensagem.Sobrecarga, ResultadoMensagem.TextoSobrecarga)]
    [InlineData(504, TipoResultadoMensagem.Timeout, ResultadoMensagem.TextoTimeout)]
    [InlineData(400, TipoResultadoMensagem.PedidoInvalido, ResultadoMensagem.TextoPedidoInvalido)]
    public async Task TraduzStatusHttp(int status, TipoResultadoMensagem tipo, string mensagem)
    {
        var captura = new CapturaHandler(_ => Resposta((HttpStatusCode)status, "corpo-secreto " + Chave + " " + Imagem));
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.LerComprovanteAsync(Base, Chave, PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(1, captura.Chamadas);
        Assert.Equal(tipo, resultado.Tipo);
        Assert.Equal(mensagem, resultado.Mensagem);
        Assert.Null(resultado.Comprovante);
        Assert.DoesNotContain(Chave, resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain(Imagem, resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("corpo-secreto", resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain(Base, resultado.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ModeloSemVisao_ViraFraseFixaSemCorpoNemChave()
    {
        var corpo = "{\"error\":{\"message\":\"The model does not support image content blocks. " + Chave + "\"}}";
        var captura = new CapturaHandler(_ => Resposta(HttpStatusCode.BadRequest, corpo));
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.LerComprovanteAsync(Base, Chave, PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(TipoResultadoMensagem.ModeloSemVisao, resultado.Tipo);
        Assert.Equal(ResultadoMensagem.TextoModeloSemVisao, resultado.Mensagem);
        Assert.Contains("Troque o modelo", resultado.Mensagem, StringComparison.Ordinal);
        Assert.Null(resultado.Comprovante);
        Assert.DoesNotContain(Chave, resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("content blocks", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Imagem, resultado.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void MensagensDeLimiteESobrecargaSaoDistintas()
    {
        Assert.NotEqual(ResultadoMensagem.TextoLimite, ResultadoMensagem.TextoSobrecarga);
        Assert.NotEqual(ResultadoMensagem.TextoTimeout, ResultadoMensagem.TextoLimite);
        Assert.NotEqual(ResultadoMensagem.TextoNaoAutorizado, ResultadoMensagem.TextoPedidoInvalido);
        Assert.NotEqual(ResultadoMensagem.TextoNaoAutorizado, ResultadoMensagem.TextoSemPermissao);
        Assert.Equal("A chave da API foi recusada.", ResultadoMensagem.TextoNaoAutorizado);
        Assert.Equal(
            "A chave foi aceita, mas não tem permissão para este uso. Confira sua conta no provedor.",
            ResultadoMensagem.TextoSemPermissao);
        Assert.DoesNotContain("401", ResultadoMensagem.TextoNaoAutorizado, StringComparison.Ordinal);
        Assert.DoesNotContain("403", ResultadoMensagem.TextoSemPermissao, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NaoAutorizado_NaoRepeteOPedidoMesmoComRetryAfter()
    {
        var captura = new CapturaHandler(_ =>
        {
            var resposta = Resposta(HttpStatusCode.Unauthorized, "nao-ler");
            resposta.Headers.TryAddWithoutValidation("Retry-After", "5");
            return resposta;
        });
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.LerComprovanteAsync(Base, Chave, PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(1, captura.Chamadas);
        Assert.Equal(TipoResultadoMensagem.NaoAutorizado, resultado.Tipo);
        Assert.Null(resultado.RetryAfter);
    }

    [Fact]
    public async Task Limite_LeRetryAfterEmSegundos_SemRepetir()
    {
        var captura = new CapturaHandler(_ =>
        {
            var resposta = Resposta((HttpStatusCode)429, "nao-ler");
            resposta.Headers.TryAddWithoutValidation("Retry-After", "17");
            return resposta;
        });
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.LerComprovanteAsync(Base, Chave, PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(1, captura.Chamadas);
        Assert.Equal(TipoResultadoMensagem.Limite, resultado.Tipo);
        Assert.Equal(TimeSpan.FromSeconds(17), resultado.RetryAfter);
        Assert.DoesNotContain("17", resultado.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Limite_LeRetryAfterEmDataHttp()
    {
        var quando = DateTimeOffset.UtcNow.AddSeconds(90);
        var captura = new CapturaHandler(_ =>
        {
            var resposta = Resposta((HttpStatusCode)429, "nao-ler");
            resposta.Headers.TryAddWithoutValidation("Retry-After", quando.ToString("r", CultureInfo.InvariantCulture));
            return resposta;
        });
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.LerComprovanteAsync(Base, Chave, PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.NotNull(resultado.RetryAfter);
        Assert.InRange(resultado.RetryAfter.Value, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(120));
    }

    [Fact]
    public async Task Limite_RetryAfterIlegivelFicaAusente()
    {
        var captura = new CapturaHandler(_ =>
        {
            var resposta = Resposta((HttpStatusCode)429, "nao-ler");
            resposta.Headers.TryAddWithoutValidation("Retry-After", "logo");
            return resposta;
        });
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.LerComprovanteAsync(Base, Chave, PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(TipoResultadoMensagem.Limite, resultado.Tipo);
        Assert.Null(resultado.RetryAfter);
    }

    [Fact]
    public async Task TimeoutDoTransporte_ViraTimeoutSemVazarDetalhe()
    {
        var captura = new CapturaHandler(_ => throw new TaskCanceledException("estourou " + Chave + " " + Base + " " + Imagem));
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.LerComprovanteAsync(Base, Chave, PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(TipoResultadoMensagem.Timeout, resultado.Tipo);
        Assert.Equal(ResultadoMensagem.TextoTimeout, resultado.Mensagem);
        Assert.DoesNotContain(Chave, resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain(Imagem, resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain(Base, resultado.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrazoProprio_CancelaPeloTokenSemAlterarTimeoutDoCliente()
    {
        var captura = new CapturaHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Resposta(HttpStatusCode.OK, CorpoValido());
        });
        using var cliente = new ClienteMensagens(captura, TimeSpan.FromMilliseconds(200));

        var resultado = await cliente.LerComprovanteAsync(Base, Chave, PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(TipoResultadoMensagem.Timeout, resultado.Tipo);
        Assert.Equal(Timeout.InfiniteTimeSpan, cliente.TimeoutDoTransporte);
        Assert.NotEqual(ClienteMensagens.TempoLimite, cliente.TimeoutDoTransporte);
        Assert.Equal(TimeSpan.FromSeconds(60), ClienteMensagens.TempoLimite);
        Assert.True(ClienteMensagens.TempoLimite > TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task Criar_RecusaHttpENaoUsaTimeoutDoHttpClient()
    {
        using var cliente = ClienteMensagens.Criar();

        var resultado = await cliente.LerComprovanteAsync(
            "http://api.anthropic.com",
            Chave,
            PedidoMensagem.ModeloAnthropic,
            Midia,
            Imagem);

        Assert.Equal(TipoResultadoMensagem.PedidoInvalido, resultado.Tipo);
        Assert.Equal(Timeout.InfiniteTimeSpan, cliente.TimeoutDoTransporte);
        Assert.DoesNotContain(Chave, resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("http", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CriarHandler_DesligaRedirectENaoUsaTimeoutDe60Segundos()
    {
        using var handler = ClienteMensagens.CriarHandler();
        using var http = ClienteMensagens.CriarHttpClient();

        Assert.False(handler.AllowAutoRedirect);
        Assert.Equal(Timeout.InfiniteTimeSpan, http.Timeout);
        Assert.NotEqual(ClienteMensagens.TempoLimite, http.Timeout);
    }

    [Fact]
    public async Task CancelamentoDoChamador_NaoViraTimeout()
    {
        var captura = new CapturaHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Resposta(HttpStatusCode.OK, CorpoValido());
        });
        using var cliente = new ClienteMensagens(captura);
        using var cts = new CancellationTokenSource();
        var tarefa = cliente.LerComprovanteAsync(Base, Chave, PedidoMensagem.ModeloAnthropic, Midia, Imagem, cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tarefa);
    }

    [Fact]
    public async Task RespostaForaDoSchema_ViraRespostaInvalidaSemEcoarCorpo()
    {
        const string corpo = "<html>segredo-corpo e " + Imagem + "</html>";
        var captura = new CapturaHandler(_ => Resposta(HttpStatusCode.OK, corpo));
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.LerComprovanteAsync(Base, Chave, PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(TipoResultadoMensagem.RespostaInvalida, resultado.Tipo);
        Assert.Equal(ResultadoMensagem.TextoRespostaInvalida, resultado.Mensagem);
        Assert.DoesNotContain("segredo-corpo", resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain(Imagem, resultado.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TextoComCerca_NaoEFalsoNegativo()
    {
        const string corpo = """
            {
              "stop_reason": "end_turn",
              "content": [
                { "type": "thinking", "thinking": "rascunho" },
                { "type": "text", "text": "Leitura:\n```json\n{\"loja\":\"Extra\",\"legivel\":true,\"total_cupom\":\"12,90\",\"itens\":[{\"descricao_lida\":\"ARROZ\",\"preco\":\"12,90\"}]}\n```" }
              ]
            }
            """;
        var captura = new CapturaHandler(_ => Resposta(HttpStatusCode.OK, corpo));
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.LerComprovanteAsync(Base, Chave, "deepseek-v4-flash", Midia, Imagem);

        Assert.True(resultado.Sucesso);
        Assert.Null(resultado.Diagnostico);
        Assert.Equal("Extra", resultado.Comprovante!.Loja);
        Assert.Equal(12.90m, Assert.Single(resultado.Comprovante.Itens).Preco);
    }

    [Fact]
    public async Task MaxTokens_Padrao8192_AceitaFaixa_ENaoChamaForaDela()
    {
        Assert.Equal(8192, PedidoMensagem.MaxTokens);
        Assert.Equal(256, PedidoMensagem.MaxTokensMinimo);
        Assert.Equal(32768, PedidoMensagem.MaxTokensMaximo);
        Assert.Equal("O máximo de tokens fica entre 256 e 32768.", PedidoMensagem.TextoMaxTokensForaDaFaixa);
        Assert.True(PedidoMensagem.MaxTokensNaFaixa(256));
        Assert.True(PedidoMensagem.MaxTokensNaFaixa(8192));
        Assert.True(PedidoMensagem.MaxTokensNaFaixa(32768));
        Assert.False(PedidoMensagem.MaxTokensNaFaixa(255));
        Assert.False(PedidoMensagem.MaxTokensNaFaixa(32769));
        Assert.Equal(8192, PedidoMensagem.NormalizarMaxTokens(0));
        Assert.Equal(8192, PedidoMensagem.NormalizarMaxTokens(255));
        Assert.Equal(8192, PedidoMensagem.NormalizarMaxTokens(32769));
        Assert.Equal(16384, PedidoMensagem.NormalizarMaxTokens(16384));

        var captura = new CapturaHandler(_ => Resposta(HttpStatusCode.OK, CorpoValido()));
        using var cliente = new ClienteMensagens(captura);

        await cliente.LerComprovanteAsync(
            Base,
            Chave,
            PedidoMensagem.ModeloAnthropic,
            Midia,
            Imagem,
            maxTokens: 16384);

        using (var doc = JsonDocument.Parse(captura.Corpo))
            Assert.Equal(16384, doc.RootElement.GetProperty("max_tokens").GetInt32());

        var fora = await cliente.LerComprovanteAsync(
            Base,
            Chave,
            PedidoMensagem.ModeloAnthropic,
            Midia,
            Imagem,
            maxTokens: 10);

        Assert.Equal(1, captura.Chamadas);
        Assert.Equal(TipoResultadoMensagem.PedidoInvalido, fora.Tipo);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PedidoMensagem.Montar(PedidoMensagem.ModeloAnthropic, Midia, Imagem, 10));
    }

    [Fact]
    public async Task StopReasonMaxTokens_ViraRespostaCortada()
    {
        const string token = "sk-sp-ficticio-corte-9f3c2a7b1e";
        var base64 = new string('Q', 240);
        var corpo = """
            {"stop_reason":"max_tokens","content":[{"type":"text","text":"{\"legivel\":true"}]}
            """
            + " Authorization: Bearer " + token
            + "\nx-api-key: " + token
            + "\ndata:image/jpeg;base64," + base64
            + "\n" + Imagem;
        var captura = new CapturaHandler(_ => Resposta(HttpStatusCode.OK, corpo));
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.LerComprovanteAsync(Base, token, "deepseek-v4.1-flash-1017", Midia, Imagem);

        Assert.Equal(1, captura.Chamadas);
        Assert.Null(resultado.RetryAfter);
        Assert.Equal(TipoResultadoMensagem.RespostaInvalida, resultado.Tipo);
        Assert.Equal(ResultadoMensagem.TextoRespostaCortada, resultado.Mensagem);
        Assert.Equal("A resposta foi cortada por limite de tokens.", resultado.Mensagem);
        Assert.Null(resultado.Comprovante);
        Assert.NotNull(resultado.Diagnostico);
        Assert.Contains("A resposta foi cortada por limite de tokens.", resultado.Diagnostico.Texto, StringComparison.Ordinal);
        Assert.Contains("stop_reason: max_tokens", resultado.Diagnostico.Texto, StringComparison.Ordinal);
        Assert.Equal("https://api.anthropic.com", resultado.Diagnostico.Origem);
        Assert.DoesNotContain(token, resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain(token, resultado.Diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(token, resultado.Diagnostico.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Imagem, resultado.Diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(base64, resultado.Diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", resultado.Diagnostico.Texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("x-api-key", resultado.Diagnostico.Texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:image", resultado.Diagnostico.Texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/v1/messages", resultado.Diagnostico.Texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Diagnostico_NaoLevaTokenImagemNemCabecalhoNoTextoNemNoLog()
    {
        const string token = "sk-ant-ficticio-alibaba-9f3c2a7b1e";
        var base64 = new string('B', 240);
        var corpo = "falha " + token + "\nAuthorization: Bearer " + token + "\nx-api-key: " + token
            + "\ndata:image/jpeg;base64," + base64 + "\n" + Imagem;
        var captura = new CapturaHandler(_ => Resposta(HttpStatusCode.OK, corpo));
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.LerComprovanteAsync(
            "https://dashscope.aliyuncs.com/apps/anthropic",
            token,
            "deepseek-v4-flash",
            Midia,
            Imagem);

        Assert.Equal(1, captura.Chamadas);
        Assert.Equal(TipoResultadoMensagem.RespostaInvalida, resultado.Tipo);
        Assert.Equal(ResultadoMensagem.TextoRespostaInvalida, resultado.Mensagem);
        Assert.NotNull(resultado.Diagnostico);
        Assert.Equal("https://dashscope.aliyuncs.com", resultado.Diagnostico.Origem);
        Assert.Equal("deepseek-v4-flash", resultado.Diagnostico.Modelo);
        Assert.Contains("JSON inválido", resultado.Diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(token, resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain(token, resultado.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(token, resultado.Diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(token, resultado.Diagnostico.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Imagem, resultado.Diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(base64, resultado.Diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("/apps/", resultado.Diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("x-api-key", resultado.Diagnostico.Texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", resultado.Diagnostico.Texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:image", resultado.Diagnostico.Texto, StringComparison.OrdinalIgnoreCase);

        var log = new StringBuilder();
        RegistroDiagnosticoLeitura.Registrar(resultado.Diagnostico, texto => log.Append(texto));
        Assert.Equal(resultado.Diagnostico.Texto, log.ToString());
        Assert.DoesNotContain(token, log.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Imagem, log.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(base64, log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExcecaoDeRede_NaoEntraNoMotivoNemNaStack()
    {
        HttpRequestException falha;
        try
        {
            throw new HttpRequestException("rastro-ex-tostring-9f3c-nao-pode-vazar");
        }
        catch (HttpRequestException ex)
        {
            falha = ex;
        }

        var captura = new CapturaHandler(_ => throw falha);
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.LerComprovanteAsync(Base, Chave, PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(TipoResultadoMensagem.SemRede, resultado.Tipo);
        Assert.Equal(ResultadoMensagem.TextoSemRede, resultado.Mensagem);
        Assert.NotNull(resultado.Diagnostico);
        Assert.DoesNotContain(falha.Message, resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain(falha.Message, resultado.Diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(falha.ToString(), resultado.Diagnostico.Motivo ?? "", StringComparison.Ordinal);
        Assert.DoesNotContain(falha.ToString(), resultado.Diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpRequestException", resultado.Diagnostico.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("rastro-ex-tostring", resultado.Diagnostico.Texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FalhaDeRede_NaoIncluiEnderecoNemChave()
    {
        var captura = new CapturaHandler(_ =>
            throw new HttpRequestException("falha em " + Base + "/v1/messages com " + Chave));
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.LerComprovanteAsync(Base, Chave, PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(TipoResultadoMensagem.SemRede, resultado.Tipo);
        Assert.Equal(ResultadoMensagem.TextoSemRede, resultado.Mensagem);
        Assert.DoesNotContain(Chave, resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("anthropic", resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("/", resultado.Mensagem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nao-e-url")]
    [InlineData("file:///tmp/comprovante.jpg")]
    [InlineData("https://user:segredo@api.anthropic.com")]
    [InlineData("https://api.anthropic.com/v1?token=segredo")]
    public async Task UrlInvalida_NaoChamaARede(string url)
    {
        var captura = new CapturaHandler(_ => Resposta(HttpStatusCode.OK, CorpoValido()));
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.LerComprovanteAsync(url, Chave, PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(0, captura.Chamadas);
        Assert.Equal(TipoResultadoMensagem.PedidoInvalido, resultado.Tipo);
        Assert.DoesNotContain("segredo", resultado.Mensagem, StringComparison.Ordinal);
        if (url.Length > 0)
            Assert.DoesNotContain(url, resultado.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChaveVaziaOuComQuebraDeLinha_NaoChamaARede()
    {
        var captura = new CapturaHandler(_ => Resposta(HttpStatusCode.OK, CorpoValido()));
        using var cliente = new ClienteMensagens(captura);

        var vazia = await cliente.LerComprovanteAsync(Base, "  ", PedidoMensagem.ModeloAnthropic, Midia, Imagem);
        var injetada = await cliente.LerComprovanteAsync(Base, "chave\r\nX-Extra: 1", PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(0, captura.Chamadas);
        Assert.Equal(ResultadoMensagem.TextoChaveNaoInformada, vazia.Mensagem);
        Assert.Equal("Informe a chave da API.", vazia.Mensagem);
        Assert.NotEqual(ResultadoMensagem.TextoNaoAutorizado, vazia.Mensagem);
        Assert.Equal(TipoResultadoMensagem.NaoAutorizado, injetada.Tipo);
        Assert.Equal(ResultadoMensagem.TextoNaoAutorizado, injetada.Mensagem);
        Assert.DoesNotContain("X-Extra", injetada.Mensagem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Leitura_ChaveAusente_MostraInformeAChave(string? chave)
    {
        var captura = new CapturaHandler(_ => Resposta(HttpStatusCode.OK, CorpoValido()));
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.LerComprovanteAsync(Base, chave, PedidoMensagem.ModeloAnthropic, Midia, Imagem);
        var exibicao = MensagensLeituraComprovante.De(resultado, null);

        Assert.Equal(0, captura.Chamadas);
        Assert.Equal(ResultadoMensagem.TextoChaveNaoInformada, resultado.Mensagem);
        Assert.Equal("Informe a chave da API.", resultado.Mensagem);
        Assert.Equal("Informe a chave da API.", exibicao.Mensagem);
        Assert.NotEqual(ResultadoMensagem.TextoNaoAutorizado, resultado.Mensagem);
        Assert.NotEqual(ResultadoMensagem.TextoNaoAutorizado, exibicao.Mensagem);
        Assert.NotEqual(MensagensLeituraComprovante.TextoSemUrlModeloOuChave, exibicao.Mensagem);
    }

    [Fact]
    public async Task SemPreferencia_LeituraUsaUrlEModeloAlibaba()
    {
        var efetiva = ChipsInteligencia.Efetiva(null, "", "");
        var captura = new CapturaHandler(_ => Resposta(HttpStatusCode.OK, CorpoValido()));
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.LerComprovanteAsync(
            efetiva.Url,
            Chave,
            efetiva.Modelo,
            Midia,
            Imagem);

        Assert.True(resultado.Sucesso);
        Assert.Equal(1, captura.Chamadas);
        Assert.Equal(ChipsInteligencia.UrlAlibaba + "/v1/messages", captura.Uri);
        Assert.Equal(PedidoMensagem.ModeloAlibaba, efetiva.Modelo);
        Assert.DoesNotContain("api.deepseek.com", captura.Uri, StringComparison.OrdinalIgnoreCase);

        using var doc = JsonDocument.Parse(captura.Corpo);
        var raiz = doc.RootElement;
        Assert.Equal(PedidoMensagem.ModeloAlibaba, raiz.GetProperty("model").GetString());
        Assert.Equal("disabled", raiz.GetProperty("thinking").GetProperty("type").GetString());
        Assert.False(raiz.TryGetProperty("output_config", out _));
        Assert.DoesNotContain("output_config", captura.Corpo, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://api.anthropic.com")]
    [InlineData("http://api.anthropic.com/")]
    [InlineData("http://127.0.0.1/v1/messages")]
    [InlineData("HTTP://api.anthropic.com")]
    [InlineData("http://user:segredo@api.anthropic.com")]
    public async Task Http_RecusadoAntesDeEnviarAChave(string url)
    {
        var captura = new CapturaHandler(_ => Resposta(HttpStatusCode.OK, CorpoValido()));
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.LerComprovanteAsync(url, Chave, PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(0, captura.Chamadas);
        Assert.Empty(captura.Chaves);
        Assert.Equal(TipoResultadoMensagem.PedidoInvalido, resultado.Tipo);
        Assert.DoesNotContain(Chave, resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("segredo", resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("http", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(302)]
    [InlineData(307)]
    public async Task Redirect_NaoReenviaChaveNemAceita200DaOutraOrigem(int status)
    {
        const string outra = "https://evil.test/coletor";
        var captura = new CapturaHandler(pedido =>
        {
            if (pedido.RequestUri?.Host == "evil.test")
                return Resposta(HttpStatusCode.OK, CorpoValido());

            var resposta = Resposta((HttpStatusCode)status, CorpoValido());
            resposta.Headers.Location = new Uri(outra);
            resposta.Content = new ConteudoQueFalhaSeLido();
            return resposta;
        });
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.LerComprovanteAsync(Base, Chave, PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(1, captura.Chamadas);
        Assert.Equal("https://api.anthropic.com/v1/messages", Assert.Single(captura.Uris));
        Assert.Equal(Chave, Assert.Single(captura.Chaves));
        Assert.DoesNotContain(captura.Uris, uri => uri.Contains("evil.test", StringComparison.Ordinal));
        Assert.Equal(TipoResultadoMensagem.Falha, resultado.Tipo);
        Assert.Equal(ResultadoMensagem.TextoFalha, resultado.Mensagem);
        Assert.Null(resultado.Comprovante);
        Assert.False(resultado.Sucesso);
        Assert.DoesNotContain("evil.test", resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain(Chave, resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain(Imagem, resultado.Mensagem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(302)]
    [InlineData(307)]
    public async Task CriarHandler_NaoSegueRedirectParaOutraOrigem(int status)
    {
        var portaOrigem = PortaLivre();
        var portaOutra = PortaLivre();
        var outraOrigem = 0;
        using var origem = Escutar(portaOrigem, ctx =>
        {
            ctx.Response.StatusCode = status;
            ctx.Response.Headers["Location"] = $"http://127.0.0.1:{portaOutra}/coletor";
            ctx.Response.ContentLength64 = 0;
        });
        using var outra = Escutar(portaOutra, ctx =>
        {
            Interlocked.Increment(ref outraOrigem);
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentLength64 = 2;
        });

        using var handler = ClienteMensagens.CriarHandler();
        using var invocador = new HttpMessageInvoker(handler, disposeHandler: false);
        using var pedido = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{portaOrigem}/v1/messages");
        pedido.Headers.ExpectContinue = false;
        pedido.Headers.TryAddWithoutValidation(ClienteMensagens.CabecalhoChave, Chave);
        pedido.Content = new StringContent("foto-secreta", Encoding.UTF8, "application/json");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        using var resposta = await invocador.SendAsync(pedido, cts.Token);

        Assert.Equal(status, (int)resposta.StatusCode);
        Assert.Equal(0, outraOrigem);
        Assert.False(handler.AllowAutoRedirect);
    }

    [Theory]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(307)]
    [InlineData(308)]
    public async Task RedirectReal_ChaveECorpoNaoChegamAoSegundoHost(int status)
    {
        var portaOrigem = PortaLivre();
        var portaOutra = PortaLivre();
        var visita = new VisitaRedirect();
        using var origem = Escutar(portaOrigem, (ctx, _) =>
        {
            ctx.Response.StatusCode = status;
            ctx.Response.Headers["Location"] = $"http://127.0.0.1:{portaOutra}/coletor";
            ctx.Response.ContentLength64 = 0;
        });
        using var outra = Escutar(portaOutra, (ctx, corpo) =>
        {
            Interlocked.Increment(ref visita.Pedidos);
            if (string.Equals(ctx.Request.Headers[ClienteMensagens.CabecalhoChave], Chave, StringComparison.Ordinal))
                visita.ViuChave = true;
            if (corpo.Contains(Imagem, StringComparison.Ordinal))
                visita.ViuCorpo = true;

            var bytes = Encoding.UTF8.GetBytes(CorpoValido());
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        });

        var transporte = new SocketsHttpHandler { AllowAutoRedirect = true };
        using var cliente = new ClienteMensagens(transporte, TimeSpan.FromSeconds(5), permiteHttp: true);

        var resultado = await cliente.LerComprovanteAsync(
            $"http://127.0.0.1:{portaOrigem}",
            Chave,
            PedidoMensagem.ModeloAnthropic,
            Midia,
            Imagem);

        Assert.Equal(TipoResultadoMensagem.Falha, resultado.Tipo);
        Assert.False(resultado.Sucesso);
        Assert.Null(resultado.Comprovante);
        Assert.Equal(0, visita.Pedidos);
        Assert.False(visita.ViuChave);
        Assert.False(visita.ViuCorpo);
        Assert.False(transporte.AllowAutoRedirect);
    }

    [Fact]
    public void Imagem_TetoEh5Mb()
    {
        Assert.Equal(5 * 1024 * 1024, ClienteMensagens.TamanhoMaximoImagem);
        Assert.True(ClienteMensagens.ImagemCabe(Base64De(ClienteMensagens.TamanhoMaximoImagem)));
        Assert.False(ClienteMensagens.ImagemCabe(Base64De(ClienteMensagens.TamanhoMaximoImagem + 1)));
    }

    [Fact]
    public async Task ImagemAcimaDe5Mb_NaoEnviaChave()
    {
        var captura = new CapturaHandler(_ => Resposta(HttpStatusCode.OK, CorpoValido()));
        using var cliente = new ClienteMensagens(captura);
        var imagem = Base64De(ClienteMensagens.TamanhoMaximoImagem + 1);

        var resultado = await cliente.LerComprovanteAsync(Base, Chave, PedidoMensagem.ModeloAnthropic, Midia, imagem);

        Assert.Equal(0, captura.Chamadas);
        Assert.Empty(captura.Chaves);
        Assert.Equal(TipoResultadoMensagem.PedidoInvalido, resultado.Tipo);
        Assert.DoesNotContain(Chave, resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("AAAA", resultado.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CorpoAnunciadoAcimaDoTeto_NaoELidoNemViraComprovante()
    {
        var captura = new CapturaHandler(_ =>
        {
            var resposta = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ConteudoQueFalhaSeLido { Comprimento = ClienteMensagens.TamanhoMaximoCorpo + 1 }
            };
            return resposta;
        });
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.LerComprovanteAsync(Base, Chave, PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(TipoResultadoMensagem.RespostaInvalida, resultado.Tipo);
        Assert.Equal(ResultadoMensagem.TextoRespostaInvalida, resultado.Mensagem);
        Assert.Null(resultado.Comprovante);
        Assert.DoesNotContain("corpo-secreto", resultado.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CorpoSemTamanhoAcimaDoTeto_NaoViraComprovante()
    {
        var captura = new CapturaHandler(_ =>
        {
            var dados = new byte[ClienteMensagens.TamanhoMaximoCorpo + 1];
            Encoding.UTF8.GetBytes("corpo-secreto").CopyTo(dados, 0);
            var resposta = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new FluxoSemBusca(new MemoryStream(dados)))
            };
            return resposta;
        });
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.LerComprovanteAsync(Base, Chave, PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(TipoResultadoMensagem.RespostaInvalida, resultado.Tipo);
        Assert.Null(resultado.Comprovante);
        Assert.DoesNotContain("corpo-secreto", resultado.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Limite_RetryAfterLongoFicaEmAlgunsMinutos()
    {
        var captura = new CapturaHandler(_ =>
        {
            var resposta = Resposta((HttpStatusCode)429, "nao-ler");
            resposta.Headers.TryAddWithoutValidation("Retry-After", "86400");
            return resposta;
        });
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.LerComprovanteAsync(Base, Chave, PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(TipoResultadoMensagem.Limite, resultado.Tipo);
        Assert.Equal(TimeSpan.FromMinutes(5), ClienteMensagens.RetryAfterMaximo);
        Assert.Equal(ClienteMensagens.RetryAfterMaximo, resultado.RetryAfter);
        Assert.DoesNotContain("86400", resultado.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Limite_RetryAfterEmDataDistanteFicaNoTeto()
    {
        var quando = DateTimeOffset.UtcNow.AddDays(2);
        var captura = new CapturaHandler(_ =>
        {
            var resposta = Resposta((HttpStatusCode)429, "nao-ler");
            resposta.Headers.TryAddWithoutValidation("Retry-After", quando.ToString("r", CultureInfo.InvariantCulture));
            return resposta;
        });
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.LerComprovanteAsync(Base, Chave, PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(ClienteMensagens.RetryAfterMaximo, resultado.RetryAfter);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    public async Task ErroDoServidor_ViraFalhaSemEcoarCorpoNemRepetir(int status)
    {
        var captura = new CapturaHandler(_ => Resposta((HttpStatusCode)status, "corpo-secreto " + Chave + " " + Imagem));
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.LerComprovanteAsync(Base, Chave, PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(1, captura.Chamadas);
        Assert.Equal(TipoResultadoMensagem.Falha, resultado.Tipo);
        Assert.Equal(ResultadoMensagem.TextoFalha, resultado.Mensagem);
        Assert.Null(resultado.RetryAfter);
        Assert.Null(resultado.Comprovante);
        Assert.DoesNotContain("corpo-secreto", resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain(Chave, resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain(Imagem, resultado.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GatewayComCaminho_PreservaOPrefixo()
    {
        var captura = new CapturaHandler(_ => Resposta(HttpStatusCode.OK, CorpoValido()));
        using var cliente = new ClienteMensagens(captura);

        await cliente.LerComprovanteAsync(
            "https://exemplo.test/gateway/",
            Chave,
            PedidoMensagem.ModeloAnthropic,
            Midia,
            Imagem);

        Assert.Equal("https://exemplo.test/gateway/v1/messages", captura.Uri);
    }

    [Fact]
    public void ToString_NaoExpoeConteudo()
    {
        var item = new ItemComprovanteLido("ARROZ", "Arroz", true, 10m, "1", "un", 0.5m, ConfiancaLeitura.Alta);
        var comprovante = new ComprovanteLido("Mercado", new DateOnly(2026, 10, 18), 10m, true, [item]);
        var resultado = ResultadoMensagem.Ok(comprovante);

        Assert.DoesNotContain("ARROZ", item.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Mercado", comprovante.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("ARROZ", resultado.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Mercado", resultado.ToString(), StringComparison.Ordinal);
    }

    private static void AssertSemAmostragem(JsonElement elemento)
    {
        switch (elemento.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var propriedade in elemento.EnumerateObject())
                {
                    Assert.NotEqual("temperature", propriedade.Name);
                    Assert.NotEqual("top_p", propriedade.Name);
                    AssertSemAmostragem(propriedade.Value);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in elemento.EnumerateArray())
                    AssertSemAmostragem(item);
                break;
        }
    }

    private static string[] Nomes(JsonElement array) =>
        array.EnumerateArray().Select(item => item.GetString()!).ToArray();

    private static string CorpoValido() => """
        {
          "content": [
            { "type": "thinking", "thinking": "ignorar" },
            {
              "type": "tool_use",
              "name": "registrar_comprovante",
              "input": { "legivel": true, "itens": [] }
            }
          ]
        }
        """;

    private static HttpResponseMessage Resposta(HttpStatusCode status, string corpo) =>
        new(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task TesteDeConexao_MontaPedidoMinimoSemFotoNemChaveNoCorpo()
    {
        const string corpoResposta = "segredo-corpo " + Chave;
        var captura = new CapturaHandler(_ => Resposta(HttpStatusCode.OK, corpoResposta));
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.TestarConexaoAsync(Base + "/", Chave, "  " + PedidoMensagem.ModeloAnthropic + "  ");

        Assert.True(resultado.Sucesso);
        Assert.Equal(1, captura.Chamadas);
        Assert.Equal("POST", captura.Metodo);
        Assert.Equal("https://api.anthropic.com/v1/messages", captura.Uri);
        Assert.Equal(Chave, captura.Chave);
        Assert.Equal(ClienteMensagens.VersaoAnthropic, captura.Versao);
        Assert.StartsWith("application/json", captura.TipoConteudo, StringComparison.Ordinal);
        Assert.DoesNotContain(Chave, captura.Uri, StringComparison.Ordinal);
        Assert.DoesNotContain(Chave, captura.Corpo, StringComparison.Ordinal);
        Assert.Equal(ResultadoMensagem.TextoConexaoOk, resultado.Mensagem);
        Assert.Equal(ResultadoMensagem.TextoConexaoOk, ClienteMensagens.TextoExibicao(resultado, Chave));
        Assert.DoesNotContain(Chave, resultado.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(corpoResposta, resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain(Chave, ClienteMensagens.TextoExibicao(resultado, Chave), StringComparison.Ordinal);
        Assert.Null(resultado.Comprovante);

        using var doc = JsonDocument.Parse(captura.Corpo);
        var raiz = doc.RootElement;
        Assert.Equal(PedidoMensagem.ModeloAnthropic, raiz.GetProperty("model").GetString());
        Assert.Equal(1, raiz.GetProperty("max_tokens").GetInt32());
        Assert.False(raiz.TryGetProperty("system", out _));
        Assert.False(raiz.TryGetProperty("tools", out _));
        Assert.False(raiz.TryGetProperty("tool_choice", out _));
        Assert.False(raiz.TryGetProperty("thinking", out _));
        Assert.DoesNotContain("\"thinking\"", captura.Corpo, StringComparison.Ordinal);
        AssertSemAmostragem(raiz);

        var mensagem = Assert.Single(raiz.GetProperty("messages").EnumerateArray());
        Assert.Equal("user", mensagem.GetProperty("role").GetString());
        Assert.Equal("ping", mensagem.GetProperty("content").GetString());
        Assert.DoesNotContain("image", captura.Corpo, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://token-plan.maas.qwencloudapi.com/apps/anthropic", "deepseek-v4.1-flash-1017", true)]
    [InlineData("https://token-plan.maas.qwencloudapi.com./apps/anthropic", "deepseek-v4.1-flash-1017", true)]
    [InlineData("https://maas.qwencloudapi.com/apps/anthropic", "qwen3.7-plus", true)]
    [InlineData("https://foo.qwencloudapi.com/apps/anthropic", "qwen3.7-flash", true)]
    [InlineData("https://QWENCLOUDAPI.COM/v1", "qwen3-vl-plus", true)]
    [InlineData("https://Token-Plan.MAAS.QwenCloudApi.Com:8443/apps/anthropic", "deepseek-v4.1-flash-1017", true)]
    [InlineData("https://qwencloudapi.com.evil.com/apps/anthropic", "deepseek-v4.1-flash-1017", false)]
    [InlineData("https://qwencloudapi.com.evil.com./apps/anthropic", "deepseek-v4.1-flash-1017", false)]
    [InlineData("https://notqwencloudapi.com./apps/anthropic", "qwen3.7-plus", false)]
    [InlineData("https://notqwencloudapi.com/apps/anthropic", "qwen3.7-plus", false)]
    [InlineData("https://maas.qwencloudapi.com.evil.com/apps/anthropic", "qwen3-vl-plus", false)]
    [InlineData("https://api.deepseek.com/anthropic", "deepseek-flash", true)]
    [InlineData("https://API.DEEPSEEK.COM/anthropic", "deepseek-flash", true)]
    [InlineData("https://api.anthropic.com", "claude-haiku-5-5", false)]
    [InlineData("https://dashscope.aliyuncs.com", "deepseek-flash", false)]
    [InlineData("https://api.deepseek.com.evil.com", "deepseek-flash", false)]
    public async Task Leitura_ThinkingNoHost_MantemToolChoiceEChave(
        string url,
        string modelo,
        bool desliga)
    {
        var captura = new CapturaHandler(_ => Resposta(HttpStatusCode.OK, CorpoValido()));
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.LerComprovanteAsync(url, Chave, modelo, Midia, Imagem);

        Assert.True(resultado.Sucesso);
        Assert.Equal(1, captura.Chamadas);
        Assert.Equal(Chave, captura.Chave);
        Assert.Equal("x-api-key", ClienteMensagens.CabecalhoChave);
        Assert.Equal(ClienteMensagens.VersaoAnthropic, captura.Versao);
        Assert.DoesNotContain(Chave, captura.Corpo, StringComparison.Ordinal);
        Assert.DoesNotContain(Chave, captura.Uri, StringComparison.Ordinal);

        using var doc = JsonDocument.Parse(captura.Corpo);
        var raiz = doc.RootElement;
        Assert.Equal(modelo, raiz.GetProperty("model").GetString());
        Assert.Equal(PedidoMensagem.MaxTokens, raiz.GetProperty("max_tokens").GetInt32());
        Assert.Equal(PedidoMensagem.CriarUri(url)!.AbsoluteUri, captura.Uri);
        var escolha = raiz.GetProperty("tool_choice");
        Assert.Equal("tool", escolha.GetProperty("type").GetString());
        Assert.Equal(PedidoMensagem.NomeFerramenta, escolha.GetProperty("name").GetString());
        Assert.False(raiz.TryGetProperty("output_config", out _));
        Assert.DoesNotContain("output_config", captura.Corpo, StringComparison.Ordinal);
        AssertThinking(raiz, captura.Corpo, desliga);
    }

    [Theory]
    [InlineData("https://api.deepseek.com/anthropic/", true)]
    [InlineData("https://API.DEEPSEEK.COM/anthropic", true)]
    [InlineData("https://token-plan.maas.qwencloudapi.com/apps/anthropic", true)]
    [InlineData("https://foo.qwencloudapi.com", true)]
    [InlineData("https://api.anthropic.com/", false)]
    [InlineData("https://dashscope.aliyuncs.com", false)]
    [InlineData("https://api.deepseek.com.evil.com/anthropic", false)]
    [InlineData("https://qwencloudapi.com.evil.com", false)]
    public async Task TesteDeConexao_ThinkingSegueOHost(string url, bool desliga)
    {
        var captura = new CapturaHandler(_ => Resposta(HttpStatusCode.OK, "ok"));
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.TestarConexaoAsync(url, Chave, "  " + PedidoMensagem.ModeloDeepSeek + "  ");

        Assert.True(resultado.Sucesso);
        Assert.Equal(1, captura.Chamadas);
        Assert.Equal(Chave, captura.Chave);
        Assert.Equal("x-api-key", ClienteMensagens.CabecalhoChave);
        Assert.Equal(ClienteMensagens.VersaoAnthropic, captura.Versao);
        Assert.DoesNotContain(Chave, captura.Corpo, StringComparison.Ordinal);

        using var doc = JsonDocument.Parse(captura.Corpo);
        var raiz = doc.RootElement;
        Assert.Equal(PedidoMensagem.ModeloDeepSeek, raiz.GetProperty("model").GetString());
        Assert.Equal(1, raiz.GetProperty("max_tokens").GetInt32());
        Assert.False(raiz.TryGetProperty("tools", out _));
        Assert.False(raiz.TryGetProperty("tool_choice", out _));
        Assert.False(raiz.TryGetProperty("system", out _));
        AssertThinking(raiz, captura.Corpo, desliga);
    }

    private static void AssertThinking(JsonElement raiz, string corpo, bool desliga)
    {
        if (!desliga)
        {
            Assert.False(raiz.TryGetProperty("thinking", out _));
            Assert.DoesNotContain("\"thinking\"", corpo, StringComparison.Ordinal);
            return;
        }

        var thinking = raiz.GetProperty("thinking");
        Assert.Equal(JsonValueKind.Object, thinking.ValueKind);
        Assert.Equal("disabled", thinking.GetProperty("type").GetString());
        Assert.Equal(["type"], thinking.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Contains("\"type\":\"disabled\"", corpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TesteDeConexao_401_MostraRecusaDaChaveSemRepetir()
    {
        var captura = new CapturaHandler(_ => Resposta(HttpStatusCode.Unauthorized, "corpo " + Chave));
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.TestarConexaoAsync(Base, Chave, PedidoMensagem.ModeloAnthropic);

        Assert.Equal(1, captura.Chamadas);
        Assert.Equal(TipoResultadoMensagem.NaoAutorizado, resultado.Tipo);
        Assert.Equal(ResultadoMensagem.TextoNaoAutorizado, ClienteMensagens.TextoExibicao(resultado, Chave));
        Assert.DoesNotContain(Chave, ClienteMensagens.TextoExibicao(resultado, Chave), StringComparison.Ordinal);
        Assert.DoesNotContain(Chave, resultado.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TesteDeConexao_SemRede_NaoEcoaEnderecoNemChave()
    {
        var captura = new CapturaHandler(_ =>
            throw new HttpRequestException("falha em " + Base + " com " + Chave));
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.TestarConexaoAsync(Base, Chave, PedidoMensagem.ModeloAnthropic);

        Assert.Equal(TipoResultadoMensagem.SemRede, resultado.Tipo);
        Assert.Equal(ResultadoMensagem.TextoSemRede, ClienteMensagens.TextoExibicao(resultado, Chave));
        Assert.DoesNotContain(Chave, resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("anthropic", resultado.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TesteDeConexao_Timeout_UsaOTokenENaoAlteraOTimeoutDoCliente()
    {
        var captura = new CapturaHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Resposta(HttpStatusCode.OK, "corpo " + Chave);
        });
        using var cliente = new ClienteMensagens(captura, TimeSpan.FromMilliseconds(200));

        var resultado = await cliente.TestarConexaoAsync(Base, Chave, PedidoMensagem.ModeloAnthropic);

        Assert.Equal(TipoResultadoMensagem.Timeout, resultado.Tipo);
        Assert.Equal(ResultadoMensagem.TextoTimeout, ClienteMensagens.TextoExibicao(resultado, Chave));
        Assert.Equal(Timeout.InfiniteTimeSpan, cliente.TimeoutDoTransporte);
        Assert.DoesNotContain(Chave, resultado.Mensagem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://api.anthropic.com")]
    [InlineData("https://user:segredo@api.anthropic.com")]
    public async Task TesteDeConexao_UrlInsegura_NaoChamaARede(string url)
    {
        var captura = new CapturaHandler(_ => Resposta(HttpStatusCode.OK, "ok"));
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.TestarConexaoAsync(url, Chave, PedidoMensagem.ModeloAnthropic);

        Assert.Equal(0, captura.Chamadas);
        Assert.Equal(TipoResultadoMensagem.PedidoInvalido, resultado.Tipo);
        Assert.Equal(ResultadoMensagem.TextoUrlInvalida, resultado.Mensagem);
        Assert.DoesNotContain(Chave, resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("segredo", resultado.Mensagem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TesteDeConexao_ChaveVazia_NaoChamaARede(string? chave)
    {
        var captura = new CapturaHandler(_ => Resposta(HttpStatusCode.Unauthorized, "corpo"));
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.TestarConexaoAsync(Base, chave, PedidoMensagem.ModeloAnthropic);

        Assert.Equal(0, captura.Chamadas);
        Assert.Equal(ResultadoMensagem.TextoChaveNaoInformada, resultado.Mensagem);
        Assert.NotEqual(ResultadoMensagem.TextoNaoAutorizado, resultado.Mensagem);
        Assert.DoesNotContain("chave-teste", resultado.Mensagem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TesteDeConexao_ModeloVazio_NaoChamaARede(string? modelo)
    {
        var captura = new CapturaHandler(_ => Resposta(HttpStatusCode.OK, "ok"));
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.TestarConexaoAsync(Base, Chave, modelo);

        Assert.Equal(0, captura.Chamadas);
        Assert.Equal(ResultadoMensagem.TextoModeloNaoInformado, resultado.Mensagem);
        Assert.DoesNotContain(Chave, resultado.Mensagem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TesteDeConexao_UrlVazia_NaoChamaARede(string? url)
    {
        var captura = new CapturaHandler(_ => Resposta(HttpStatusCode.OK, "ok"));
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.TestarConexaoAsync(url, Chave, PedidoMensagem.ModeloAnthropic);

        Assert.Equal(0, captura.Chamadas);
        Assert.Equal(ResultadoMensagem.TextoUrlNaoInformada, resultado.Mensagem);
        Assert.DoesNotContain(Chave, resultado.Mensagem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://api.anthropic.com/v1?token=segredo")]
    [InlineData("https://api.anthropic.com/v1#segredo")]
    [InlineData("http://api.anthropic.com")]
    [InlineData("https://user:segredo@api.anthropic.com")]
    public async Task TesteDeConexao_UrlComQueryOuInvalida_NaoChamaARede(string url)
    {
        var captura = new CapturaHandler(_ => Resposta(HttpStatusCode.OK, "ok"));
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.TestarConexaoAsync(url, Chave, PedidoMensagem.ModeloAnthropic);

        Assert.Equal(0, captura.Chamadas);
        Assert.Equal(ResultadoMensagem.TextoUrlInvalida, resultado.Mensagem);
        Assert.NotEqual(ResultadoMensagem.TextoPedidoInvalido, resultado.Mensagem);
        Assert.DoesNotContain(Chave, resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("segredo", resultado.Mensagem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(302)]
    [InlineData(307)]
    public async Task TesteDeConexao_RedirectNaoESeguido(int status)
    {
        const string outra = "https://evil.test/coletor";
        var captura = new CapturaHandler(pedido =>
        {
            if (pedido.RequestUri?.Host == "evil.test")
                return Resposta(HttpStatusCode.OK, "ok " + Chave);

            var resposta = Resposta((HttpStatusCode)status, "corpo " + Chave);
            resposta.Headers.Location = new Uri(outra);
            resposta.Content = new ConteudoQueFalhaSeLido();
            return resposta;
        });
        using var cliente = new ClienteMensagens(captura);

        var resultado = await cliente.TestarConexaoAsync(Base, Chave, PedidoMensagem.ModeloAnthropic);

        Assert.Equal(1, captura.Chamadas);
        Assert.Equal("https://api.anthropic.com/v1/messages", Assert.Single(captura.Uris));
        Assert.DoesNotContain(captura.Uris, uri => uri.Contains("evil.test", StringComparison.Ordinal));
        Assert.Equal(TipoResultadoMensagem.Falha, resultado.Tipo);
        Assert.Equal(ResultadoMensagem.TextoFalha, resultado.Mensagem);
        Assert.False(resultado.Sucesso);
        Assert.DoesNotContain(Chave, resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("evil.test", resultado.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void FrasesDoTesteDeConexao_SaoDistintasENaoCarregamChave()
    {
        string[] frases =
        [
            ResultadoMensagem.TextoChaveNaoInformada,
            ResultadoMensagem.TextoUrlNaoInformada,
            ResultadoMensagem.TextoModeloNaoInformado,
            ResultadoMensagem.TextoUrlInvalida,
            ResultadoMensagem.TextoNaoAutorizado,
            ResultadoMensagem.TextoSemPermissao,
            ResultadoMensagem.TextoSemRede,
            ResultadoMensagem.TextoTimeout,
            ResultadoMensagem.TextoPedidoInvalido
        ];

        Assert.Equal(frases.Length, frases.Distinct(StringComparer.Ordinal).Count());
        foreach (var frase in frases)
        {
            Assert.DoesNotContain(Chave, frase, StringComparison.Ordinal);
            Assert.DoesNotContain("segredo", frase, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("a")]
    [InlineData("API")]
    [InlineData("de")]
    [InlineData(" ")]
    public void TextoExibicao_ChaveCurta_NaoTrocaFraseFixa(string segredo)
    {
        var frases = new[]
        {
            ResultadoMensagem.TextoChaveNaoInformada,
            ResultadoMensagem.TextoUrlNaoInformada,
            ResultadoMensagem.TextoModeloNaoInformado,
            ResultadoMensagem.TextoUrlInvalida,
            ResultadoMensagem.TextoNaoAutorizado,
            ResultadoMensagem.TextoSemPermissao,
            ResultadoMensagem.TextoSemRede,
            ResultadoMensagem.TextoTimeout
        };

        foreach (var frase in frases)
        {
            var resultado = ResultadoMensagem.Falha(TipoResultadoMensagem.PedidoInvalido, frase);
            Assert.Equal(frase, ClienteMensagens.TextoExibicao(resultado, segredo));
        }
    }

    [Fact]
    public void PreparoChamada_ToStringNaoExpoeChave()
    {
        var tipo = typeof(ClienteMensagens).GetNestedType("PreparoChamada", System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(tipo);
        var ctor = tipo!.GetConstructor([typeof(Uri), typeof(string), typeof(ResultadoMensagem)]);
        Assert.NotNull(ctor);
        var valor = ctor!.Invoke([new Uri("https://api.anthropic.com/v1/messages"), Chave, null]);

        var texto = valor.ToString();

        Assert.Equal("PreparoChamada", texto);
        Assert.DoesNotContain(Chave, texto!, StringComparison.Ordinal);
        Assert.DoesNotContain("anthropic", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void TextoExibicao_DescartaMensagemQueCarregaAChave()
    {
        var sujo = new ResultadoMensagem(TipoResultadoMensagem.Falha, "falhou " + Chave);

        var texto = ClienteMensagens.TextoExibicao(sujo, Chave);

        Assert.Equal(ResultadoMensagem.TextoFalha, texto);
        Assert.DoesNotContain(Chave, texto, StringComparison.Ordinal);
    }

    private sealed class CapturaHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        public CapturaHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            : this((pedido, _) => Task.FromResult(responder(pedido)))
        {
        }

        public CapturaHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        public int Chamadas { get; private set; }
        public string Metodo { get; private set; } = string.Empty;
        public string Uri { get; private set; } = string.Empty;
        public string? Chave { get; private set; }
        public string? Versao { get; private set; }
        public string? TipoConteudo { get; private set; }
        public string Corpo { get; private set; } = string.Empty;
        public List<string?> Chaves { get; } = [];
        public List<string> Uris { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Chamadas++;
            Metodo = request.Method.Method;
            Uri = request.RequestUri?.AbsoluteUri ?? string.Empty;
            Uris.Add(Uri);
            Chave = request.Headers.TryGetValues(ClienteMensagens.CabecalhoChave, out var chaves)
                ? chaves.Single()
                : null;
            Versao = request.Headers.TryGetValues(ClienteMensagens.CabecalhoVersao, out var versoes)
                ? versoes.Single()
                : null;
            Chaves.Add(Chave);
            TipoConteudo = request.Content?.Headers.ContentType?.MediaType;
            Corpo = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return await _responder(request, cancellationToken);
        }
    }

    private static string Base64De(int bytes)
    {
        var resto = bytes % 3;
        var chars = ((bytes + 2) / 3) * 4;
        var texto = new string('A', chars);
        if (resto == 1)
            return string.Concat(texto.AsSpan(0, chars - 2), "==");
        if (resto == 2)
            return string.Concat(texto.AsSpan(0, chars - 1), "=");
        return texto;
    }

    private static int PortaLivre()
    {
        var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        var porta = ((IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();
        return porta;
    }

    private static HttpListener Escutar(int porta, Action<HttpListenerContext> responder) =>
        Escutar(porta, (contexto, _) => responder(contexto));

    private static HttpListener Escutar(int porta, Action<HttpListenerContext, string> responder)
    {
        var escuta = new HttpListener();
        escuta.Prefixes.Add($"http://127.0.0.1:{porta}/");
        escuta.Start();
        _ = Task.Run(async () =>
        {
            while (escuta.IsListening)
            {
                HttpListenerContext contexto;
                try
                {
                    contexto = await escuta.GetContextAsync();
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                try
                {
                    var corpo = await LerPedidoAsync(contexto.Request);
                    responder(contexto, corpo);
                }
                finally
                {
                    contexto.Response.Close();
                }
            }
        });
        return escuta;
    }

    private static async Task<string> LerPedidoAsync(HttpListenerRequest pedido)
    {
        using var memoria = new MemoryStream();
        var buffer = new byte[4096];
        while (memoria.Length < 65536)
        {
            var lidos = await pedido.InputStream.ReadAsync(buffer);
            if (lidos == 0)
                break;

            memoria.Write(buffer, 0, lidos);
        }

        return Encoding.UTF8.GetString(memoria.GetBuffer(), 0, (int)memoria.Length);
    }

    private sealed class VisitaRedirect
    {
        public int Pedidos;
        public bool ViuChave;
        public bool ViuCorpo;
    }

    private sealed class FluxoSemBusca : Stream
    {
        private readonly Stream _interno;

        public FluxoSemBusca(Stream interno) => _interno = interno;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => _interno.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _interno.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _interno.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class ConteudoQueFalhaSeLido : HttpContent
    {
        public long? Comprimento { get; init; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            throw new InvalidOperationException("corpo-secreto");

        protected override bool TryComputeLength(out long length)
        {
            if (Comprimento is long valor)
            {
                length = valor;
                return true;
            }

            length = 0;
            return false;
        }
    }
}
