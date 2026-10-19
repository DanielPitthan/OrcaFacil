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

    [Fact]
    public void Cadastrar_TravaAntesDoPrimeiroAwait()
    {
        var dialog = File.ReadAllText(Path.Combine(
            EncontrarRaiz(), "OrcaFacil", "Components", "Pages", "Cesta", "PreviaComprovanteDialog.razor"));
        var metodo = ExtrairBloco(dialog, "Task CadastrarAsync()");
        var guardaMatch = System.Text.RegularExpressions.Regex.Match(metodo, @"_cadastrando\s*\|\|");
        var guarda = guardaMatch.Success ? guardaMatch.Index : -1;
        var retorno = guarda < 0
            ? -1
            : metodo.IndexOf("return", guarda + guardaMatch.Length, StringComparison.Ordinal);
        var trava = metodo.IndexOf("_cadastrando = true;", StringComparison.Ordinal);
        var primeiroAwait = metodo.IndexOf("await", StringComparison.Ordinal);

        Assert.True(guarda >= 0);
        Assert.True(retorno > guarda);
        Assert.True(trava > retorno);
        Assert.True(primeiroAwait > trava);
    }

    [Fact]
    public void ConfirmarPrevia_TravaEFechaSemAwait()
    {
        var dialog = File.ReadAllText(Path.Combine(
            EncontrarRaiz(), "OrcaFacil", "Components", "Pages", "Cesta", "PreviaComprovanteDialog.razor"));
        var metodo = ExtrairBloco(dialog, "void Confirmar()");
        var guardaMatch = System.Text.RegularExpressions.Regex.Match(metodo, @"_confirmando");
        var guarda = guardaMatch.Success ? guardaMatch.Index : -1;
        var retorno = guarda < 0
            ? -1
            : metodo.IndexOf("return", guarda + guardaMatch.Length, StringComparison.Ordinal);
        var trava = metodo.IndexOf("_confirmando = true;", StringComparison.Ordinal);
        var fechar = metodo.IndexOf("MudDialog.Close", StringComparison.Ordinal);

        Assert.True(guarda >= 0);
        Assert.True(retorno > guarda);
        Assert.True(trava > retorno);
        Assert.True(fechar > trava);
        Assert.DoesNotContain("await", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviaAberta_TravaAntesDoPrimeiroAwait()
    {
        var pagina = File.ReadAllText(Path.Combine(
            EncontrarRaiz(), "OrcaFacil", "Components", "Pages", "Cesta", "CestaPage.razor"));
        var metodo = ExtrairBloco(pagina, "Task AbrirPreviaAsync()");
        var guardaMatch = System.Text.RegularExpressions.Regex.Match(metodo, @"_previaAberta");
        var guarda = guardaMatch.Success ? guardaMatch.Index : -1;
        var retorno = guarda < 0
            ? -1
            : metodo.IndexOf("return", guarda + guardaMatch.Length, StringComparison.Ordinal);
        var trava = metodo.IndexOf("_previaAberta = true;", StringComparison.Ordinal);
        var primeiroAwait = metodo.IndexOf("await", StringComparison.Ordinal);

        Assert.True(guarda >= 0);
        Assert.True(retorno > guarda);
        Assert.True(trava > retorno);
        Assert.True(primeiroAwait > trava);
    }

    [Fact]
    public void Contraste_LocalCategoriaEErroDoSheet()
    {
        var raiz = EncontrarRaiz();
        var tema = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "wwwroot", "css", "orcafacil-theme.css"));
        var lista = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Shared", "ItemList.razor.css"));
        var sheet = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Shared", "ItemSheet.razor.css"));
        var sub = ExtrairRegra(tema, ".of-appbar-sub");
        var categoria = ExtrairRegra(lista, ".of-lista-sub");

        Assert.Contains("color: var(--of-text-secondary);", sub, StringComparison.Ordinal);
        Assert.Contains("color: var(--of-text-secondary);", categoria, StringComparison.Ordinal);
        Assert.Contains("color: #9B2C3A;", sheet, StringComparison.Ordinal);
        Assert.Contains(".of-lista-item-erro", lista, StringComparison.Ordinal);
        Assert.Contains("#9B2C3A", lista, StringComparison.Ordinal);
        Assert.Contains("#F59A9A", lista, StringComparison.Ordinal);
        Assert.True(Razao("#5A6B78", "#ffffff") >= 4.5);
        Assert.True(Razao("#5A6B78", "#EFF3F7") >= 4.5);
        Assert.True(Razao("#C9D1DD", "#1B1F27") >= 4.5);
        Assert.True(Razao("#C9D1DD", "#12151B") >= 4.5);
        Assert.True(Razao("#9B2C3A", "#ffffff") >= 4.5);
        Assert.True(Razao("#F59A9A", "#1B1F27") >= 4.5);
    }

    private static string ExtrairRegra(string css, string seletor)
    {
        var indice = css.IndexOf(seletor, StringComparison.Ordinal);
        Assert.True(indice >= 0, seletor);
        var abre = css.IndexOf('{', indice);
        var fecha = css.IndexOf('}', abre);
        return css[abre..(fecha + 1)];
    }

    private static double Razao(string frente, string fundo)
    {
        var clara = Math.Max(Luz(frente), Luz(fundo));
        var escura = Math.Min(Luz(frente), Luz(fundo));
        return (clara + 0.05) / (escura + 0.05);
    }

    private static double Luz(string hex)
    {
        hex = hex.TrimStart('#');
        double Canal(string trecho)
        {
            var valor = Convert.ToInt32(trecho, 16) / 255d;
            return valor <= 0.04045 ? valor / 12.92 : Math.Pow((valor + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Canal(hex[..2])) + (0.7152 * Canal(hex.Substring(2, 2))) + (0.0722 * Canal(hex.Substring(4, 2)));
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
