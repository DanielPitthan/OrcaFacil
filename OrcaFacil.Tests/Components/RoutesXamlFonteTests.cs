using System.Text;
using System.Text.RegularExpressions;

namespace OrcaFacil.Tests.Components;

/// <summary>
/// O XAML de MainPage referencia Routes. O tipo precisa existir em C# de verdade:
/// um comentário com a mesma declaração não deixa o APK sair.
/// </summary>
public class RoutesXamlFonteTests
{
    private static readonly Regex Declaracao = new(
        @"namespace\s+OrcaFacil\.Components\s*;\s*public\s+partial\s+class\s+Routes\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [Fact]
    public void MainPage_ApontaParaRoutes_ETipoExisteForaDeComentario()
    {
        var xaml = SemComentarios(Ler("OrcaFacil", "MainPage.xaml"));
        var parcial = SemComentarios(Ler("OrcaFacil", "Components", "Routes.razor.cs"));

        Assert.Contains("xmlns:components=\"clr-namespace:OrcaFacil.Components\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ComponentType=\"{x:Type components:Routes}\"", xaml, StringComparison.Ordinal);
        Assert.Matches(Declaracao, parcial);

        var classeComentada = SemComentarios(Ler("OrcaFacil", "Components", "Routes.razor.cs")
            .Replace("public partial class Routes", "/* public partial class Routes */", StringComparison.Ordinal));
        Assert.DoesNotMatch(Declaracao, classeComentada);

        var xamlComentado = SemComentarios(Ler("OrcaFacil", "MainPage.xaml")
            .Replace(
                "ComponentType=\"{x:Type components:Routes}\"",
                "<!-- ComponentType=\"{x:Type components:Routes}\" -->",
                StringComparison.Ordinal));
        Assert.DoesNotContain("ComponentType=\"{x:Type components:Routes}\"", xamlComentado, StringComparison.Ordinal);

        const string soComentario = """
            namespace OrcaFacil.Components;
            // public partial class Routes;
            /* public partial class Routes */
            <!-- ComponentType="{x:Type components:Routes}" -->
            """;
        var comentario = SemComentarios(soComentario);
        Assert.DoesNotMatch(Declaracao, comentario);
        Assert.DoesNotContain("ComponentType=\"{x:Type components:Routes}\"", comentario, StringComparison.Ordinal);
    }

    private static string Ler(params string[] partes)
        => File.ReadAllText(Path.Combine(new[] { Raiz() }.Concat(partes).ToArray()));

    private static string Raiz()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null)
        {
            if (File.Exists(Path.Combine(diretorio.FullName, "OrcaFacil.slnx")))
                return diretorio.FullName;

            diretorio = diretorio.Parent;
        }

        throw new InvalidOperationException("Raiz do repositório não encontrada.");
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

            if (c == '<' && ComecaCom(fonte, i, "<!--"))
            {
                i += 4;
                while (i + 2 < fonte.Length && !ComecaCom(fonte, i, "-->"))
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

    private static bool ComecaCom(string fonte, int indice, string trecho)
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
}
