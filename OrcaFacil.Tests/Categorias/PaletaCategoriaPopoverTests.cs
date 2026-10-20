using System.Text;

namespace OrcaFacil.Tests.Categorias;

/// <summary>
/// A paleta abre num diálogo próprio do DialogService. Comentário no fonte não conta.
/// </summary>
public class PaletaCategoriaPopoverTests
{
    [Fact]
    public void PaletaAbrePorDialogService_ForaDaCategoria_EComentarioNaoConta()
    {
        var seletor = SemComentarios(Ler("OrcaFacil", "Components", "Shared", "ColorSwatchPicker.razor"));
        var dialogo = SemComentarios(Ler("OrcaFacil", "Components", "Shared", "PaletaCategoriaDialog.razor"));
        var categoria = SemComentarios(Ler("OrcaFacil", "Components", "Pages", "Categorias", "CategoriaForm.razor"));

        Assert.Contains("DialogService.ShowAsync<PaletaCategoriaDialog>", seletor, StringComparison.Ordinal);
        Assert.Contains("CloseOnEscapeKey = true", seletor, StringComparison.Ordinal);
        Assert.Contains("BackdropClick = true", seletor, StringComparison.Ordinal);
        Assert.Contains("DecisaoCorPaleta.Resolver", seletor, StringComparison.Ordinal);
        Assert.DoesNotContain("MudColorPicker", seletor, StringComparison.Ordinal);
        Assert.DoesNotContain("PickerVariant.Inline", seletor, StringComparison.Ordinal);
        Assert.DoesNotContain("PickerVariant.Dialog", seletor, StringComparison.Ordinal);
        Assert.Contains("PickerVariant=\"PickerVariant.Static\"", dialogo, StringComparison.Ordinal);
        Assert.DoesNotContain("PickerVariant.Inline", dialogo, StringComparison.Ordinal);
        Assert.DoesNotContain("PickerVariant.Dialog", dialogo, StringComparison.Ordinal);
        Assert.DoesNotContain("MudColorPicker", categoria, StringComparison.Ordinal);
        Assert.DoesNotContain("of-dialogo-categoria", categoria, StringComparison.Ordinal);

        var mutado = SemComentarios(Ler("OrcaFacil", "Components", "Shared", "ColorSwatchPicker.razor").Replace(
            "DialogService.ShowAsync<PaletaCategoriaDialog>",
            "@* DialogService.ShowAsync<PaletaCategoriaDialog> *@",
            StringComparison.Ordinal));

        Assert.DoesNotContain("ShowAsync<PaletaCategoriaDialog>", mutado, StringComparison.Ordinal);
        Assert.Contains("DecisaoCorPaleta.Resolver", mutado, StringComparison.Ordinal);
    }

    [Fact]
    public void CorSoEntraNoFormularioNoOk_EComentarioNaoConta()
    {
        var seletor = SemComentarios(Ler("OrcaFacil", "Components", "Shared", "ColorSwatchPicker.razor"));

        Assert.Contains("var confirmou = resultado is { Canceled: false };", seletor, StringComparison.Ordinal);
        Assert.Contains("DecisaoCorPaleta.Resolver(Value, resultado?.Data as string, confirmou)", seletor, StringComparison.Ordinal);
        Assert.Contains("if (string.Equals(cor, Value, StringComparison.OrdinalIgnoreCase))", seletor, StringComparison.Ordinal);

        var mutado = SemComentarios(Ler("OrcaFacil", "Components", "Shared", "ColorSwatchPicker.razor").Replace(
            "DecisaoCorPaleta.Resolver(Value, resultado?.Data as string, confirmou)",
            "/* DecisaoCorPaleta.Resolver(Value, resultado?.Data as string, confirmou) */",
            StringComparison.Ordinal));

        Assert.DoesNotContain("DecisaoCorPaleta.Resolver", mutado, StringComparison.Ordinal);
        Assert.Contains("ShowAsync<PaletaCategoriaDialog>", mutado, StringComparison.Ordinal);
    }

