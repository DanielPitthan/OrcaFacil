using System.Globalization;
using System.Net;
using System.Text;

namespace OrcaFacil.Core.Llm;

/// <summary>
/// Cliente de <c>POST {base}/v1/messages</c>. A chave entra só no header de cada pedido.
/// Não grava chave, header, corpo, imagem nem caminho.
/// </summary>
public sealed class ClienteMensagens
{
    public const string VersaoAnthropic = "2023-06-01";
    public const string CabecalhoChave = "x-api-key";
    public const string CabecalhoVersao = "anthropic-version";

    /// <summary>Prazo próprio da leitura, maior que o timeout de 15 s do cliente de notícias.</summary>
    public static readonly TimeSpan TempoLimite = TimeSpan.FromSeconds(60);

    private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;

    private readonly HttpClient _http;
    private readonly TimeSpan _tempoLimite;

    public ClienteMensagens(HttpClient http, TimeSpan? tempoLimite = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        var limite = tempoLimite ?? TempoLimite;
        if (limite <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(tempoLimite));

        _tempoLimite = limite;
        _http = http;
        _http.Timeout = limite;
    }

    public async Task<ResultadoMensagem> LerComprovanteAsync(
        string? urlBase,
        string? chave,
        string? modelo,
        string? tipoMidia,
        string? imagemBase64,
        CancellationToken cancellationToken = default)
    {
        if (PedidoMensagem.CriarUri(urlBase) is not Uri uri)
            return ResultadoMensagem.Falha(TipoResultadoMensagem.PedidoInvalido, ResultadoMensagem.TextoPedidoInvalido);

        if (string.IsNullOrWhiteSpace(chave) || chave.Contains('\r') || chave.Contains('\n'))
            return ResultadoMensagem.Falha(TipoResultadoMensagem.NaoAutorizado, ResultadoMensagem.TextoNaoAutorizado);

        if (string.IsNullOrWhiteSpace(modelo) || string.IsNullOrWhiteSpace(tipoMidia) || string.IsNullOrWhiteSpace(imagemBase64))
            return ResultadoMensagem.Falha(TipoResultadoMensagem.PedidoInvalido, ResultadoMensagem.TextoPedidoInvalido);

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(_tempoLimite);

        try
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Post, uri);
            if (!pedido.Headers.TryAddWithoutValidation(CabecalhoChave, chave) ||
                !pedido.Headers.TryAddWithoutValidation(CabecalhoVersao, VersaoAnthropic))
            {
                return ResultadoMensagem.Falha(TipoResultadoMensagem.PedidoInvalido, ResultadoMensagem.TextoPedidoInvalido);
            }

            var json = PedidoMensagem.Montar(modelo.Trim(), tipoMidia.Trim(), imagemBase64);
            pedido.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resposta = await _http.SendAsync(pedido, HttpCompletionOption.ResponseHeadersRead, limite.Token);
            if (resposta.StatusCode == HttpStatusCode.OK)
                return await LerCorpoAsync(resposta, limite.Token);

            return MapearStatus(resposta);
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

    private static async Task<ResultadoMensagem> LerCorpoAsync(HttpResponseMessage resposta, CancellationToken cancellationToken)
    {
        var corpo = resposta.Content is null
            ? string.Empty
            : await resposta.Content.ReadAsStringAsync(cancellationToken);

        var comprovante = LeitorRespostaComprovante.Ler(corpo);
        return comprovante is null
            ? ResultadoMensagem.Falha(TipoResultadoMensagem.RespostaInvalida, ResultadoMensagem.TextoRespostaInvalida)
            : ResultadoMensagem.Ok(comprovante);
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

            var teto = (long)TimeSpan.MaxValue.TotalSeconds;
            return segundos >= teto ? TimeSpan.MaxValue : TimeSpan.FromSeconds(segundos);
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
        return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
    }
}
