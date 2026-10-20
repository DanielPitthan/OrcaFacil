using System.Text;

namespace OrcaFacil.Tests.Categorias;

/// <summary>
/// No Android 11 o diálogo da categoria recorta a paleta inline. O espectro abre
/// em overlay. Comentário no fonte não conta como a abertura real.
/// </summary>
public class PaletaCategoriaPopoverTests
{
    [Fact]
    public void PaletaAbreEmDialogo_EComentarioNaoConta()
    {
        var razor = File.ReadAllText(Caminho("OrcaFacil", "Components", "Shared", "ColorSwatchPicker.razor"));
        var limpo = SemComentarios(razor);

        Assert.Contains("PickerVariant=\"PickerVariant.Dialog\"", limpo, StringComparison.Ordinal);
        Assert.Contains("class=\"of-campo-paleta\"", limpo, StringComparison.Ordinal);
        Assert.DoesNotContain("width:180px", limpo, StringComparison.Ordinal);

        var mutado = razor.Replace(
            "PickerVariant=\"PickerVariant.Dialog\"",
            "@* PickerVariant=\"PickerVariant.Dialog\" *@",
            StringComparison.Ordinal);
        var mutadoLimpo = SemComentarios(mutado);

        Assert.DoesNotContain("PickerVariant.Dialog", mutadoLimpo, StringComparison.Ordinal);
        Assert.Contains("MudColorPicker", mutadoLimpo, StringComparison.Ordinal);
    }

    [Fact]
    public void OverlayDaPaletaNaoHerdaORecorteDoDialogo()
    {
        var css = File.ReadAllText(Caminho("OrcaFacil", "wwwroot", "css", "orcafacil-theme.css"));
        var limpo = SemComentarios(css);
        var paleta = Bloco(limpo, ".mud-overlay.mud-overlay-dialog .mud-picker.mud-dialog");
        var conteudo = Bloco(limpo, ".mud-overlay.mud-overlay-dialog .mud-picker.mud-dialog .mud-picker-content");
        var dialogo = Bloco(limpo, ".mud-dialog-container .mud-dialog");

        Assert.Contains("overflow: visible;", paleta, StringComparison.Ordinal);
        Assert.Contains("transform: none;", paleta, StringComparison.Ordinal);
        Assert.Contains("max-height: none;", paleta, StringComparison.Ordinal);
        Assert.Contains("animation: none;", paleta, StringComparison.Ordinal);
        Assert.Contains("overflow: visible;", conteudo, StringComparison.Ordinal);
        Assert.Contains("overflow: hidden;", dialogo, StringComparison.Ordinal);

        var mutado = css.Replace(
            "transform: none;",
            "/* transform: none; */",
            StringComparison.Ordinal);
        var paletaMutada = Bloco(SemComentarios(mutado), ".mud-overlay.mud-overlay-dialog .mud-picker.mud-dialog");

        Assert.DoesNotContain("transform: none", paletaMutada, StringComparison.Ordinal);
        Assert.Contains("overflow: visible;", paletaMutada, StringComparison.Ordinal);
    }

    [Fact]
    public void ComentarioNaoContaComoAberturaDaPaleta()
    {
        const string fonte = """
            @* PickerVariant="PickerVariant.Dialog" *@
            // PickerVariant.Dialog
            /* PickerVariant.Dialog */
            <!-- PickerVariant.Dialog -->
            <MudColorPicker Label="Outras cores" />
            """;

        var limpo = SemComentarios(fonte);

        Assert.DoesNotContain("PickerVariant.Dialog", limpo, StringComparison.Ordinal);
        Assert.Contains("MudColorPicker", limpo, StringComparison.Ordinal);
        Assert.Contains("Outras cores", limpo, StringComparison.Ordinal);
    }

    private static string Bloco(string css, string seletor)
    {
        var inicio = 0;
        while (inicio < css.Length)
        {
            var abre = css.IndexOf('{', inicio);
            if (abre < 0)
                break;

            var fecha = FecharBloco(css, abre);
            if (css[inicio..abre].Trim() == seletor)
                return css[abre..(fecha + 1)];

            inicio = fecha + 1;
        }

        throw new InvalidOperationException("Seletor não encontrado: " + seletor);
    }

    private static int FecharBloco(string css, int abre)
    {
        var profundidade = 0;
        for (var i = abre; i < css.Length; i++)
        {
            if (css[i] == '{')
                profundidade++;
            else if (css[i] == '}')
            {
                profundidade--;
                if (profundidade == 0)
                    return i;
            }
        }

        throw new InvalidOperationException("Bloco de CSS sem fechamento.");
    }

    private static string SemComentarios(string fonte)
    {
        var texto = new StringBuilder(fonte.Length);
        for (var i = 0; i < fonte.Length; i++)
        {
            var c = fonte[i];
            if (c == '"')
            {
                texto.Append(c);
                for (i++; i < fonte.Length; i++)
                {
                    texto.Append(fonte[i]);
                    if (fonte[i] == '\\' && i + 1 < fonte.Length)
                    {
                        texto.Append(fonte[++i]);
                        continue;
                    }

                    if (fonte[i] == '"')
                        break;
                }

                continue;
            }

            if (c == '@' && i + 1 < fonte.Length && fonte[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < fonte.Length && !(fonte[i] == '*' && fonte[i + 1] == '@'))
                    i++;
                if (i + 1 < fonte.Length)
                    i++;
                continue;
            }

            if (c == '<' && FonteComecaCom(fonte, i, "<!--"))
            {
                i += 4;
                while (i + 2 < fonte.Length && !FonteComecaCom(fonte, i, "-->"))
                    i++;
                if (i + 2 < fonte.Length)
                    i += 2;
                continue;
            }

            if (c == '/' && i + 1 < fonte.Length && fonte[i + 1] == '/')
            {
                while (i < fonte.Length && fonte[i] != '\n')
                    i++;
                if (i < fonte.Length)
                    texto.Append('\n');
                continue;
            }

            if (c == '/' && i + 1 < fonte.Length && fonte[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < fonte.Length && !(fonte[i] == '*' && fonte[i + 1] == '/'))
                    i++;
                if (i + 1 < fonte.Length)
                    i++;
                continue;
            }

            texto.Append(c);
        }

        return texto.ToString();
    }

    private static bool FonteComecaCom(string fonte, int indice, string trecho)
    {
        if (indice + trecho.Length > fonte.Length)
            return false;

        for (var i = 0; i < trecho.Length; i++)
        {
            if (fonte[indice + i] != trecho[i])
                return false;
        }

        return true;
    }

    private static string Caminho(params string[] partes)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OrcaFacil", "Components", "Shared", "ColorSwatchPicker.razor")))
                return Path.Combine(new[] { dir.FullName }.Concat(partes).ToArray());

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Não foi possível localizar o seletor de cor a partir de " + AppContext.BaseDirectory);
    }
}
