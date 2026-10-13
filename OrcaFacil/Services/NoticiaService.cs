using System.Xml;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using OrcaFacil.Core.Noticias;
using OrcaFacil.Services.Interfaces;

namespace OrcaFacil.Services;

public class NoticiaService : INoticiaService
{
    public const string HttpClientName = "noticias";
    public const int MaximoPorFeed = 15;
    public const string AvisoListaAnterior = "Não foi possível atualizar. Mostrando a última lista.";

    private static readonly TimeSpan ValidadeCache = TimeSpan.FromMinutes(15);

    private static readonly IReadOnlyDictionary<EscopoNoticia, string> Feeds = new Dictionary<EscopoNoticia, string>
    {
        [EscopoNoticia.Brasil] = "https://news.google.com/rss/search?q=economia+brasil&hl=pt-BR&gl=BR&ceid=BR:pt-419",
        [EscopoNoticia.Mundo] = "https://news.google.com/rss/search?q=economia+internacional&hl=pt-BR&gl=BR&ceid=BR:pt-419"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NoticiaService> _logger;
    private readonly object _sync = new();
    private readonly Dictionary<EscopoNoticia, EntradaCache> _cache = new();

    public NoticiaService(IHttpClientFactory httpClientFactory, ILogger<NoticiaService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ResultadoNoticias> ListarAsync(
        EscopoNoticia escopo,
        bool forcarAtualizacao = false,
        CancellationToken cancellationToken = default)
    {
        if (!forcarAtualizacao && TentarCache(escopo, out var emCache))
            return new ResultadoNoticias(emCache, null);

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var xml = await client.GetStringAsync(Feeds[escopo], cancellationToken);
            var itens = RssNoticiaParser.Ler(xml, MaximoPorFeed);
            Guardar(escopo, itens);
            return new ResultadoNoticias(itens, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or XmlException)
        {
            _logger.LogWarning(ex, "Falha ao buscar notícias de {Escopo}", escopo);
            if (TentarCache(escopo, out var anterior, ignorarValidade: true))
                return new ResultadoNoticias(anterior, AvisoListaAnterior);

            throw;
        }
    }

    private bool TentarCache(EscopoNoticia escopo, out IReadOnlyList<Noticia> itens, bool ignorarValidade = false)
    {
        lock (_sync)
        {
            if (_cache.TryGetValue(escopo, out var entrada) &&
                (ignorarValidade || DateTimeOffset.UtcNow - entrada.ObtidoEm < ValidadeCache))
            {
                itens = entrada.Itens;
                return true;
            }
        }

        itens = [];
        return false;
    }

    private void Guardar(EscopoNoticia escopo, IReadOnlyList<Noticia> itens)
    {
        lock (_sync)
        {
            _cache[escopo] = new EntradaCache(DateTimeOffset.UtcNow, itens);
        }
    }

    private sealed record EntradaCache(DateTimeOffset ObtidoEm, IReadOnlyList<Noticia> Itens);
}
