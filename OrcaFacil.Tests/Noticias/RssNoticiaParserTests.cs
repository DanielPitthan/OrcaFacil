using OrcaFacil.Core.Noticias;

namespace OrcaFacil.Tests.Noticias;

public class RssNoticiaParserTests
{
    [Fact]
    public void ExtraiTituloLinkDataEVeiculo_EIgnoraItemSemLink()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0">
              <channel>
                <item>
                  <title>IPCA sobe em setembro - G1</title>
                  <link>https://news.google.com/rss/articles/ipca</link>
                  <pubDate>Fri, 09 Oct 2026 12:00:00 GMT</pubDate>
                  <source url="https://g1.globo.com">G1</source>
                </item>
                <item>
                  <title>Sem link</title>
                  <pubDate>Fri, 09 Oct 2026 15:00:00 GMT</pubDate>
                  <source url="https://exemplo.com">Exemplo</source>
                </item>
                <item>
                  <title>Dólar recua no exterior</title>
                  <link>   </link>
                  <pubDate>Fri, 09 Oct 2026 16:00:00 GMT</pubDate>
                </item>
                <item>
                  <title>Bolsas sobem com queda do petróleo - Reuters</title>
                  <link>https://exemplo.com/bolsas</link>
                  <pubDate>Fri, 09 Oct 2026 13:30:00 GMT</pubDate>
                  <source url="https://www.reuters.com">Reuters</source>
                </item>
              </channel>
            </rss>
            """;

        var noticias = RssNoticiaParser.Ler(xml);

        Assert.Equal(2, noticias.Count);

        Assert.Equal("Bolsas sobem com queda do petróleo", noticias[0].Titulo);
        Assert.Equal("https://exemplo.com/bolsas", noticias[0].Link);
        Assert.Equal("Reuters", noticias[0].Veiculo);
        Assert.Equal(new DateTimeOffset(2026, 10, 9, 13, 30, 0, TimeSpan.Zero), noticias[0].PublicadaEm);

        Assert.Equal("IPCA sobe em setembro", noticias[1].Titulo);
        Assert.Equal("https://news.google.com/rss/articles/ipca", noticias[1].Link);
        Assert.Equal("G1", noticias[1].Veiculo);
        Assert.Equal(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero), noticias[1].PublicadaEm);
    }

    [Fact]
    public void LimitaQuantidade_MantendoAsMaisRecentes()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0">
              <channel>
                <item>
                  <title>Mais antiga - Fonte</title>
                  <link>https://exemplo.com/1</link>
                  <pubDate>Fri, 09 Oct 2026 10:00:00 GMT</pubDate>
                  <source url="https://exemplo.com">Fonte</source>
                </item>
                <item>
                  <title>Do meio - Fonte</title>
                  <link>https://exemplo.com/2</link>
                  <pubDate>Fri, 09 Oct 2026 11:00:00 GMT</pubDate>
                  <source url="https://exemplo.com">Fonte</source>
                </item>
                <item>
                  <title>Mais recente - Fonte</title>
                  <link>https://exemplo.com/3</link>
                  <pubDate>Fri, 09 Oct 2026 12:00:00 GMT</pubDate>
                  <source url="https://exemplo.com">Fonte</source>
                </item>
              </channel>
            </rss>
            """;

        var noticias = RssNoticiaParser.Ler(xml, maximo: 2);

        Assert.Equal(2, noticias.Count);
        Assert.Equal("Mais recente", noticias[0].Titulo);
        Assert.Equal("Do meio", noticias[1].Titulo);
    }

    [Fact]
    public void XmlVazio_RetornaListaVazia()
    {
        Assert.Empty(RssNoticiaParser.Ler(null));
        Assert.Empty(RssNoticiaParser.Ler("   "));
        Assert.Empty(RssNoticiaParser.Ler("<rss><channel></channel></rss>", maximo: 0));
    }
}
