using System.Text;

namespace OrcaFacil.Tests.Components;

/// <summary>
/// O patch grava só a cópia em obj e troca o StaticWebAsset. Comentário não conta.
/// </summary>
public class PatchBlazorWebViewFonteTests
{
    private const string Alvo =
        "Name=\"PatchBlazorWebViewForLegacyAndroid\"";

    private const string Entradas = "Inputs=\"@(_BlazorWebViewOriginal)\"";

    private const string Saida = "Outputs=\"$(_PatchedBlazorWebViewJs)\"";

    private const string PastaDaCopia =
        "<_PatchedBlazorWebViewDir>$(IntermediateOutputPath)patched-webview/</_PatchedBlazorWebViewDir>";

    private const string GravaCopia = "File.WriteAllText(DestinationFile, adjusted, utf8)";

    private const string TravaForaDoObj =
        "if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase))";

    [Fact]
    public void Alvo_TemEntradaOriginalESaidaNaCopia_EComentarioNaoConta()
    {
        var projeto = SemComentarios(Ler("OrcaFacil", "OrcaFacil.csproj"));

        Assert.Contains(Alvo, projeto, StringComparison.Ordinal);
        Assert.Contains(
            "BeforeTargets=\"GenerateStaticWebAssetsManifest;StaticWebAssetsPrepareForRun;ConvertStaticWebAssetsToMauiAssets\"",
            projeto,
            StringComparison.Ordinal);
        Assert.DoesNotContain(Entradas, projeto, StringComparison.Ordinal);
        Assert.DoesNotContain(Saida, projeto, StringComparison.Ordinal);
        Assert.Contains(PastaDaCopia, projeto, StringComparison.Ordinal);
        Assert.DoesNotContain("BeforeTargets=\"Build\"", projeto, StringComparison.Ordinal);
        Assert.DoesNotContain("powershell", projeto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pwsh", projeto, StringComparison.OrdinalIgnoreCase);

        var comIncremental = projeto.Replace(Alvo, Alvo + " " + Entradas + " " + Saida, StringComparison.Ordinal);
        Assert.Contains(Entradas, comIncremental, StringComparison.Ordinal);
        Assert.Contains(Saida, comIncremental, StringComparison.Ordinal);

        const string soComentario = """
            <!-- Inputs="@(_BlazorWebViewOriginal)" Outputs="$(_PatchedBlazorWebViewJs)" -->
            // <_PatchedBlazorWebViewDir>$(IntermediateOutputPath)patched-webview/</_PatchedBlazorWebViewDir>
            """;
        var comentario = SemComentarios(soComentario);
        Assert.DoesNotContain(Entradas, comentario, StringComparison.Ordinal);
        Assert.DoesNotContain(Saida, comentario, StringComparison.Ordinal);
        Assert.DoesNotContain(PastaDaCopia, comentario, StringComparison.Ordinal);
    }

    [Fact]
    public void StaticWebAsset_TrocaOOriginalPelaCopia()
    {
        var projeto = SemComentarios(Ler("OrcaFacil", "OrcaFacil.csproj"));

        Assert.Contains("<StaticWebAsset Remove=\"@(_BlazorWebViewOriginal)\" />", projeto, StringComparison.Ordinal);
        Assert.Contains("<StaticWebAsset Include=\"$(_PatchedBlazorWebViewJs)\">", projeto, StringComparison.Ordinal);
        Assert.Contains("<ContentRoot>$(_PatchedBlazorWebViewDir)</ContentRoot>", projeto, StringComparison.Ordinal);
        Assert.Contains("<BasePath>$(_BlazorWebViewBasePath)</BasePath>", projeto, StringComparison.Ordinal);
        Assert.Contains("<_BlazorWebViewBasePath>%(_BlazorWebViewOriginal.BasePath)</_BlazorWebViewBasePath>", projeto, StringComparison.Ordinal);
        Assert.Contains("<RelativePath>_framework/blazor.webview.js</RelativePath>", projeto, StringComparison.Ordinal);
        Assert.Contains("<SourceType>Discovered</SourceType>", projeto, StringComparison.Ordinal);
        Assert.Contains("<AssetKind>All</AssetKind>", projeto, StringComparison.Ordinal);
        Assert.Contains("<AssetMode>All</AssetMode>", projeto, StringComparison.Ordinal);
        Assert.Contains("<AssetRole>Primary</AssetRole>", projeto, StringComparison.Ordinal);
        Assert.Contains("<OriginalItemSpec>$(_PatchedBlazorWebViewJs)</OriginalItemSpec>", projeto, StringComparison.Ordinal);
        Assert.DoesNotContain("%(_BlazorWebViewOriginal.Fingerprint)", projeto, StringComparison.Ordinal);
        Assert.DoesNotContain("%(_BlazorWebViewOriginal.Integrity)", projeto, StringComparison.Ordinal);
        Assert.Contains("<Integrity>$(_PatchedBlazorWebViewIntegrity)</Integrity>", projeto, StringComparison.Ordinal);
        Assert.Contains("<Fingerprint>$(_PatchedBlazorWebViewFingerprint)</Fingerprint>", projeto, StringComparison.Ordinal);
        Assert.Contains("sha.ComputeHash(File.ReadAllBytes(DestinationFile))", projeto, StringComparison.Ordinal);
        Assert.Contains("<FileLength>$(_PatchedBlazorWebViewLength)</FileLength>", projeto, StringComparison.Ordinal);
        Assert.Contains("<LastWriteTime>$(_BlazorWebViewLastWriteTime)</LastWriteTime>", projeto, StringComparison.Ordinal);

        var trocaComentada = SemComentarios(Ler("OrcaFacil", "OrcaFacil.csproj")
            .Replace(
                "<StaticWebAsset Remove=\"@(_BlazorWebViewOriginal)\" />",
                "<!-- <StaticWebAsset Remove=\"@(_BlazorWebViewOriginal)\" /> -->",
                StringComparison.Ordinal));
        Assert.DoesNotContain("<StaticWebAsset Remove=\"@(_BlazorWebViewOriginal)\" />", trocaComentada, StringComparison.Ordinal);
    }

    [Fact]
    public void Tarefa_SoGravaDentroDoObj_EComentarioNaoConta()
    {
        var projeto = SemComentarios(Ler("OrcaFacil", "OrcaFacil.csproj"));

        Assert.Contains("ObjDirectory=\"$(MSBuildProjectDirectory)/obj\"", projeto, StringComparison.Ordinal);
        Assert.Contains("DestinationFile=\"$(_PatchedBlazorWebViewJs)\"", projeto, StringComparison.Ordinal);
        Assert.Contains(GravaCopia, projeto, StringComparison.Ordinal);
        Assert.Contains(TravaForaDoObj, projeto, StringComparison.Ordinal);
        Assert.Contains("Log.LogError(\"O patch só pode gravar dentro de obj: \" + dest)", projeto, StringComparison.Ordinal);
        Assert.DoesNotContain("File.WriteAllText(SourceFile", projeto, StringComparison.Ordinal);
        Assert.DoesNotContain("NuGetPackageRoot", projeto, StringComparison.Ordinal);
        Assert.DoesNotContain(".nuget", projeto, StringComparison.Ordinal);
        Assert.Equal(1, Contar(projeto, "File.WriteAllText("));

        var travaComentada = SemComentarios(Ler("OrcaFacil", "OrcaFacil.csproj")
            .Replace(TravaForaDoObj, "/* " + TravaForaDoObj + " */", StringComparison.Ordinal));
        Assert.DoesNotContain(TravaForaDoObj, travaComentada, StringComparison.Ordinal);

        var gravaNoOriginal = SemComentarios(Ler("OrcaFacil", "OrcaFacil.csproj")
            .Replace(GravaCopia, "File.WriteAllText(SourceFile, adjusted, utf8)", StringComparison.Ordinal));
        Assert.Contains("File.WriteAllText(SourceFile", gravaNoOriginal, StringComparison.Ordinal);
        Assert.DoesNotContain(GravaCopia, gravaNoOriginal, StringComparison.Ordinal);

        const string soComentario = """
            // File.WriteAllText(DestinationFile, adjusted, utf8)
            /* if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) */
            <!-- ObjDirectory="$(MSBuildProjectDirectory)/obj" -->
            """;
        var comentario = SemComentarios(soComentario);
        Assert.DoesNotContain(GravaCopia, comentario, StringComparison.Ordinal);
        Assert.DoesNotContain(TravaForaDoObj, comentario, StringComparison.Ordinal);
        Assert.DoesNotContain("ObjDirectory=\"$(MSBuildProjectDirectory)/obj\"", comentario, StringComparison.Ordinal);
    }

    [Fact]
    public void ScriptPowerShell_NaoFazParteDoProjeto()
    {
        Assert.False(File.Exists(Path.Combine(Raiz(), "OrcaFacil", "build", "PatchBlazorWebView.ps1")));
        var projeto = SemComentarios(Ler("OrcaFacil", "OrcaFacil.csproj"));
        Assert.DoesNotContain("PatchBlazorWebView.ps1", projeto, StringComparison.Ordinal);
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
