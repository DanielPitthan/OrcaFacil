using System.Globalization;
using System.Xml.Linq;

namespace OrcaFacil.Core.Noticias;

/// <summary>Lê um RSS 2.0 (incluindo o formato do Google News) e devolve as manchetes mais recentes.</summary>
public static class RssNoticiaParser
{
    public static IReadOnlyList<Noticia> Ler(string? xml, int maximo = 15)
    {
        if (maximo < 1 || string.IsNullOrWhiteSpace(xml))
            return [];

        var documento = XDocument.Parse(xml);
        var noticias = new List<Noticia>();

        foreach (var item in documento.Descendants().Where(e => e.Name.LocalName == "item"))
        {
            var link = Texto(item, "link");
            if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                continue;
            }

            var titulo = Texto(item, "title");
            var veiculo = Texto(item, "source");
            (titulo, veiculo) = SepararVeiculo(titulo, veiculo);

            if (string.IsNullOrWhiteSpace(titulo))
                continue;

            noticias.Add(new Noticia(titulo, uri.AbsoluteUri, veiculo, Data(item)));
        }

        return noticias
            .OrderByDescending(n => n.PublicadaEm ?? DateTimeOffset.MinValue)
            .Take(maximo)
            .ToList();
    }

    private static (string Titulo, string Veiculo) SepararVeiculo(string titulo, string veiculo)
    {
        if (string.IsNullOrWhiteSpace(veiculo))
        {
            var separador = titulo.LastIndexOf(" - ", StringComparison.Ordinal);
            if (separador > 0)
            {
                veiculo = titulo[(separador + 3)..].Trim();
                titulo = titulo[..separador].Trim();
            }
        }
        else if (titulo.EndsWith(" - " + veiculo, StringComparison.Ordinal))
        {
            titulo = titulo[..^(veiculo.Length + 3)].Trim();
        }

        return (titulo, veiculo);
    }

    private static DateTimeOffset? Data(XElement item)
    {
        var texto = Texto(item, "pubDate");
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        return DateTimeOffset.TryParse(
            texto,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var data)
            ? data
            : null;
    }

    private static string Texto(XElement pai, string nome) =>
        pai.Elements().FirstOrDefault(e => e.Name.LocalName == nome)?.Value.Trim() ?? string.Empty;
}
