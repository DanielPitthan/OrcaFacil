namespace OrcaFacil.Tests.Dirpf;

/// <summary>
/// O aviso fica no painel do DARF. O WebView do Android é Chrome 83:
/// sem light-dark, gap, :is, :where, :has ou color-mix.
/// O texto quebra dentro de 360px e o contraste fica em 4,5:1 ou mais.
/// </summary>
public class AvisoDarfComplementarTelaTests
{
    [Theory]
    [InlineData("html.of-tema-claro", "#1E2A33", "#FFFFFF")]
    [InlineData("html.of-tema-escuro", "#E8EAF0", "#12151B")]
    public void Aviso_TemContrasteECabeNaLarguraEstreita(string tema, string texto, string fundo)
    {
        var css = File.ReadAllText(Caminho("ExportarPlanilhaPanel.razor.css"));
        var razor = File.ReadAllText(Caminho("ExportarPlanilhaPanel.razor"));

        Assert.Contains("class=\"of-darf-complementar\"", razor, StringComparison.Ordinal);
        Assert.Contains("role=\"status\"", razor, StringComparison.Ordinal);
        Assert.Contains("AvisoDarfComplementar.Prefixo", razor, StringComparison.Ordinal);

        var bloco = Bloco(css, $":global({tema}) .of-darf-complementar");
        Assert.Contains($"color: {texto};", bloco, StringComparison.Ordinal);
        Assert.Contains($"background-color: {fundo};", bloco, StringComparison.Ordinal);

        var caixa = Bloco(css, ".of-darf-complementar");
        Assert.Contains("white-space: normal;", caixa, StringComparison.Ordinal);
        Assert.Contains("overflow-wrap: anywhere;", caixa, StringComparison.Ordinal);
        Assert.Contains("max-width: 100%;", caixa, StringComparison.Ordinal);
        Assert.DoesNotContain("min-width", css, StringComparison.Ordinal);
        Assert.DoesNotContain("nowrap", css, StringComparison.Ordinal);
        Assert.DoesNotContain("light-dark(", css, StringComparison.Ordinal);
        Assert.DoesNotContain("color-mix(", css, StringComparison.Ordinal);
        Assert.DoesNotContain(":is(", css, StringComparison.Ordinal);
        Assert.DoesNotContain(":where(", css, StringComparison.Ordinal);
        Assert.DoesNotContain(":has(", css, StringComparison.Ordinal);
        Assert.DoesNotContain("gap:", css, StringComparison.Ordinal);

        var razao = Contraste(texto, fundo);
        Assert.True(razao >= 4.5, $"{texto} sobre {fundo} = {razao:0.00}");
    }

    private static string Bloco(string css, string seletor)
    {
        var inicio = css.IndexOf(seletor + " {", StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Seletor não encontrado: {seletor}");
        var abre = css.IndexOf('{', inicio);
        var fecha = css.IndexOf('}', abre);
        return css[abre..(fecha + 1)];
    }

    private static string Caminho(string arquivo)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var caminho = Path.Combine(dir.FullName, "OrcaFacil", "Components", "Pages", "Configuracoes", arquivo);
            if (File.Exists(caminho))
                return caminho;

            dir = dir.Parent;
        }

        throw new InvalidOperationException($"Não foi possível localizar {arquivo}.");
    }

    private static double Contraste(string frente, string fundo)
    {
        var clara = Math.Max(Luminancia(frente), Luminancia(fundo));
        var escura = Math.Min(Luminancia(frente), Luminancia(fundo));
        return (clara + 0.05) / (escura + 0.05);
    }

    private static double Luminancia(string hex)
    {
        hex = hex.TrimStart('#');
        var r = Canal(hex[..2]);
        var g = Canal(hex.Substring(2, 2));
        var b = Canal(hex.Substring(4, 2));
        return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
    }

    private static double Canal(string hex)
    {
        var valor = Convert.ToInt32(hex, 16) / 255d;
        return valor <= 0.04045 ? valor / 12.92 : Math.Pow((valor + 0.055) / 1.055, 2.4);
    }
}
