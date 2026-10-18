using System.Globalization;
using System.Net;
using System.Text;

namespace OrcaFacil.Core.Llm;

/// <summary>
/// Cliente de <c>POST {base}/v1/messages</c>. A produção nasce em <see cref="Criar"/>,
/// com redirect desligado. A chave entra só no header de cada pedido HTTPS.
/// Não grava chave, header, corpo, imagem nem caminho.
/// </summary>
public sealed class ClienteMensagens : IDisposable
{
    public const string VersaoAnthropic = "2023-06-01";
    public const string CabecalhoChave = "x-api-key";
    public const string CabecalhoVersao = "anthropic-version";

    /// <summary>Binário da imagem. A API corta em 5 MB.</summary>
    public const int TamanhoMaximoImagem = 5 * 1024 * 1024;

    /// <summary>JSON da resposta. Acima disso o corpo é descartado sem ser lido por inteiro.</summary>
    public const int TamanhoMaximoCorpo = 1024 * 1024;

    /// <summary>Prazo próprio da leitura, maior que o timeout de 15 s do cliente de notícias.</summary>
    public static readonly TimeSpan TempoLimite = TimeSpan.FromSeconds(60);

    /// <summary>Teto de <c>Retry-After</c>. Um valor maior é reduzido a este prazo.</summary>
    public static readonly TimeSpan RetryAfterMaximo = TimeSpan.FromMinutes(5);

    private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;

    private readonly HttpClient _http;
    private readonly TimeSpan _tempoLimite;
    private readonly bool _permiteHttp;

