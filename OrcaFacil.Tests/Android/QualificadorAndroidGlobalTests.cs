using System.Text.RegularExpressions;

namespace OrcaFacil.Tests.Android;

/// <summary>
/// Dentro de OrcaFacil.Platforms.Android o nome Android sombreia o SDK e o
/// compilador emite CS0234. Toda referência ao SDK leva global::.
/// </summary>
public class QualificadorAndroidGlobalTests
{
    [Fact]
    public void PlataformaAndroid_QualificaOSdkComGlobal()
    {
        var pasta = Path.Combine(Raiz(), "OrcaFacil", "Platforms", "Android");
        var erros = new List<string>();

        foreach (var arquivo in Directory.EnumerateFiles(pasta, "*.cs", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
        {
            var texto = ApagarComentarios(File.ReadAllText(arquivo));
            foreach (Match ocorrencia in Regex.Matches(texto, "Android\\."))
            {
                if (PrecedidoPor(texto, ocorrencia.Index, "global::")
                    || PrecedidoPor(texto, ocorrencia.Index, "Platforms."))
                    continue;

                var linha = texto.Take(ocorrencia.Index).Count(c => c == '\n') + 1;
                erros.Add($"{Path.GetRelativePath(Raiz(), arquivo)}:{linha} use global::Android.");
            }
        }

        Assert.True(erros.Count == 0, string.Join(Environment.NewLine, erros));
    }

    private static bool PrecedidoPor(string texto, int indice, string prefixo) =>
        indice >= prefixo.Length
        && texto.Substring(indice - prefixo.Length, prefixo.Length) == prefixo;

    private static string ApagarComentarios(string texto)
    {
        texto = Regex.Replace(texto, @"/\*.*?\*/", "", RegexOptions.Singleline);
        return Regex.Replace(texto, @"//.*?$", "", RegexOptions.Multiline);
    }

    private static string Raiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OrcaFacil", "Platforms", "Android", "AndroidManifest.xml")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar Platforms/Android a partir de " + AppContext.BaseDirectory);
    }
}
