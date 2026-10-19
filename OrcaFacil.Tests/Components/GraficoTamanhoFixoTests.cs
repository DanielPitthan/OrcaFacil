using System.Text.RegularExpressions;

namespace OrcaFacil.Tests.Components;

/// <summary>
/// No Android 11 o ResizeObserver do MudChart (MatchBoundsToSize) mede o SVG.
/// Sem caixa estável o viewBox oscila e o rótulo do eixo infla. Quem trava é
/// display:block, min-width/min-height 0 e altura em px. overflow corta o eixo
/// (text-anchor end e meio glifo inferior) e o tooltip das barras.
/// </summary>
public class GraficoTamanhoFixoTests
{
    [Theory]
    [InlineData("MonthlyComparisonChart", "of-comparacao-plot", "220px")]
    [InlineData("MonthEvolutionChart", "of-evolucao-caixa", "168px")]
    public void Container_TravaAlturaEmPx_ELarguraDoPai_SemCortarRotulo(string componente, string classe, string altura)
    {
        var css = SemComentarios(File.ReadAllText(CaminhoCss(componente)));
        var razor = File.ReadAllText(CaminhoRazor(componente));
        var bloco = BlocoDoSeletor(css, "." + classe);
        var chart = BlocoDoSeletor(css, $".{classe} ::deep .mud-chart");
        var svg = BlocoDoSeletor(css, $".{classe} ::deep .mud-chart svg");

        AssertTemAltura(bloco, altura);
        Assert.Contains("width: 100%;", bloco, StringComparison.Ordinal);
        Assert.Contains("max-width: 100%;", bloco, StringComparison.Ordinal);
        Assert.Contains("min-width: 0;", bloco, StringComparison.Ordinal);
        Assert.DoesNotContain("overflow", bloco, StringComparison.Ordinal);

        AssertCaixaEstavel(chart, altura);
        AssertCaixaEstavel(svg, altura);

        Assert.Contains($"Height=\"{altura}\"", razor, StringComparison.Ordinal);
        Assert.Contains("Width=\"100%\"", razor, StringComparison.Ordinal);
        Assert.Contains($"class=\"{classe}\"", razor, StringComparison.Ordinal);

        AssertCssEstavel(css);
    }

    [Fact]
    public void MudChart_TemDisplayBlockNaPropriaRegra()
    {
        foreach (var (componente, classe) in new[]
        {
            ("MonthlyComparisonChart", "of-comparacao-plot"),
            ("MonthEvolutionChart", "of-evolucao-caixa")
        })
        {
            var css = SemComentarios(File.ReadAllText(CaminhoCss(componente)));
            var seletor = $".{classe} ::deep .mud-chart";
            var chart = BlocoDoSeletor(css, seletor);

            Assert.Contains("display: block;", chart, StringComparison.Ordinal);
            Assert.DoesNotContain("svg", SeletoresDaRegra(css, seletor), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Evolucao_MantemOPlotEm168px_SemCortarOMarcadorHoje()
    {
        var css = SemComentarios(File.ReadAllText(CaminhoCss("MonthEvolutionChart")));
        var bloco = BlocoDoSeletor(css, ".of-evolucao-plot");

        AssertTemAltura(bloco, "168px");
        Assert.Contains("width: 100%;", bloco, StringComparison.Ordinal);
        Assert.Contains("max-width: 100%;", bloco, StringComparison.Ordinal);
        Assert.Contains("min-width: 0;", bloco, StringComparison.Ordinal);
        Assert.Contains("position: relative;", bloco, StringComparison.Ordinal);
        Assert.DoesNotContain("overflow", bloco, StringComparison.Ordinal);
    }

    private static void AssertTemAltura(string bloco, string altura)
    {
        Assert.True(
            Regex.IsMatch(bloco, $@"(?<![\w-])height: {Regex.Escape(altura)};"),
            $"Falta height: {altura} fora de min/max-height.");
    }

    private static void AssertCaixaEstavel(string bloco, string altura)
    {
        Assert.Contains("display: block;", bloco, StringComparison.Ordinal);
        AssertTemAltura(bloco, altura);
        Assert.Contains("min-height: 0;", bloco, StringComparison.Ordinal);
        Assert.Contains($"max-height: {altura};", bloco, StringComparison.Ordinal);
        Assert.Contains("width: 100%;", bloco, StringComparison.Ordinal);
        Assert.Contains("max-width: 100%;", bloco, StringComparison.Ordinal);
        Assert.Contains("min-width: 0;", bloco, StringComparison.Ordinal);
        Assert.DoesNotContain("overflow", bloco, StringComparison.Ordinal);
    }

    private static void AssertCssEstavel(string css)
    {
        Assert.DoesNotContain("overflow", css, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"(?<![\w-])\d*(?:\.\d+)?(?:d|s|l)?vh\b", RegexOptions.IgnoreCase), css);
        Assert.DoesNotContain("aspect-ratio", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("container-type", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@container", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(":has(", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clamp(", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("min(", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("max(", css, StringComparison.OrdinalIgnoreCase);
    }

    private static string SemComentarios(string css) =>
        Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);

    private static string SeletoresDaRegra(string css, string seletor)
    {
        var inicio = 0;
        while (inicio < css.Length)
        {
            var abre = css.IndexOf('{', inicio);
            if (abre < 0)
                break;

            var fecha = css.IndexOf('}', abre);
            if (fecha < 0)
                break;

            var cabeca = css[inicio..abre].Trim();
            if (cabeca == seletor)
                return cabeca;

            inicio = fecha + 1;
        }

        throw new InvalidOperationException($"Seletor não encontrado: {seletor}");
    }

    private static string BlocoDoSeletor(string css, string seletor)
    {
        var inicio = 0;
        while (inicio < css.Length)
        {
            var abre = css.IndexOf('{', inicio);
            if (abre < 0)
                break;

            var fecha = css.IndexOf('}', abre);
            if (fecha < 0)
                break;

            if (css[inicio..abre].Trim() == seletor)
                return css[abre..(fecha + 1)];

            inicio = fecha + 1;
        }

        throw new InvalidOperationException($"Seletor não encontrado: {seletor}");
    }

    private static string CaminhoCss(string componente) =>
        Path.Combine(Raiz(), "OrcaFacil", "Components", "Shared", componente + ".razor.css");

    private static string CaminhoRazor(string componente) =>
        Path.Combine(Raiz(), "OrcaFacil", "Components", "Shared", componente + ".razor");

    private static string Raiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OrcaFacil", "Components", "Shared", "MonthlyComparisonChart.razor.css")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar o CSS dos gráficos a partir de " + AppContext.BaseDirectory);
    }
}
