using System.Text;

namespace OrcaFacil.Tests.Categorias;

/// <summary>
/// No 9.9.0 o overlay do PickerVariant.Dialog nasce dentro do papel da categoria.
/// A animação deixa transform e o overflow hidden recorta o position fixed.
/// Comentário no fonte não conta como regra.
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
    public void CampoFechadoMantem180px_EComentarioNaoConta()
    {
        var css = File.ReadAllText(Caminho("OrcaFacil", "wwwroot", "css", "orcafacil-theme.css"));
        var bloco = Bloco(SemComentarios(css), ".of-campo-paleta");

        Assert.Contains("width: 180px;", bloco, StringComparison.Ordinal);
        Assert.Contains("max-width: calc(100% - 32px);", bloco, StringComparison.Ordinal);

        var mutado = css.Replace(
            """
            .of-campo-paleta {
                width: 180px;
                max-width: calc(100% - 32px);
            }
            """,
            """
            .of-campo-paleta {
                /* width: 180px; */
                /* max-width: calc(100% - 32px); */
            }
            """,
            StringComparison.Ordinal);
        var blocoMutado = Bloco(SemComentarios(mutado), ".of-campo-paleta");

        Assert.DoesNotContain("width: 180px", blocoMutado, StringComparison.Ordinal);
        Assert.DoesNotContain("max-width: calc(100% - 32px)", blocoMutado, StringComparison.Ordinal);
    }

    [Fact]
    public void DialogoDaCategoriaNaoDeixaTransform_EComentarioNaoConta()
    {
        var razor = File.ReadAllText(Caminho("OrcaFacil", "Components", "Pages", "Categorias", "CategoriaForm.razor"));
        var css = File.ReadAllText(Caminho("OrcaFacil", "wwwroot", "css", "orcafacil-theme.css"));
        var limpo = SemComentarios(css);
        var bloco = Bloco(limpo, ".mud-dialog-container .mud-dialog.of-dialogo-categoria");
        var demais = Bloco(limpo, ".mud-dialog-container .mud-dialog");

        Assert.Contains("Class=\"of-dialogo-categoria\"", SemComentarios(razor), StringComparison.Ordinal);
        Assert.Contains("transform: none;", bloco, StringComparison.Ordinal);
        Assert.Contains("animation: none;", bloco, StringComparison.Ordinal);
        Assert.DoesNotContain(":has(", bloco, StringComparison.Ordinal);
        Assert.Contains("overflow: hidden;", demais, StringComparison.Ordinal);

        var razorMutado = razor.Replace(
            "Class=\"of-dialogo-categoria\"",
            "@* Class=\"of-dialogo-categoria\" *@",
            StringComparison.Ordinal);
        Assert.DoesNotContain("of-dialogo-categoria", SemComentarios(razorMutado), StringComparison.Ordinal);

        var cssMutado = css.Replace(
            """
            .mud-dialog-container .mud-dialog.of-dialogo-categoria {
                transform: none;
                animation: none;
            }
            """,
            """
            .mud-dialog-container .mud-dialog.of-dialogo-categoria {
                /* transform: none; */
                /* animation: none; */
            }
            """,
            StringComparison.Ordinal);
        var blocoMutado = Bloco(SemComentarios(cssMutado), ".mud-dialog-container .mud-dialog.of-dialogo-categoria");

        Assert.DoesNotContain("transform: none", blocoMutado, StringComparison.Ordinal);
        Assert.DoesNotContain("animation: none", blocoMutado, StringComparison.Ordinal);
    }

    [Fact]
    public void PapelDaPaletaRolaEmTelaBaixa_EComentarioNaoConta()
    {
        var css = File.ReadAllText(Caminho("OrcaFacil", "wwwroot", "css", "orcafacil-theme.css"));
        var bloco = Bloco(SemComentarios(css), ".mud-overlay.mud-overlay-dialog .mud-picker.mud-dialog");

        Assert.Contains("max-height: calc(100vh - 32px);", bloco, StringComparison.Ordinal);
        Assert.Contains("max-height: calc(100dvh - 32px);", bloco, StringComparison.Ordinal);
        Assert.Contains("overflow-x: hidden;", bloco, StringComparison.Ordinal);
        Assert.Contains("overflow-y: auto;", bloco, StringComparison.Ordinal);
        Assert.Contains("max-width: calc(100% - 32px);", bloco, StringComparison.Ordinal);

        var mutado = css.Replace(
            """
                max-height: calc(100vh - 32px);
                max-height: calc(100dvh - 32px);
                overflow-x: hidden;
                overflow-y: auto;
                transform: none;
                animation: none;
            """,
            """
                /* max-height: calc(100vh - 32px); */
                /* max-height: calc(100dvh - 32px); */
                /* overflow-x: hidden; */
                /* overflow-y: auto; */
                transform: none;
                animation: none;
            """,
            StringComparison.Ordinal);
        var blocoMutado = Bloco(SemComentarios(mutado), ".mud-overlay.mud-overlay-dialog .mud-picker.mud-dialog");

        Assert.DoesNotContain("100dvh - 32px", blocoMutado, StringComparison.Ordinal);
        Assert.DoesNotContain("overflow-y: auto", blocoMutado, StringComparison.Ordinal);
        Assert.Contains("max-width: calc(100% - 32px);", blocoMutado, StringComparison.Ordinal);
    }

    [Fact]
    public void BordaDosCamposRgbFicaSoNoPapelDaPaleta_EComentarioNaoConta()
    {
        var css = File.ReadAllText(Caminho("OrcaFacil", "wwwroot", "css", "orcafacil-theme.css"));
        var limpo = SemComentarios(css);
        var paleta = Bloco(limpo, ".mud-overlay.mud-overlay-dialog .mud-picker.mud-dialog");
        var categoria = Bloco(limpo, ".mud-dialog-container .mud-dialog.of-dialogo-categoria");

        Assert.Contains("--mud-palette-lines-inputs: #767676;", paleta, StringComparison.Ordinal);
        Assert.DoesNotContain("--mud-palette-lines-inputs", categoria, StringComparison.Ordinal);
        Assert.DoesNotContain(":has(", limpo, StringComparison.Ordinal);

        var mutado = css.Replace(
            "--mud-palette-lines-inputs: #767676;",
            "/* --mud-palette-lines-inputs: #767676; */",
            StringComparison.Ordinal);
        var paletaMutada = Bloco(SemComentarios(mutado), ".mud-overlay.mud-overlay-dialog .mud-picker.mud-dialog");

        Assert.DoesNotContain("#767676", paletaMutada, StringComparison.Ordinal);
        Assert.Contains("overflow-y: auto;", paletaMutada, StringComparison.Ordinal);
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
