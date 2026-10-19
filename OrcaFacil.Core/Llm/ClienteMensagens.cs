using System.Globalization;
using System.Net;
using System.Text;

namespace OrcaFacil.Core.Llm;

/// <summary>
/// Cliente de <c>POST {base}/v1/messages</c>. A produção nasce em <see cref="Criar"/>,
/// com redirect desligado. A chave entra só no header de cada pedido HTTPS.
/// Não grava chave, header, corpo, imagem nem caminho.
/// O diagnóstico da falha fica no resultado, já sem token, cabeçalho e imagem.
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
        CancellationToken cancellationToken = default,
        int maxTokens = PedidoMensagem.MaxTokens)
    {
        var preparo = PrepararChamada(urlBase, chave);
        if (preparo.Falha is not null)
            return Task.FromResult(preparo.Falha);

        if (string.IsNullOrWhiteSpace(modelo) ||
            string.IsNullOrWhiteSpace(tipoMidia) ||
            string.IsNullOrWhiteSpace(imagemBase64) ||
            !ImagemCabe(imagemBase64) ||
            !PedidoMensagem.MaxTokensNaFaixa(maxTokens))
        {
            return Task.FromResult(
                ResultadoMensagem.Falha(TipoResultadoMensagem.PedidoInvalido, ResultadoMensagem.TextoPedidoInvalido));
        }

        var json = PedidoMensagem.Montar(modelo.Trim(), tipoMidia.Trim(), imagemBase64, maxTokens);
        return EnviarAsync(
            preparo.Uri,
            preparo.Chave,
            json,
            interpretarComprovante: true,
            modelo.Trim(),
            imagemBase64,
            cancellationToken);
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
        if (DiagnosticoTeste(urlBase, chave, modelo) is ResultadoMensagem local)
            return Task.FromResult(local);

        var preparo = PrepararChamada(urlBase, chave);
        if (preparo.Falha is not null)
            return Task.FromResult(preparo.Falha);

        var json = PedidoMensagem.MontarTesteConexao(modelo!);
        return EnviarAsync(
            preparo.Uri,
            preparo.Chave,
            json,
            interpretarComprovante: false,
            modelo!.Trim(),
            imagemBase64: null,
            cancellationToken);
    }

    /// <summary>
    /// Texto fixo para a tela. Frase conhecida não é trocada quando a chave é curta
    /// e aparece dentro dela. Mensagem fora dessas frases, se carregar a chave, vira falha genérica.
    /// </summary>
    public static string TextoExibicao(ResultadoMensagem resultado, string? segredo)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        var texto = resultado.Sucesso
            ? ResultadoMensagem.TextoConexaoOk
            : (string.IsNullOrEmpty(resultado.Mensagem) ? ResultadoMensagem.TextoFalha : resultado.Mensagem);

        if (FraseFixa(texto) || string.IsNullOrWhiteSpace(segredo))
            return texto;

        if (texto.Contains(segredo, StringComparison.Ordinal))
            return ResultadoMensagem.TextoFalha;

        return texto;
    }

    private static bool FraseFixa(string texto) => texto is
        ResultadoMensagem.TextoConexaoOk or
        ResultadoMensagem.TextoNaoAutorizado or
        ResultadoMensagem.TextoSemPermissao or
        ResultadoMensagem.TextoLimite or
        ResultadoMensagem.TextoSobrecarga or
        ResultadoMensagem.TextoTimeout or
        ResultadoMensagem.TextoPedidoInvalido or
        ResultadoMensagem.TextoChaveNaoInformada or
        ResultadoMensagem.TextoUrlNaoInformada or
        ResultadoMensagem.TextoModeloNaoInformado or
        ResultadoMensagem.TextoUrlInvalida or
        ResultadoMensagem.TextoRespostaInvalida or
        ResultadoMensagem.TextoRespostaCortada or
        ResultadoMensagem.TextoSemRede or
        ResultadoMensagem.TextoFalha or
        ResultadoMensagem.TextoModeloSemVisao;

    /// <summary>
    /// Falhas locais do teste, antes da rede. Cada uma tem frase própria,
    /// diferente do 401, da falta de rede e do prazo.
    /// </summary>
    private static ResultadoMensagem? DiagnosticoTeste(string? urlBase, string? chave, string? modelo)
    {
        if (string.IsNullOrWhiteSpace(urlBase))
            return ResultadoMensagem.Falha(TipoResultadoMensagem.PedidoInvalido, ResultadoMensagem.TextoUrlNaoInformada);

        if (PedidoMensagem.CriarUri(urlBase) is null)
            return ResultadoMensagem.Falha(TipoResultadoMensagem.PedidoInvalido, ResultadoMensagem.TextoUrlInvalida);

        if (string.IsNullOrWhiteSpace(chave))
            return ResultadoMensagem.Falha(TipoResultadoMensagem.PedidoInvalido, ResultadoMensagem.TextoChaveNaoInformada);

        if (string.IsNullOrWhiteSpace(modelo))
            return ResultadoMensagem.Falha(TipoResultadoMensagem.PedidoInvalido, ResultadoMensagem.TextoModeloNaoInformado);

        return null;
    }

    private readonly record struct PreparoChamada(Uri Uri, string Chave, ResultadoMensagem? Falha)
    {
        public static PreparoChamada Recusa(ResultadoMensagem falha) => new(null!, "", falha);

        public override string ToString() => "PreparoChamada";
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
        string? modelo,
        string? imagemBase64,
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
                return Recusar(
                    TipoResultadoMensagem.PedidoInvalido,
                    ResultadoMensagem.TextoPedidoInvalido,
                    uri,
                    modelo,
                    chave,
                    imagemBase64);
            }

            pedido.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resposta = await _http.SendAsync(pedido, HttpCompletionOption.ResponseHeadersRead, limite.Token);
            if (EhRedirecionamento(resposta.StatusCode))
            {
                return Recusar(
                    TipoResultadoMensagem.Falha,
                    ResultadoMensagem.TextoFalha,
                    uri,
                    modelo,
                    chave,
                    imagemBase64,
                    (int)resposta.StatusCode);
            }

            if (resposta.StatusCode != HttpStatusCode.OK)
                return await MapearStatusAsync(resposta, uri, modelo, chave, imagemBase64, limite.Token);

            // 200 do teste de conexão é sucesso sem ler o corpo: não é um comprovante,
            // e o texto da resposta não entra na mensagem.
            if (!interpretarComprovante)
                return ResultadoMensagem.ConexaoOk();

            return await LerCorpoAsync(resposta, uri, modelo, chave, imagemBase64, limite.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Recusar(TipoResultadoMensagem.Timeout, ResultadoMensagem.TextoTimeout, uri, modelo, chave, imagemBase64);
        }
        catch (TimeoutException)
        {
            return Recusar(TipoResultadoMensagem.Timeout, ResultadoMensagem.TextoTimeout, uri, modelo, chave, imagemBase64);
        }
        catch (HttpRequestException ex) when (ex.InnerException is TimeoutException or OperationCanceledException)
        {
            return Recusar(TipoResultadoMensagem.Timeout, ResultadoMensagem.TextoTimeout, uri, modelo, chave, imagemBase64);
        }
        catch (HttpRequestException)
        {
            return Recusar(TipoResultadoMensagem.SemRede, ResultadoMensagem.TextoSemRede, uri, modelo, chave, imagemBase64);
        }
        catch (IOException)
        {
            return Recusar(TipoResultadoMensagem.SemRede, ResultadoMensagem.TextoSemRede, uri, modelo, chave, imagemBase64);
        }
        catch (FormatException)
        {
            return Recusar(
                TipoResultadoMensagem.PedidoInvalido,
                ResultadoMensagem.TextoPedidoInvalido,
                uri,
                modelo,
                chave,
                imagemBase64);
        }
    }

    private static bool EhRedirecionamento(HttpStatusCode status)
    {
        var codigo = (int)status;
        return codigo is >= 300 and < 400;
    }

    private static async Task<ResultadoMensagem> LerCorpoAsync(
        HttpResponseMessage resposta,
        Uri uri,
        string? modelo,
        string chave,
        string? imagemBase64,
        CancellationToken cancellationToken)
    {
        var corpo = await LerTextoLimitadoAsync(resposta.Content, cancellationToken);
        if (corpo is null)
        {
            return Recusar(
                TipoResultadoMensagem.RespostaInvalida,
                ResultadoMensagem.TextoRespostaInvalida,
                uri,
                modelo,
                chave,
                imagemBase64,
                (int)resposta.StatusCode,
                corpo: null,
                corpoIncompleto: true,
                motivo: LeitorRespostaComprovante.MotivoTruncada);
        }

        var leitura = LeitorRespostaComprovante.Interpretar(corpo, cancellationToken);
        if (leitura.Comprovante is not null)
            return ResultadoMensagem.Ok(leitura.Comprovante);

        var mensagem = leitura.Motivo == LeitorRespostaComprovante.MotivoCortada
            ? ResultadoMensagem.TextoRespostaCortada
            : ResultadoMensagem.TextoRespostaInvalida;

        return Recusar(
            TipoResultadoMensagem.RespostaInvalida,
            mensagem,
            uri,
            modelo,
            chave,
            imagemBase64,
            (int)resposta.StatusCode,
            corpo,
            corpoIncompleto: false,
            leitura.Motivo,
            leitura.StopReason,
            leitura.FinishReason);
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

    private const int AmostraMaximaBytes = 32 * 1024;

    private static async Task<ResultadoMensagem> MapearStatusAsync(
        HttpResponseMessage resposta,
        Uri uri,
        string? modelo,
        string chave,
        string? imagemBase64,
        CancellationToken cancellationToken)
    {
        var (amostra, truncado) = await LerAmostraAsync(resposta.Content, cancellationToken);
        var status = (int)resposta.StatusCode;
        if (status == 400 && DiagnosticoErroProvedor.IndicaModeloSemVisao(amostra))
        {
            return Recusar(
                TipoResultadoMensagem.ModeloSemVisao,
                ResultadoMensagem.TextoModeloSemVisao,
                uri,
                modelo,
                chave,
                imagemBase64,
                status,
                amostra,
                truncado);
        }

        var (tipo, mensagem, retry) = status switch
        {
            401 => (TipoResultadoMensagem.NaoAutorizado, ResultadoMensagem.TextoNaoAutorizado, (TimeSpan?)null),
            403 => (TipoResultadoMensagem.SemPermissao, ResultadoMensagem.TextoSemPermissao, null),
            429 => (TipoResultadoMensagem.Limite, ResultadoMensagem.TextoLimite, LerRetryAfter(resposta)),
            529 => (TipoResultadoMensagem.Sobrecarga, ResultadoMensagem.TextoSobrecarga, null),
            504 => (TipoResultadoMensagem.Timeout, ResultadoMensagem.TextoTimeout, null),
            400 => (TipoResultadoMensagem.PedidoInvalido, ResultadoMensagem.TextoPedidoInvalido, null),
            _ => (TipoResultadoMensagem.Falha, ResultadoMensagem.TextoFalha, null)
        };

        return Recusar(
            tipo,
            mensagem,
            uri,
            modelo,
            chave,
            imagemBase64,
            status,
            amostra,
            truncado,
            retryAfter: retry);
    }

    /// <summary>
    /// Lê só o começo do corpo de erro. A amostra entra no diagnóstico já sanitizada.
    /// Cabeçalhos do pedido não são lidos.
    /// </summary>
    private static async Task<(string? Texto, bool Truncado)> LerAmostraAsync(
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        if (content is null)
            return (null, false);

        try
        {
            if (content.Headers.ContentLength is long anunciado && anunciado == 0)
                return (string.Empty, false);

            var anunciadoGrande = content.Headers.ContentLength is long tamanho && tamanho > AmostraMaximaBytes;
            await using var fluxo = await content.ReadAsStreamAsync(cancellationToken);
            using var memoria = new MemoryStream();
            var buffer = new byte[8192];
            long total = 0;
            while (total < AmostraMaximaBytes)
            {
                var quer = (int)Math.Min(buffer.Length, AmostraMaximaBytes - total);
                var lidos = await fluxo.ReadAsync(buffer.AsMemory(0, quer), cancellationToken);
                if (lidos == 0)
                    break;

                memoria.Write(buffer, 0, lidos);
                total += lidos;
            }

            var sobrou = anunciadoGrande;
            if (!sobrou && total >= AmostraMaximaBytes)
            {
                var extra = await fluxo.ReadAsync(buffer.AsMemory(0, 1), cancellationToken);
                sobrou = extra > 0;
            }

            var texto = Encoding.UTF8.GetString(memoria.GetBuffer(), 0, (int)memoria.Length);
            return (texto, sobrou);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return (null, false);
        }
    }

    private static ResultadoMensagem Recusar(
        TipoResultadoMensagem tipo,
        string mensagem,
        Uri uri,
        string? modelo,
        string? chave,
        string? imagemBase64,
        int? status = null,
        string? corpo = null,
        bool corpoIncompleto = false,
        string? motivo = null,
        string? stopReason = null,
        string? finishReason = null,
        TimeSpan? retryAfter = null)
    {
        var diagnostico = DiagnosticoResposta.Criar(
            status,
            uri.IsAbsoluteUri ? uri.AbsoluteUri : null,
            modelo,
            tipo.ToString(),
            mensagem,
            motivo,
            stopReason,
            finishReason,
            corpo,
            corpoIncompleto,
            [chave, imagemBase64]);

        return ResultadoMensagem.Falha(tipo, mensagem, retryAfter, diagnostico);
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
