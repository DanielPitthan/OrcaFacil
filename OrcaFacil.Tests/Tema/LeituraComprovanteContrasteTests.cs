namespace OrcaFacil.Tests.Tema;

/// <summary>
/// Texto da leitura do comprovante. O azul primário #659ECD sobre branco fica em 2,86:1,
/// então o botão e o texto usam o tom do tema.
/// </summary>
public class LeituraComprovanteContrasteTests
{
    [Theory]
    [InlineData("html.of-tema-claro", "#1E2A33", "#ffffff")]
    [InlineData("html.of-tema-claro", "#9B2C3A", "#ffffff")]
    [InlineData("html.of-tema-escuro", "#E8EAF0", "#1B1F27")]
    [InlineData("html.of-tema-escuro", "#F59A9A", "#1B1F27")]
    public void TextoDaLeitura_PassaDeQuatroEMeio(string tema, string texto, string fundo)
    {
        var css = File.ReadAllText(CaminhoTema());
        Assert.Contains($"{tema} .of-leitura", css, StringComparison.Ordinal);
        Assert.Contains($"color: {texto};", css, StringComparison.Ordinal);
        Assert.Contains($"background: {fundo};", css, StringComparison.Ordinal);

        var razao = Contraste(texto, fundo);
        Assert.True(razao >= 4.5, $"{texto} sobre {fundo} = {razao:0.00}");
    }

    [Fact]
    public void BotaoTentarDeNovo_UsaOTextoDoTema_NosDoisModos()
    {
        var css = File.ReadAllText(CaminhoTema());

        Assert.Contains("html.of-tema-claro .of-leitura .mud-button-root.mud-button-outlined.of-tentar", css, StringComparison.Ordinal);
        Assert.Contains("html.of-tema-escuro .mud-dialog .mud-button-root.mud-button-outlined.of-tentar", css, StringComparison.Ordinal);
        Assert.Contains("Tentar de novo", File.ReadAllText(CaminhoPainel()), StringComparison.Ordinal);
    }

    private static string CaminhoTema() => Caminho("OrcaFacil", "wwwroot", "css", "orcafacil-theme.css");

    private static string CaminhoPainel() =>
        Caminho("OrcaFacil", "Components", "Pages", "Cesta", "LeituraComprovantePainel.razor");

    private static string Caminho(params string[] partes)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var caminho = Path.Combine(new[] { dir.FullName }.Concat(partes).ToArray());
            if (File.Exists(caminho))
                return caminho;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar " + string.Join('/', partes));
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
