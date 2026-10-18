using System.Globalization;
using System.Net;
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
        using var http = new HttpClient(captura);
        var cliente = new ClienteMensagens(http);

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
        Assert.False(http.DefaultRequestHeaders.Contains(ClienteMensagens.CabecalhoChave));
        Assert.False(http.DefaultRequestHeaders.Contains(ClienteMensagens.CabecalhoVersao));
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
        using var http = new HttpClient(captura);
        var cliente = new ClienteMensagens(http);

        await cliente.LerComprovanteAsync(Base, "chave-um", PedidoMensagem.ModeloAnthropic, Midia, Imagem);
        await cliente.LerComprovanteAsync(Base, "chave-dois", PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(["chave-um", "chave-dois"], captura.Chaves);
        Assert.False(http.DefaultRequestHeaders.Contains(ClienteMensagens.CabecalhoChave));
    }

    [Theory]
    [InlineData(401, TipoResultadoMensagem.NaoAutorizado, ResultadoMensagem.TextoNaoAutorizado)]
    [InlineData(429, TipoResultadoMensagem.Limite, ResultadoMensagem.TextoLimite)]
    [InlineData(529, TipoResultadoMensagem.Sobrecarga, ResultadoMensagem.TextoSobrecarga)]
    [InlineData(504, TipoResultadoMensagem.Timeout, ResultadoMensagem.TextoTimeout)]
    [InlineData(400, TipoResultadoMensagem.PedidoInvalido, ResultadoMensagem.TextoPedidoInvalido)]
    public async Task TraduzStatusHttp(int status, TipoResultadoMensagem tipo, string mensagem)
    {
        var captura = new CapturaHandler(_ => Resposta((HttpStatusCode)status, "corpo-secreto " + Chave + " " + Imagem));
        using var http = new HttpClient(captura);
        var cliente = new ClienteMensagens(http);

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
    public void MensagensDeLimiteESobrecargaSaoDistintas()
    {
        Assert.NotEqual(ResultadoMensagem.TextoLimite, ResultadoMensagem.TextoSobrecarga);
        Assert.NotEqual(ResultadoMensagem.TextoTimeout, ResultadoMensagem.TextoLimite);
        Assert.NotEqual(ResultadoMensagem.TextoNaoAutorizado, ResultadoMensagem.TextoPedidoInvalido);
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
        using var http = new HttpClient(captura);
        var cliente = new ClienteMensagens(http);

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
        using var http = new HttpClient(captura);
        var cliente = new ClienteMensagens(http);

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
        using var http = new HttpClient(captura);
        var cliente = new ClienteMensagens(http);

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
        using var http = new HttpClient(captura);
        var cliente = new ClienteMensagens(http);

        var resultado = await cliente.LerComprovanteAsync(Base, Chave, PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(TipoResultadoMensagem.Limite, resultado.Tipo);
        Assert.Null(resultado.RetryAfter);
    }

    [Fact]
    public async Task TimeoutDoTransporte_ViraTimeoutSemVazarDetalhe()
    {
        var captura = new CapturaHandler(_ => throw new TaskCanceledException("estourou " + Chave + " " + Base + " " + Imagem));
        using var http = new HttpClient(captura);
        var cliente = new ClienteMensagens(http);

        var resultado = await cliente.LerComprovanteAsync(Base, Chave, PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(TipoResultadoMensagem.Timeout, resultado.Tipo);
        Assert.Equal(ResultadoMensagem.TextoTimeout, resultado.Mensagem);
        Assert.DoesNotContain(Chave, resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain(Imagem, resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain(Base, resultado.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrazoProprio_SubstituiTimeoutDe15Segundos()
    {
        var captura = new CapturaHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Resposta(HttpStatusCode.OK, CorpoValido());
        });
        using var http = new HttpClient(captura) { Timeout = TimeSpan.FromSeconds(15) };
        var cliente = new ClienteMensagens(http, TimeSpan.FromMilliseconds(200));

        var resultado = await cliente.LerComprovanteAsync(Base, Chave, PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(TipoResultadoMensagem.Timeout, resultado.Tipo);
        Assert.Equal(TimeSpan.FromMilliseconds(200), http.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(60), ClienteMensagens.TempoLimite);
        Assert.True(ClienteMensagens.TempoLimite > TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void Construtor_AplicaTimeoutProprioMaiorQue15Segundos()
    {
        using var http = new HttpClient(new CapturaHandler(_ => Resposta(HttpStatusCode.OK, "{}")))
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        _ = new ClienteMensagens(http);

        Assert.Equal(ClienteMensagens.TempoLimite, http.Timeout);
        Assert.True(http.Timeout > TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task CancelamentoDoChamador_NaoViraTimeout()
    {
        var captura = new CapturaHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Resposta(HttpStatusCode.OK, CorpoValido());
        });
        using var http = new HttpClient(captura);
        var cliente = new ClienteMensagens(http);
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
        using var http = new HttpClient(captura);
        var cliente = new ClienteMensagens(http);

        var resultado = await cliente.LerComprovanteAsync(Base, Chave, PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(TipoResultadoMensagem.RespostaInvalida, resultado.Tipo);
        Assert.Equal(ResultadoMensagem.TextoRespostaInvalida, resultado.Mensagem);
        Assert.DoesNotContain("segredo-corpo", resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain(Imagem, resultado.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FalhaDeRede_NaoIncluiEnderecoNemChave()
    {
        var captura = new CapturaHandler(_ =>
            throw new HttpRequestException("falha em " + Base + "/v1/messages com " + Chave));
        using var http = new HttpClient(captura);
        var cliente = new ClienteMensagens(http);

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
        using var http = new HttpClient(captura);
        var cliente = new ClienteMensagens(http);

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
        using var http = new HttpClient(captura);
        var cliente = new ClienteMensagens(http);

        var vazia = await cliente.LerComprovanteAsync(Base, "  ", PedidoMensagem.ModeloAnthropic, Midia, Imagem);
        var injetada = await cliente.LerComprovanteAsync(Base, "chave\r\nX-Extra: 1", PedidoMensagem.ModeloAnthropic, Midia, Imagem);

        Assert.Equal(0, captura.Chamadas);
        Assert.Equal(TipoResultadoMensagem.NaoAutorizado, vazia.Tipo);
        Assert.Equal(TipoResultadoMensagem.NaoAutorizado, injetada.Tipo);
        Assert.DoesNotContain("X-Extra", injetada.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GatewayComCaminho_PreservaOPrefixo()
    {
        var captura = new CapturaHandler(_ => Resposta(HttpStatusCode.OK, CorpoValido()));
        using var http = new HttpClient(captura);
        var cliente = new ClienteMensagens(http);

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

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Chamadas++;
            Metodo = request.Method.Method;
            Uri = request.RequestUri?.AbsoluteUri ?? string.Empty;
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
}
