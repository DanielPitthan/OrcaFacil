namespace OrcaFacil.Tests.Tema;

public class ContrasteRecuperacaoTests
{
    [Theory]
    [InlineData("#1E2A33", "#659ECD")]
    [InlineData("#0B1220", "#6EA8FE")]
    [InlineData("#1E2A33", "#FFFFFF")]
    [InlineData("#1E2A33", "#EFF3F7")]
    [InlineData("#E8EAF0", "#12151B")]
    [InlineData("#E8EAF0", "#1B1F27")]
    [InlineData("#426785", "#FFFFFF")]
    [InlineData("#426785", "#EFF3F7")]
    [InlineData("#B2CFE6", "#1B1F27")]
    [InlineData("#5A6B78", "#FFFFFF")]
    [InlineData("#5A6B78", "#EFF3F7")]
    [InlineData("#C9D1DD", "#1B1F27")]
    public void ParesDaTelaDeRecuperacao_TemContrasteDeTexto(string frente, string fundo)
    {
        Assert.True(Contraste(frente, fundo) >= 4.5, $"{frente} sobre {fundo} = {Contraste(frente, fundo):0.00}");
    }

    [Fact]
    public void CssDaRecuperacao_SegueClaroEEscuroSemFicarPresoNoFundoClaro()
    {
        var css = File.ReadAllText(CaminhoCss());

        Assert.Contains("light-dark(#EFF3F7, #12151B)", css, StringComparison.Ordinal);
        Assert.Contains("light-dark(#659ECD, #6EA8FE)", css, StringComparison.Ordinal);
        Assert.Contains("light-dark(#1E2A33, #0B1220)", css, StringComparison.Ordinal);
        Assert.Contains("html.of-tema-escuro", css, StringComparison.Ordinal);
        Assert.Contains("html.of-tema-claro", css, StringComparison.Ordinal);
        Assert.DoesNotContain("color: #ffffff", css, StringComparison.Ordinal);
    }

    private static string CaminhoCss()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var caminho = Path.Combine(dir.FullName, "OrcaFacil", "Components", "Pages", "RecuperacaoInicializacao.razor.css");
            if (File.Exists(caminho))
                return caminho;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar o CSS da recuperação.");
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
