using System.Text.RegularExpressions;

namespace OrcaFacil.Tests.Components;

/// <summary>
/// No Android 11 o ResizeObserver do MudChart (MatchBoundsToSize) mede o SVG.
/// Sem caixa estável o viewBox oscila e o rótulo do eixo infla. O contêiner
/// fica em px, com a largura do pai.
/// </summary>
public class GraficoTamanhoFixoTests
{
    [Theory]
    [InlineData("MonthlyComparisonChart", "of-comparacao-plot", "220px")]
    [InlineData("MonthEvolutionChart", "of-evolucao-caixa", "168px")]
    public void Container_TravaAlturaEmPx_ELarguraDoPai(string componente, string classe, string altura)
    {
        var css = File.ReadAllText(CaminhoCss(componente));
        var razor = File.ReadAllText(CaminhoRazor(componente));
        var bloco = BlocoDoSeletor(css, "." + classe);
        var svg = BlocoDoSeletor(css, $".{classe} ::deep .mud-chart svg");

        Assert.Contains($"height: {altura};", bloco, StringComparison.Ordinal);
        Assert.Contains("width: 100%;", bloco, StringComparison.Ordinal);
        Assert.Contains("min-width: 0;", bloco, StringComparison.Ordinal);
        Assert.Contains("overflow: hidden;", bloco, StringComparison.Ordinal);

        Assert.Contains("display: block;", svg, StringComparison.Ordinal);
        Assert.Contains($"height: {altura};", svg, StringComparison.Ordinal);
        Assert.Contains("min-height: 0;", svg, StringComparison.Ordinal);
        Assert.Contains($"max-height: {altura};", svg, StringComparison.Ordinal);
        Assert.Contains("width: 100%;", svg, StringComparison.Ordinal);
        Assert.Contains("min-width: 0;", svg, StringComparison.Ordinal);
        Assert.Contains("overflow: hidden;", svg, StringComparison.Ordinal);

        Assert.Contains($"Height=\"{altura}\"", razor, StringComparison.Ordinal);
        Assert.Contains("Width=\"100%\"", razor, StringComparison.Ordinal);
        Assert.Contains($"class=\"{classe}\"", razor, StringComparison.Ordinal);

        AssertCssEstavel(css);
    }

    [Fact]
    public void Evolucao_MantemOPlotEm168px_SemCortarOMarcadorHoje()
    {
        var css = File.ReadAllText(CaminhoCss("MonthEvolutionChart"));
        var bloco = BlocoDoSeletor(css, ".of-evolucao-plot");

        Assert.Contains("height: 168px;", bloco, StringComparison.Ordinal);
        Assert.Contains("width: 100%;", bloco, StringComparison.Ordinal);
        Assert.Contains("min-width: 0;", bloco, StringComparison.Ordinal);
        Assert.Contains("position: relative;", bloco, StringComparison.Ordinal);
        Assert.DoesNotContain("overflow", bloco, StringComparison.Ordinal);
    }

    private static void AssertCssEstavel(string css)
    {
        Assert.DoesNotMatch(new Regex(@"(?<![\w-])(?:d|s|l)?vh\b", RegexOptions.IgnoreCase), css);
        Assert.DoesNotContain("aspect-ratio", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("container-type", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@container", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(":has(", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clamp(", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("min(", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("max(", css, StringComparison.OrdinalIgnoreCase);
    }

    private static string BlocoDoSeletor(string css, string seletor)
    {
        var texto = Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
        var inicio = 0;
        while (inicio < texto.Length)
        {
            var abre = texto.IndexOf('{', inicio);
            if (abre < 0)
                break;

            var fecha = texto.IndexOf('}', abre);
            if (fecha < 0)
                break;

            var cabeca = texto[inicio..abre];
            var seletores = cabeca.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (seletores.Any(item => item == seletor))
                return texto[abre..(fecha + 1)];

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