    /// <summary>
    /// Ponto de teste. Http só entra com <paramref name="permiteHttp"/>.
    /// O redirect é desligado aqui, mesmo que o handler recebido o siga.
    /// A produção usa <see cref="Criar"/>, que não aceita um cliente de fora.
    /// </summary>
    internal ClienteMensagens(HttpMessageHandler transporte, TimeSpan? tempoLimite = null, bool permiteHttp = false)
    {
        ArgumentNullException.ThrowIfNull(transporte);
        var limite = tempoLimite ?? TempoLimite;
        if (limite <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(tempoLimite));

        ImpedirRedirect(transporte);
        _tempoLimite = limite;
        _permiteHttp = permiteHttp;
        _http = new HttpClient(transporte, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    /// <summary>Cliente de produção: HTTPS, sem redirect, prazo só no token da chamada.</summary>
    public static ClienteMensagens Criar(TimeSpan? tempoLimite = null) =>
        new(CriarHandler(), tempoLimite, permiteHttp: false);

    /// <summary>Handler sem redirect automático. O prazo de 60 s fica no token da chamada, não aqui.</summary>
    public static SocketsHttpHandler CriarHandler()
    {
        var handler = new SocketsHttpHandler();
        ImpedirRedirect(handler);
        return handler;
    }

    /// <summary>
    /// Transporte de <see cref="CriarHandler"/>.
    /// <see cref="HttpClient.Timeout"/> fica infinito para o token da chamada ser o único prazo.
    /// </summary>
    public static HttpClient CriarHttpClient() => new(CriarHandler(), disposeHandler: true)
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    internal TimeSpan TimeoutDoTransporte => _http.Timeout;

    public void Dispose() => _http.Dispose();

    private static void ImpedirRedirect(HttpMessageHandler transporte)
    {
        switch (transporte)
        {
            case SocketsHttpHandler sockets:
                sockets.AllowAutoRedirect = false;
                break;
            case HttpClientHandler http:
                http.AllowAutoRedirect = false;
                break;
        }
    }

    /// <summary>Tamanho do binário estimado pelo base64, sem alocar a imagem.</summary>
    public static bool ImagemCabe(string? imagemBase64)
    {
        if (string.IsNullOrEmpty(imagemBase64))
            return false;

        var comprimento = imagemBase64.Length;
        if ((comprimento & 3) != 0)
            return false;

        var padding = 0;
        if (imagemBase64[comprimento - 1] == '=')
            padding++;
        if (comprimento > 1 && imagemBase64[comprimento - 2] == '=')
            padding++;

        var bytes = (comprimento / 4L) * 3L - padding;
        return bytes > 0 && bytes <= TamanhoMaximoImagem;
    }

    public Task<ResultadoMensagem> LerComprovanteAsync(
        string? urlBase,
        string? chave,
        string? modelo,
        string? tipoMidia,
        string? imagemBase64,
        CancellationToken cancellationToken = default)
    {
        var preparo = PrepararChamada(urlBase, chave);
        if (preparo.Falha is not null)
            return Task.FromResult(preparo.Falha);

        if (string.IsNullOrWhiteSpace(modelo) ||
            string.IsNullOrWhiteSpace(tipoMidia) ||
            string.IsNullOrWhiteSpace(imagemBase64) ||
            !ImagemCabe(imagemBase64))
        {
            return Task.FromResult(
                ResultadoMensagem.Falha(TipoResultadoMensagem.PedidoInvalido, ResultadoMensagem.TextoPedidoInvalido));
        }

        var json = PedidoMensagem.Montar(modelo.Trim(), tipoMidia.Trim(), imagemBase64);
        return EnviarAsync(preparo.Uri, preparo.Chave, json, interpretarComprovante: true, cancellationToken);
    }

    /// <summary>
    /// Chamada mínima, sem foto. O cliente de produção nasce em <see cref="Criar"/>.
    /// </summary>
    public Task<ResultadoMensagem> TestarConexaoAsync(
        string? urlBase,
        string? chave,
        string? modelo,
        CancellationToken cancellationToken = default)
    {
        var preparo = PrepararChamada(urlBase, chave);
        if (preparo.Falha is not null)
            return Task.FromResult(preparo.Falha);

        if (string.IsNullOrWhiteSpace(modelo))
        {
            return Task.FromResult(
                ResultadoMensagem.Falha(TipoResultadoMensagem.PedidoInvalido, ResultadoMensagem.TextoPedidoInvalido));
        }

        var json = PedidoMensagem.MontarTesteConexao(modelo);
        return EnviarAsync(preparo.Uri, preparo.Chave, json, interpretarComprovante: false, cancellationToken);
    }

    /// <summary>
    /// Texto fixo para a tela. Se a mensagem carregasse a chave, devolve a falha genérica.
    /// </summary>
    public static string TextoExibicao(ResultadoMensagem resultado, string? segredo)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        var texto = resultado.Sucesso
            ? ResultadoMensagem.TextoConexaoOk
            : (string.IsNullOrEmpty(resultado.Mensagem) ? ResultadoMensagem.TextoFalha : resultado.Mensagem);

        if (!string.IsNullOrEmpty(segredo) && texto.Contains(segredo, StringComparison.Ordinal))
            return ResultadoMensagem.TextoFalha;

        return texto;
    }

    private readonly record struct PreparoChamada(Uri Uri, string Chave, ResultadoMensagem? Falha)
    {
        public static PreparoChamada Recusa(ResultadoMensagem falha) => new(null!, "", falha);
    }

    private PreparoChamada PrepararChamada(string? urlBase, string? chave)
    {
        if (PedidoMensagem.CriarUri(urlBase, _permiteHttp) is not Uri criada ||
            (criada.Scheme != Uri.UriSchemeHttps && !_permiteHttp))
        {
            return PreparoChamada.Recusa(
                ResultadoMensagem.Falha(TipoResultadoMensagem.PedidoInvalido, ResultadoMensagem.TextoPedidoInvalido));
        }

        if (string.IsNullOrWhiteSpace(chave) || chave.Contains('\r') || chave.Contains('\n'))
        {
            return PreparoChamada.Recusa(
                ResultadoMensagem.Falha(TipoResultadoMensagem.NaoAutorizado, ResultadoMensagem.TextoNaoAutorizado));
        }

        return new PreparoChamada(criada, chave, null);
    }

    private async Task<ResultadoMensagem> EnviarAsync(
        Uri uri,
        string chave,
        string json,
        bool interpretarComprovante,
        CancellationToken cancellationToken)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(_tempoLimite);

        try
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Post, uri);
            pedido.Headers.ExpectContinue = false;
            if (!pedido.Headers.TryAddWithoutValidation(CabecalhoChave, chave) ||
                !pedido.Headers.TryAddWithoutValidation(CabecalhoVersao, VersaoAnthropic))
            {
                return ResultadoMensagem.Falha(TipoResultadoMensagem.PedidoInvalido, ResultadoMensagem.TextoPedidoInvalido);
            }

            pedido.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resposta = await _http.SendAsync(pedido, HttpCompletionOption.ResponseHeadersRead, limite.Token);
            if (EhRedirecionamento(resposta.StatusCode))
                return ResultadoMensagem.Falha(TipoResultadoMensagem.Falha, ResultadoMensagem.TextoFalha);

            if (resposta.StatusCode != HttpStatusCode.OK)
                return MapearStatus(resposta);

