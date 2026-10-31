using System.Text;

namespace OrcaFacil.Tests.Components;

/// <summary>
/// O patch do blazor.webview.js entra no APK só se rodar antes de virar MauiAsset,
/// sem PowerShell. Comentário com o mesmo texto não mantém a trava.
/// </summary>
public class PatchBlazorWebViewFonteTests
{
    private const string Alvo =
        "Name=\"PatchBlazorWebViewForLegacyAndroid\" BeforeTargets=\"ConvertStaticWebAssetsToMauiAssets\"";

    [Fact]
    public void Alvo_RodaAntesDosAssets_SemPowerShell_EComentarioNaoConta()
    {
        var projeto = SemComentarios(Ler("OrcaFacil", "OrcaFacil.csproj"));

        Assert.Contains(Alvo, projeto, StringComparison.Ordinal);
        Assert.Contains("PackageRoot=\"$(NuGetPackageRoot)\"", projeto, StringComparison.Ordinal);
        Assert.Contains("ObjDirectory=\"$(MSBuildProjectDirectory)/obj\"", projeto, StringComparison.Ordinal);
        Assert.Contains(".Replace(\"static{this.nextEventDelegatorId=0}\", \"static nextEventDelegatorId=0;\")", projeto, StringComparison.Ordinal);
        Assert.Contains("adjusted.Contains(\"static{\")", projeto, StringComparison.Ordinal);
        Assert.Contains("Log.LogError(\"blazor.webview.js não encontrado.\")", projeto, StringComparison.Ordinal);
        Assert.Contains("Log.LogError(\"O padrão static{ permaneceu em \" + file)", projeto, StringComparison.Ordinal);
        Assert.DoesNotContain("powershell", projeto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pwsh", projeto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BeforeTargets=\"Build\"", projeto, StringComparison.Ordinal);

        var alvoComentado = SemComentarios(Ler("OrcaFacil", "OrcaFacil.csproj")
            .Replace(Alvo, "<!-- " + Alvo + " -->", StringComparison.Ordinal));
        Assert.DoesNotContain(Alvo, alvoComentado, StringComparison.Ordinal);

        var deVoltaAoBuild = SemComentarios(Ler("OrcaFacil", "OrcaFacil.csproj")
            .Replace(
                "BeforeTargets=\"ConvertStaticWebAssetsToMauiAssets\"",
                "BeforeTargets=\"Build\"",
                StringComparison.Ordinal));
        Assert.Contains("BeforeTargets=\"Build\"", deVoltaAoBuild, StringComparison.Ordinal);
        Assert.DoesNotContain(Alvo, deVoltaAoBuild, StringComparison.Ordinal);

        const string soComentario = """
            <!-- Name="PatchBlazorWebViewForLegacyAndroid" BeforeTargets="ConvertStaticWebAssetsToMauiAssets" -->
            // powershell -NoProfile -File PatchBlazorWebView.ps1
            /* pwsh */
            """;
        var comentario = SemComentarios(soComentario);
        Assert.DoesNotContain(Alvo, comentario, StringComparison.Ordinal);
        Assert.DoesNotContain("powershell", comentario, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pwsh", comentario, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Script_UsaHome_JoinPath_EFalhaSeNaoAcharOuSobrarStatic_EComentarioNaoConta()
    {
        var script = SemComentarios(Ler("OrcaFacil", "build", "PatchBlazorWebView.ps1"));

        Assert.Contains("$perfil = $env:USERPROFILE", script, StringComparison.Ordinal);
        Assert.Contains("$perfil = $env:HOME", script, StringComparison.Ordinal);
        Assert.Contains("Join-Path $perfil \".nuget\"", script, StringComparison.Ordinal);
        Assert.Contains("Join-Path $pastaNuget \"packages\"", script, StringComparison.Ordinal);
        Assert.Contains("Join-Path $pastaPacotes \"microsoft.aspnetcore.components.webview\"", script, StringComparison.Ordinal);
        Assert.Contains("Join-Path $PSScriptRoot \"..\"", script, StringComparison.Ordinal);
        Assert.Contains("Join-Path $pastaPai \"obj\"", script, StringComparison.Ordinal);
        Assert.Contains("Write-Host \"blazor.webview.js não encontrado.\"", script, StringComparison.Ordinal);
        Assert.Contains("exit 1", script, StringComparison.Ordinal);
        Assert.Contains("$ajustado.Contains('static{')", script, StringComparison.Ordinal);
        Assert.Contains("if ($restouStatic)", script, StringComparison.Ordinal);
        Assert.Equal(2, Contar(script, "exit 1"));
        Assert.DoesNotContain("exit 0", script, StringComparison.Ordinal);
        Assert.DoesNotContain(".nuget\\packages", script, StringComparison.Ordinal);

        var semSaida = SemComentarios(Ler("OrcaFacil", "build", "PatchBlazorWebView.ps1")
            .Replace("exit 1", "/* exit 1 */", StringComparison.Ordinal));
        Assert.DoesNotContain("exit 1", semSaida, StringComparison.Ordinal);
        Assert.Equal(0, Contar(semSaida, "exit 1"));

        var homeComentado = SemComentarios(Ler("OrcaFacil", "build", "PatchBlazorWebView.ps1")
            .Replace("$perfil = $env:HOME", "# $perfil = $env:HOME", StringComparison.Ordinal));
        Assert.DoesNotContain("$perfil = $env:HOME", homeComentado, StringComparison.Ordinal);

        const string soComentario = """
            # $perfil = $env:USERPROFILE
            # $perfil = $env:HOME
            # Join-Path $perfil ".nuget"
            # exit 1
            # static{
            """;
        var comentario = SemComentarios(soComentario);
        Assert.DoesNotContain("$perfil = $env:HOME", comentario, StringComparison.Ordinal);
        Assert.DoesNotContain("exit 1", comentario, StringComparison.Ordinal);
        Assert.DoesNotContain("static{", comentario, StringComparison.Ordinal);
    }

    private static int Contar(string fonte, string trecho)
    {
        var total = 0;
        var cursor = 0;
        while ((cursor = fonte.IndexOf(trecho, cursor, StringComparison.Ordinal)) >= 0)
        {
            total++;
            cursor += trecho.Length;
        }

        return total;
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

            if (c == '\'')
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

                    if (fonte[i] == '\'')
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

            if (c == '#' && ComentarioDeLinha(fonte, i))
            {
                while (i < fonte.Length && fonte[i] != '\n')
                    i++;
                if (i < fonte.Length)
                    texto.Append('\n');
                continue;
            }

            texto.Append(c);
        }

        return texto.ToString();
    }

    private static bool ComentarioDeLinha(string fonte, int indice)
    {
        for (var i = indice - 1; i >= 0; i--)
        {
            if (fonte[i] == '\n')
                return true;
            if (fonte[i] != ' ' && fonte[i] != '\t' && fonte[i] != '\r')
                return false;
        }

        return true;
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
