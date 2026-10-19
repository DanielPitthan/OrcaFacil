namespace OrcaFacil.Tests.Llm;

public class CestaLeituraFonteTests
{
    [Theory]
    [InlineData("Task IniciarLeituraAsync()")]
    [InlineData("Task TentarDeNovoAsync()")]
    public void GuardaDeToqueDuplo_VemAntesDaTrava_EAntesDoPrimeiroAwait(string assinatura)
    {
        var pagina = File.ReadAllText(Path.Combine(EncontrarRaiz(), "OrcaFacil", "Components", "Pages", "Cesta", "CestaPage.razor"));
        var metodo = ExtrairBloco(pagina, assinatura);
        var guardaMatch = System.Text.RegularExpressions.Regex.Match(metodo, @"_lendo\s*\|\|\s*_descartada");
        var guarda = guardaMatch.Success ? guardaMatch.Index : -1;
        var retorno = guarda < 0
            ? -1
            : metodo.IndexOf("return", guarda + guardaMatch.Length, StringComparison.Ordinal);
        var trava = metodo.IndexOf("_lendo = true;", StringComparison.Ordinal);
        var primeiroAwait = metodo.IndexOf("await", StringComparison.Ordinal);

        Assert.True(guarda >= 0, assinatura);
        Assert.True(retorno > guarda, assinatura);
        Assert.True(trava > retorno, assinatura);
        Assert.True(primeiroAwait > trava, assinatura);
    }

    [Fact]
    public void AgoraNao_RetornaAntesDeMarcarVisto()
    {
        var raiz = EncontrarRaiz();
        var dialog = File.ReadAllText(Path.Combine(
            raiz, "OrcaFacil", "Components", "Pages", "Cesta", "AvisoPrivacidadeComprovanteDialog.razor"));
        var agoraNao = ExtrairMembro(dialog, "void AgoraNao()");

        Assert.Contains("Cancel()", agoraNao, StringComparison.Ordinal);
        Assert.DoesNotContain("MarcarVisto", agoraNao, StringComparison.Ordinal);

        var pagina = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Pages", "Cesta", "CestaPage.razor"));
        var confirmar = ExtrairBloco(pagina, "Task<bool> ConfirmarAvisoAsync()");
        var retorno = confirmar.IndexOf("return false", StringComparison.Ordinal);
        var marcar = confirmar.IndexOf("MarcarVisto", StringComparison.Ordinal);

        Assert.True(retorno >= 0);
        Assert.True(marcar > retorno);
    }

    private static string ExtrairMembro(string fonte, string assinatura)
    {
        var indice = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(indice >= 0, assinatura);
        var i = indice + assinatura.Length;
        while (i < fonte.Length && char.IsWhiteSpace(fonte[i]))
            i++;

        if (i < fonte.Length && fonte[i] == '{')
            return ExtrairChaves(fonte, i);

        var fim = fonte.IndexOf(';', i);
        Assert.True(fim > i, assinatura);
        return fonte[indice..(fim + 1)];
    }

    private static string ExtrairBloco(string fonte, string assinatura)
    {
        var indice = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(indice >= 0, assinatura);
        return ExtrairChaves(fonte, fonte.IndexOf('{', indice));
    }

    private static string ExtrairChaves(string fonte, int abre)
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
                    return fonte[abre..(i + 1)];
            }
        }

        throw new InvalidOperationException("Bloco sem fechamento.");
    }

    private static string EncontrarRaiz()
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
}
