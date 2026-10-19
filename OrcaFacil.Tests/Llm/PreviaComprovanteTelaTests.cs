namespace OrcaFacil.Tests.Llm;

/// <summary>
/// A prévia reusa ItemList e StickyActionBar sem parâmetro obrigatório novo.
/// O destaque do não identificado passa de 4,5:1 no claro e no escuro.
/// </summary>
public class PreviaComprovanteTelaTests
{
    [Theory]
    [InlineData("#9B2C3A", "#ffffff")]
    [InlineData("#9B2C3A", "#EFF3F7")]
    [InlineData("#F59A9A", "#1B1F27")]
    [InlineData("#F59A9A", "#12151B")]
    [InlineData("#1E2A33", "#ffffff")]
    [InlineData("#ffffff", "#1E2A33")]
    [InlineData("#E8EAF0", "#1B1F27")]
    [InlineData("#1B1F27", "#E8EAF0")]
    public void CoresDaPrevia_PassamDeQuatroEMeio(string frente, string fundo)
    {
        var css = File.ReadAllText(Caminho("OrcaFacil", "wwwroot", "css", "orcafacil-theme.css"));
        Assert.Contains($"color: {frente};", css, StringComparison.Ordinal);
        Assert.Contains(fundo, css, StringComparison.Ordinal);
        var razao = Contraste(frente, fundo);
        Assert.True(razao >= 4.5, $"{frente} sobre {fundo} = {razao:0.00}");
    }

    [Fact]
    public void Dialogo_UsaOsComponentesExistentes_ESoCadastraAoConfirmar()
    {
        var dialog = File.ReadAllText(Caminho(
            "OrcaFacil", "Components", "Pages", "Cesta", "PreviaComprovanteDialog.razor"));
        var pagina = File.ReadAllText(Caminho(
            "OrcaFacil", "Components", "Pages", "Cesta", "CestaPage.razor"));
        var lista = File.ReadAllText(Caminho("OrcaFacil", "Components", "Shared", "ItemList.razor"));
        var barra = File.ReadAllText(Caminho("OrcaFacil", "Components", "Shared", "StickyActionBar.razor"));

        Assert.Contains("<FormSection", dialog, StringComparison.Ordinal);
        Assert.Contains("<ItemList", dialog, StringComparison.Ordinal);
        Assert.Contains("<ItemSheet", dialog, StringComparison.Ordinal);
        Assert.Contains("StickyActionBar Contido=\"true\"", dialog, StringComparison.Ordinal);
        Assert.Contains("PrimarioDesabilitado", dialog, StringComparison.Ordinal);
        Assert.Contains("EmErro=\"EmErro\"", dialog, StringComparison.Ordinal);
        Assert.Contains("MostrarAdicionar=\"false\"", dialog, StringComparison.Ordinal);
        Assert.Contains(">Cadastrar</MudButton>", dialog, StringComparison.Ordinal);
        Assert.Contains("ProdutoCestaDialog", dialog, StringComparison.Ordinal);
        Assert.Contains("IniciarCadastro", dialog, StringComparison.Ordinal);
        Assert.Contains("CancelarCadastro", dialog, StringComparison.Ordinal);
        Assert.Contains("ConfirmarCadastro", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("SalvarCompraAsync", dialog, StringComparison.Ordinal);

        Assert.Contains("SalvarCompraAsync(\"Nova compra\", modelo)", pagina, StringComparison.Ordinal);
        Assert.Contains("SalvarProdutoDaPreviaAsync", pagina, StringComparison.Ordinal);

        Assert.Contains("[Parameter] public Func<TItem, bool>? EmErro", lista, StringComparison.Ordinal);
        Assert.Contains("[Parameter] public bool MostrarAdicionar { get; set; } = true;", lista, StringComparison.Ordinal);
        Assert.DoesNotContain("EditorRequired] public Func<TItem, bool>? EmErro", lista, StringComparison.Ordinal);
        Assert.Contains("PrimarioDesabilitado", barra, StringComparison.Ordinal);
        Assert.Contains("public bool Contido { get; set; }", barra, StringComparison.Ordinal);
    }

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
