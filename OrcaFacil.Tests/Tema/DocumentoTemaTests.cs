namespace OrcaFacil.Tests.Tema;

public class DocumentoTemaTests
{
    [Fact]
    public void IndexHtml_NaoNasceClaro_EAplicaAClasseSalvaAntesDoCss()
    {
        var raiz = EncontrarRaiz();
        var html = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "wwwroot", "index.html"));
        var temaJs = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "wwwroot", "js", "tema.js"));
        var css = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "wwwroot", "css", "orcafacil-theme.css"));
        var layout = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Layout", "MainLayout.razor"));

        Assert.DoesNotContain("class=\"of-tema-claro\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"of-tema-escuro\"", html, StringComparison.Ordinal);
        Assert.Contains("orcafacil.tema.classe", html, StringComparison.Ordinal);
        Assert.Contains("orcafacil.tema.classe", temaJs, StringComparison.Ordinal);
        Assert.Contains("color-scheme: light dark", css, StringComparison.Ordinal);
        Assert.Contains("ObserveSystemDarkModeChange=\"false\"", layout, StringComparison.Ordinal);

        var script = html.IndexOf("<script>", StringComparison.Ordinal);
        var cssLink = html.IndexOf("orcafacil-theme.css", StringComparison.Ordinal);
        Assert.True(script >= 0 && script < cssLink);
    }

    private static string EncontrarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OrcaFacil", "wwwroot", "index.html")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar o index.html a partir de " + AppContext.BaseDirectory);
    }
}
