using System.Text;

namespace OrcaFacil.Tests.Categorias;

/// <summary>
/// A paleta personalizada da categoria abre por cima do conteúdo. Comentário
/// no .razor não conta como a abertura real.
/// </summary>
public class PaletaCategoriaPopoverTests
{
    [Fact]
    public void ComentarioNaoContaComoAberturaDaPaleta()
    {
        const string fonte = """
            @* PickerVariant="PickerVariant.Dialog" *@
            // PickerVariant.Dialog
            <MudColorPicker Label="Outras cores" />
            """;

        var limpo = SemComentarios(fonte);

        Assert.DoesNotContain("PickerVariant.Dialog", limpo, StringComparison.Ordinal);
        Assert.Contains("MudColorPicker", limpo, StringComparison.Ordinal);
        Assert.Contains("Outras cores", limpo, StringComparison.Ordinal);
    }

    private static string SemComentarios(string fonte)
    {
        var texto = new StringBuilder(fonte.Length);
        for (var i = 0; i < fonte.Length; i++)
        {
            if (fonte[i] == '@' && i + 1 < fonte.Length && fonte[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < fonte.Length && !(fonte[i] == '*' && fonte[i + 1] == '@'))
                    i++;
                if (i + 1 < fonte.Length)
                    i++;
                continue;
            }

            if (fonte[i] == '/' && i + 1 < fonte.Length && fonte[i + 1] == '/')
            {
                while (i < fonte.Length && fonte[i] != '\n')
                    i++;
                if (i < fonte.Length)
                    texto.Append('\n');
                continue;
            }

            texto.Append(fonte[i]);
        }

        return texto.ToString();
    }
}
