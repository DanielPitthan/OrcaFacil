using System.Text;

namespace OrcaFacil.Tests.Categorias;

/// <summary>
/// O voltar do Android cancela a paleta e não navega. Comentário no fonte não conta.
/// </summary>
public class VoltarPaletaFonteTests
{
    [Fact]
    public void DialogoCancelaNoVoltar_EComentarioNaoConta()
    {
        var fonte = Ler("OrcaFacil", "Components", "Shared", "PaletaCategoriaDialog.razor");
        var dialogo = SemComentarios(fonte);
        var corpo = Metodo(dialogo, "FecharPeloVoltar");

        Assert.Contains("VoltarPaleta.Abrir(_fecharPeloVoltar)", dialogo, StringComparison.Ordinal);
        Assert.Contains("VoltarPaleta.Fechar(_fecharPeloVoltar)", dialogo, StringComparison.Ordinal);
        Assert.Contains("InvokeAsync(Cancelar)", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("Close", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("NavigateTo", dialogo, StringComparison.Ordinal);
        Assert.DoesNotContain("GoBack", dialogo, StringComparison.Ordinal);
        Assert.Contains("private void Cancelar() => MudDialog.Cancel();", dialogo, StringComparison.Ordinal);

        var mutado = SemComentarios(fonte.Replace(
            "VoltarPaleta.Abrir(_fecharPeloVoltar);",
            "/* VoltarPaleta.Abrir(_fecharPeloVoltar); */",
            StringComparison.Ordinal));

        Assert.DoesNotContain("VoltarPaleta.Abrir(", mutado, StringComparison.Ordinal);
        Assert.Contains("private void Cancelar() => MudDialog.Cancel();", mutado, StringComparison.Ordinal);
    }

    [Fact]
    public void AndroidConsomeOVoltarSemNavegar_EUsaGlobal_EComentarioNaoConta()
    {
        var fonte = Ler(
            "OrcaFacil",
            "Platforms",
            "Android",
            "VoltarPaletaAndroid.cs");
        var android = SemComentarios(fonte);
        var aperto = Metodo(android, "HandleOnBackPressed");
        var consumo = aperto.IndexOf("TentarConsumir()", StringComparison.Ordinal);
        var segue = aperto.IndexOf("_dispatcher.OnBackPressed()", StringComparison.Ordinal);

        Assert.Contains("namespace OrcaFacil.Platforms.Android;", android, StringComparison.Ordinal);
        Assert.Contains("global::Android.OS.Looper", android, StringComparison.Ordinal);
        Assert.Contains("global::Android.App.Activity", android, StringComparison.Ordinal);
        Assert.Contains("OnBackPressedDispatcher.AddCallback(activity, _callback)", android, StringComparison.Ordinal);
        Assert.True(consumo >= 0 && consumo < segue);
        Assert.Contains("return;", aperto[consumo..segue], StringComparison.Ordinal);
        Assert.DoesNotContain("GoBack", android, StringComparison.Ordinal);
        Assert.DoesNotContain("NavigateTo", android, StringComparison.Ordinal);
        Assert.DoesNotContain("history.back", android, StringComparison.Ordinal);

        var mutado = SemComentarios(fonte.Replace(
            "if (_sessao.TentarConsumir())",
            "if (false /* _sessao.TentarConsumir() */)",
            StringComparison.Ordinal));
        var apertoMutado = Metodo(mutado, "HandleOnBackPressed");

        Assert.DoesNotContain("TentarConsumir()", apertoMutado, StringComparison.Ordinal);
        Assert.Contains("_dispatcher.OnBackPressed()", apertoMutado, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistroAndroidFicaNoAndroid_EComentarioNaoConta()
    {
        var fonte = Ler("OrcaFacil", "MauiProgram.cs");
        var programa = SemComentarios(fonte);

        Assert.Contains("AddSingleton<IVoltarPaleta, VoltarPaletaAndroid>()", programa, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<IVoltarPaleta, VoltarPaletaInerte>()", programa, StringComparison.Ordinal);

        var mutado = SemComentarios(fonte.Replace(
            "AddSingleton<IVoltarPaleta, VoltarPaletaAndroid>()",
            "/* AddSingleton<IVoltarPaleta, VoltarPaletaAndroid>() */",
            StringComparison.Ordinal));

        Assert.DoesNotContain("VoltarPaletaAndroid", mutado, StringComparison.Ordinal);
        Assert.Contains("VoltarPaletaInerte", mutado, StringComparison.Ordinal);
    }

    private static string Metodo(string fonte, string nome)
    {
        var inicio = fonte.IndexOf("void " + nome, StringComparison.Ordinal);
        if (inicio < 0)
            throw new InvalidOperationException("Método não encontrado: " + nome);

        var chave = fonte.IndexOf('{', inicio);
        if (chave < 0)
            throw new InvalidOperationException("Método sem corpo: " + nome);

        return fonte[inicio..(FecharBloco(fonte, chave) + 1)];
    }

    private static int FecharBloco(string fonte, int abre)
    {
        var profundidade = 0;
        for (var i = abre; i < fonte.Length; i++)
        {
            if (fonte[i] == '{')
                profundidade++;
            else if (fonte[i] == '}')
            {
                profundidade--;
                if (profundidade == 0)
                    return i;
            }
        }

        throw new InvalidOperationException("Bloco sem fechamento.");
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
            var caminho = Path.Combine(new[] { dir.FullName }.Concat(partes).ToArray());
            if (File.Exists(caminho))
                return File.ReadAllText(caminho);

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Não foi possível localizar " + string.Join('/', partes) + " a partir de " + AppContext.BaseDirectory);
    }
}
