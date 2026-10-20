namespace OrcaFacil.Tests.Components;

/// <summary>
/// ItemList ganhou MostrarMenu, opcional e verdadeiro por padrão.
/// As telas que já usavam a lista não passam o parâmetro, então o menu continua.
/// </summary>
public class ItemListMenuOpcionalTests
{
    [Fact]
    public void MenuContinuaPorPadrao_EAsTelasAtuaisNaoPassamOParametro()
    {
        var raiz = EncontrarRaiz();
        var lista = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Shared", "ItemList.razor"));
        var barra = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Shared", "StickyActionBar.razor"));

        Assert.Contains("[Parameter] public bool MostrarMenu { get; set; } = true;", lista, StringComparison.Ordinal);
        Assert.Contains("@if (MostrarMenu)", lista, StringComparison.Ordinal);
        Assert.Contains("Editar", lista, StringComparison.Ordinal);
        Assert.Contains("Remover", lista, StringComparison.Ordinal);
        Assert.Contains("[Parameter] public ButtonType TipoPrimario { get; set; } = ButtonType.Submit;", barra, StringComparison.Ordinal);
        Assert.Contains("[Parameter] public bool Contido { get; set; }", barra, StringComparison.Ordinal);

        foreach (var tela in new[]
        {
            Path.Combine("OrcaFacil", "Components", "Pages", "Lancamentos", "FaturaCartaoPainel.razor"),
            Path.Combine("OrcaFacil", "Components", "Pages", "Cesta", "CompraCestaDialog.razor"),
            Path.Combine("OrcaFacil", "Components", "Pages", "Cesta", "PreviaComprovanteDialog.razor")
        })
        {
            var texto = File.ReadAllText(Path.Combine(raiz, tela));
            Assert.Contains("<ItemList ", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("MostrarMenu", texto, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void LinkDoExtratoFicaSoNoPainelDeAtivos()
    {
        var raiz = EncontrarRaiz();
        var ativos = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Pages", "Ativos", "AtivosList.razor"));
        var menu = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Layout", "NavMenu.razor"));
        var layout = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Layout", "MainLayout.razor"));

        Assert.Contains("@page \"/ativos/painel\"", ativos, StringComparison.Ordinal);
        Assert.Contains("Href=\"/ativos/importar-b3\"", ativos, StringComparison.Ordinal);
        Assert.DoesNotContain("importar-b3", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("importar-b3", layout, StringComparison.Ordinal);
    }

    private static string EncontrarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OrcaFacil", "Components", "Shared", "ItemList.razor")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