    [Fact]
    public void CampoFechadoMantem180px_EComentarioNaoConta()
    {
        var css = Ler("OrcaFacil", "wwwroot", "css", "orcafacil-theme.css");
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
    public void AmostrasTemNomeAcessivel_EComentarioNaoConta()
    {
        var seletor = SemComentarios(Ler("OrcaFacil", "Components", "Shared", "ColorSwatchPicker.razor"));

        Assert.Contains("<button type=\"button\"", seletor, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"@cor\"", seletor, StringComparison.Ordinal);
        Assert.Contains("aria-pressed=", seletor, StringComparison.Ordinal);
        Assert.Contains("class=\"of-color-swatch", seletor, StringComparison.Ordinal);

        var mutado = SemComentarios(Ler("OrcaFacil", "Components", "Shared", "ColorSwatchPicker.razor").Replace(
            "aria-label=\"@cor\"",
            "@* aria-label=\"@cor\" *@",
            StringComparison.Ordinal).Replace(
            "aria-pressed=",
            "@* aria-pressed= *@",
            StringComparison.Ordinal));

        Assert.DoesNotContain("aria-label=\"@cor\"", mutado, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-pressed=", mutado, StringComparison.Ordinal);
        Assert.Contains("<button type=\"button\"", mutado, StringComparison.Ordinal);
    }

    [Fact]
    public void RegrasDeTelaBaixaFicamSoNoDialogoDaPaleta_EComentarioNaoConta()
    {
        var css = Ler("OrcaFacil", "wwwroot", "css", "orcafacil-theme.css");
        var limpo = SemComentarios(css);
        var paleta = Bloco(limpo, ".mud-dialog-container .mud-dialog.of-dialogo-paleta");

        Assert.Contains("width: min(392px, calc(100% - 16px));", paleta, StringComparison.Ordinal);
        Assert.Contains("max-width: calc(100% - 16px);", paleta, StringComparison.Ordinal);
        Assert.Contains("max-height: calc(100vh - 32px);", paleta, StringComparison.Ordinal);
        Assert.Contains("max-height: calc(100dvh - 32px);", paleta, StringComparison.Ordinal);
        Assert.Contains("overflow-x: hidden;", paleta, StringComparison.Ordinal);
        Assert.Contains("overflow-y: auto;", paleta, StringComparison.Ordinal);
        Assert.Contains("--mud-palette-lines-inputs: #767676;", paleta, StringComparison.Ordinal);
        Assert.Contains("width: 100%;", Bloco(limpo, ".mud-dialog-container .mud-dialog.of-dialogo-paleta .mud-picker-color-picker"), StringComparison.Ordinal);
        Assert.Contains("max-width: 312px;", Bloco(limpo, ".mud-dialog-container .mud-dialog.of-dialogo-paleta .mud-picker-color-picker"), StringComparison.Ordinal);
        Assert.Contains("width: 100%;", Bloco(limpo, ".mud-dialog-container .mud-dialog.of-dialogo-paleta .mud-picker-color-controls"), StringComparison.Ordinal);
        Assert.Contains("padding-left: 0;", Bloco(limpo, ".mud-dialog-container .mud-dialog.of-dialogo-paleta .mud-picker-color-controls"), StringComparison.Ordinal);
        Assert.Contains("margin-inline-end: 0 !important;", Bloco(limpo, ".mud-dialog-container .mud-dialog.of-dialogo-paleta .mud-picker-control-switch .mud-icon-button"), StringComparison.Ordinal);
        Assert.DoesNotContain("of-dialogo-categoria", limpo, StringComparison.Ordinal);
        Assert.DoesNotContain(".mud-overlay.mud-overlay-dialog .mud-picker.mud-dialog", limpo, StringComparison.Ordinal);
        Assert.DoesNotContain(":has(", limpo, StringComparison.Ordinal);

        var mutado = css.Replace(
            "--mud-palette-lines-inputs: #767676;",
            "/* --mud-palette-lines-inputs: #767676; */",
            StringComparison.Ordinal);
        var paletaMutada = Bloco(SemComentarios(mutado), ".mud-dialog-container .mud-dialog.of-dialogo-paleta");

        Assert.DoesNotContain("#767676", paletaMutada, StringComparison.Ordinal);
        Assert.Contains("max-height: calc(100dvh - 32px);", paletaMutada, StringComparison.Ordinal);
    }

    [Fact]
    public void ComentarioNaoContaComoAberturaDaPaleta()
    {
        const string fonte = """
            @* DialogService.ShowAsync<PaletaCategoriaDialog> *@
            // PickerVariant.Static
            /* aria-label="@cor" */
            <!-- DecisaoCorPaleta.Resolver -->
            <button type="button">Outras cores</button>
            """;

        var limpo = SemComentarios(fonte);

        Assert.DoesNotContain("PaletaCategoriaDialog", limpo, StringComparison.Ordinal);
        Assert.DoesNotContain("PickerVariant.Static", limpo, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label", limpo, StringComparison.Ordinal);
        Assert.DoesNotContain("DecisaoCorPaleta", limpo, StringComparison.Ordinal);
        Assert.Contains("Outras cores", limpo, StringComparison.Ordinal);
    }

    [Fact]
    public void SegundoCliqueNaoAbreOutraPaleta_EComentarioNaoConta()
    {
        var seletor = SemComentarios(Ler("OrcaFacil", "Components", "Shared", "ColorSwatchPicker.razor"));
        var guarda = seletor.IndexOf("if (_abrindo)", StringComparison.Ordinal);
        var trava = seletor.IndexOf("_abrindo = true", StringComparison.Ordinal);
        var primeiroAwait = seletor.IndexOf("await DialogService", StringComparison.Ordinal);
        var solta = seletor.IndexOf("_abrindo = false", StringComparison.Ordinal);

        Assert.True(guarda >= 0 && guarda < trava && trava < primeiroAwait && primeiroAwait < solta);
        Assert.Contains("finally", seletor, StringComparison.Ordinal);
        Assert.Contains("MaxWidth = MaxWidth.ExtraSmall", seletor, StringComparison.Ordinal);
        Assert.Contains("FullWidth = true", seletor, StringComparison.Ordinal);

        var mutado = SemComentarios(Ler("OrcaFacil", "Components", "Shared", "ColorSwatchPicker.razor").Replace(
            "if (_abrindo)\n            return;",
            "/* if (_abrindo)\n            return; */",
            StringComparison.Ordinal));

        Assert.DoesNotContain("if (_abrindo)", mutado, StringComparison.Ordinal);
        Assert.True(mutado.IndexOf("_abrindo = true", StringComparison.Ordinal) < mutado.IndexOf("await DialogService", StringComparison.Ordinal));
    }

    [Fact]
    public void ArrastarNaoFechaNemGrava_EComentarioNaoConta()
    {
        var fonte = Ler("OrcaFacil", "Components", "Shared", "PaletaCategoriaDialog.razor");
        var dialogo = SemComentarios(fonte);
        var corpo = Metodo(dialogo, "AoArrastar");

        Assert.Contains("ValueChanged=\"AoArrastar\"", dialogo, StringComparison.Ordinal);
        Assert.True(SoAtualizaEscolha(corpo));
        Assert.Contains("DefaultFocus=\"DefaultFocus.FirstChild\"", dialogo, StringComparison.Ordinal);

        var mutado = SemComentarios(fonte.Replace(
            "private void AoArrastar(MudColor cor) => _escolha = cor;",
            "private void AoArrastar(MudColor cor) { _escolha = cor; MudDialog.Close(DialogResult.Ok(cor)); }",
            StringComparison.Ordinal));

        Assert.False(SoAtualizaEscolha(Metodo(mutado, "AoArrastar")));
    }

    [Fact]
    public void FocoDaAmostraTemFallbackEContraste_EComentarioNaoConta()
    {
        var css = Ler("OrcaFacil", "wwwroot", "css", "orcafacil-theme.css");
        var limpo = SemComentarios(css);
        var foco = Bloco(limpo, ".of-color-swatch:focus");
        var visivel = Bloco(limpo, ".of-color-swatch:focus-visible");

        Assert.Contains("outline: 2px solid #1E2A33;", foco, StringComparison.Ordinal);
        Assert.Contains("outline: 2px solid #1E2A33;", visivel, StringComparison.Ordinal);
        Assert.Contains("outline-color: #E8EAF0;", Bloco(limpo, "html.of-tema-escuro .of-color-swatch:focus"), StringComparison.Ordinal);
        Assert.True(Contraste("#1E2A33", "#ffffff") >= 4.5);
        Assert.True(Contraste("#E8EAF0", "#1B1F27") >= 4.5);

        var mutado = SemComentarios(css.Replace(
            """
            .of-color-swatch:focus {
                outline: 2px solid #1E2A33;
                outline-offset: 2px;
            }
            """,
            """
            .of-color-swatch:focus {
                /* outline: 2px solid #1E2A33; */
                outline-offset: 2px;
            }
            """,
            StringComparison.Ordinal));

        Assert.DoesNotContain("outline: 2px solid #1E2A33;", Bloco(mutado, ".of-color-swatch:focus"), StringComparison.Ordinal);
        Assert.Contains("outline: 2px solid #1E2A33;", Bloco(mutado, ".of-color-swatch:focus-visible"), StringComparison.Ordinal);
    }

    [Fact]
    public void RotuloOutrasCoresPassaDeQuatroEMeio()
    {
        var css = SemComentarios(Ler("OrcaFacil", "wwwroot", "css", "orcafacil-theme.css"));
        var claro = Bloco(css, ".of-abrir-paleta-rotulo");
        var escuro = Bloco(css, "html.of-tema-escuro .of-abrir-paleta-rotulo");

        Assert.Contains("color: #5A6B78;", claro, StringComparison.Ordinal);
        Assert.Contains("color: #C9D1DD;", escuro, StringComparison.Ordinal);
        Assert.True(Contraste("#5A6B78", "#ffffff") >= 4.5);
        Assert.True(Contraste("#5A6B78", "#EFF3F7") >= 4.5);
        Assert.True(Contraste("#C9D1DD", "#1B1F27") >= 4.5);
        Assert.True(Contraste("#C9D1DD", "#12151B") >= 4.5);
    }

    [Fact]
    public void DecisaoNaoPrometeVoltarDoAndroid()
    {
        var fonte = Ler("OrcaFacil.Core", "Categorias", "DecisaoCorPaleta.cs");

        Assert.DoesNotContain("voltar", fonte, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cancelar", fonte, StringComparison.Ordinal);
    }

    private static bool SoAtualizaEscolha(string corpo) =>
        !corpo.Contains("Close", StringComparison.Ordinal)
        && !corpo.Contains("Resolver", StringComparison.Ordinal)
        && !corpo.Contains("InvokeAsync", StringComparison.Ordinal);

    private static string Metodo(string fonte, string nome)
    {
        var inicio = fonte.IndexOf("void " + nome, StringComparison.Ordinal);
        if (inicio < 0)
            throw new InvalidOperationException("Método não encontrado: " + nome);

        var flecha = fonte.IndexOf("=>", inicio, StringComparison.Ordinal);
        var chave = fonte.IndexOf('{', inicio);
        if (flecha >= 0 && (chave < 0 || flecha < chave))
        {
            var fim = fonte.IndexOf(';', flecha);
            return fonte[inicio..(fim + 1)];
        }

        if (chave < 0)
            throw new InvalidOperationException("Método sem corpo: " + nome);

        return fonte[inicio..(FecharBloco(fonte, chave) + 1)];
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

    private static string Ler(params string[] partes)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OrcaFacil", "Components", "Shared", "ColorSwatchPicker.razor")))
                return File.ReadAllText(Path.Combine(new[] { dir.FullName }.Concat(partes).ToArray()));

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Não foi possível localizar o seletor de cor a partir de " + AppContext.BaseDirectory);
    }
}