            return interpretarComprovante
                ? await LerCorpoAsync(resposta, limite.Token)
                : ResultadoMensagem.ConexaoOk();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return ResultadoMensagem.Falha(TipoResultadoMensagem.Timeout, ResultadoMensagem.TextoTimeout);
        }
        catch (TimeoutException)
        {
            return ResultadoMensagem.Falha(TipoResultadoMensagem.Timeout, ResultadoMensagem.TextoTimeout);
        }
        catch (HttpRequestException ex) when (ex.InnerException is TimeoutException or OperationCanceledException)
        {
            return ResultadoMensagem.Falha(TipoResultadoMensagem.Timeout, ResultadoMensagem.TextoTimeout);
        }
        catch (HttpRequestException)
        {
            return ResultadoMensagem.Falha(TipoResultadoMensagem.SemRede, ResultadoMensagem.TextoSemRede);
        }
        catch (IOException)
        {
            return ResultadoMensagem.Falha(TipoResultadoMensagem.SemRede, ResultadoMensagem.TextoSemRede);
        }
        catch (FormatException)
        {
            return ResultadoMensagem.Falha(TipoResultadoMensagem.PedidoInvalido, ResultadoMensagem.TextoPedidoInvalido);
        }
    }

    private static bool EhRedirecionamento(HttpStatusCode status)
    {
        var codigo = (int)status;
        return codigo is >= 300 and < 400;
    }

    private static async Task<ResultadoMensagem> LerCorpoAsync(HttpResponseMessage resposta, CancellationToken cancellationToken)
    {
        var corpo = await LerTextoLimitadoAsync(resposta.Content, cancellationToken);
        if (corpo is null)
            return ResultadoMensagem.Falha(TipoResultadoMensagem.RespostaInvalida, ResultadoMensagem.TextoRespostaInvalida);

        var comprovante = LeitorRespostaComprovante.Ler(corpo);
        return comprovante is null
            ? ResultadoMensagem.Falha(TipoResultadoMensagem.RespostaInvalida, ResultadoMensagem.TextoRespostaInvalida)
            : ResultadoMensagem.Ok(comprovante);
    }

    private static async Task<string?> LerTextoLimitadoAsync(HttpContent? content, CancellationToken cancellationToken)
    {
        if (content is null)
            return string.Empty;

        if (content.Headers.ContentLength is long anunciado && anunciado > TamanhoMaximoCorpo)
            return null;

        await using var fluxo = await content.ReadAsStreamAsync(cancellationToken);
        using var memoria = new MemoryStream();
        var buffer = new byte[8192];
        long total = 0;
        while (true)
        {
            var lidos = await fluxo.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (lidos == 0)
                break;

            total += lidos;
            if (total > TamanhoMaximoCorpo)
                return null;

            memoria.Write(buffer, 0, lidos);
        }

        return Encoding.UTF8.GetString(memoria.GetBuffer(), 0, (int)memoria.Length);
    }

    private static ResultadoMensagem MapearStatus(HttpResponseMessage resposta)
    {
        return (int)resposta.StatusCode switch
        {
            401 => ResultadoMensagem.Falha(TipoResultadoMensagem.NaoAutorizado, ResultadoMensagem.TextoNaoAutorizado),
            429 => ResultadoMensagem.Falha(
                TipoResultadoMensagem.Limite,
                ResultadoMensagem.TextoLimite,
                LerRetryAfter(resposta)),
            529 => ResultadoMensagem.Falha(TipoResultadoMensagem.Sobrecarga, ResultadoMensagem.TextoSobrecarga),
            504 => ResultadoMensagem.Falha(TipoResultadoMensagem.Timeout, ResultadoMensagem.TextoTimeout),
            400 => ResultadoMensagem.Falha(TipoResultadoMensagem.PedidoInvalido, ResultadoMensagem.TextoPedidoInvalido),
            _ => ResultadoMensagem.Falha(TipoResultadoMensagem.Falha, ResultadoMensagem.TextoFalha)
        };
    }

    private static TimeSpan? LerRetryAfter(HttpResponseMessage resposta)
    {
        if (!resposta.Headers.TryGetValues("Retry-After", out var valores))
            return null;

        var texto = valores.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        if (long.TryParse(texto, NumberStyles.Integer, Invariante, out var segundos))
        {
            if (segundos < 0)
                return null;

            var teto = (long)RetryAfterMaximo.TotalSeconds;
            return segundos >= teto ? RetryAfterMaximo : TimeSpan.FromSeconds(segundos);
        }

        if (!DateTimeOffset.TryParse(
                texto,
                Invariante,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var quando))
        {
            return null;
        }

        var delta = quando - DateTimeOffset.UtcNow;
        if (delta < TimeSpan.Zero)
            return TimeSpan.Zero;

        return delta > RetryAfterMaximo ? RetryAfterMaximo : delta;
    }
}
