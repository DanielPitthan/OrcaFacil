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
    public void Leitura_UsaMaxTokensDaConfiguracao_ENaoChamaSemMediaType()
    {
        var raiz = EncontrarRaiz();
        var pagina = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Pages", "Cesta", "CestaPage.razor"));
        var enviar = ExtrairBloco(pagina, "Task EnviarJpegAsync(");
        var recusa = enviar.IndexOf("MensagensLeituraComprovante.FormatoNaoConvertido()", StringComparison.Ordinal);
        var chamada = enviar.IndexOf("Cliente.LerComprovanteAsync(", StringComparison.Ordinal);

        Assert.Contains("Configuracao.UrlInteligencia", enviar, StringComparison.Ordinal);
        Assert.Contains("Configuracao.ModeloInteligencia", enviar, StringComparison.Ordinal);
        Assert.Contains("Configuracao.MaxTokensInteligencia", enviar, StringComparison.Ordinal);
        Assert.Contains("PreparoFotoComprovante.EscolherMediaType(jpeg)", enviar, StringComparison.Ordinal);
        Assert.True(recusa >= 0);
        Assert.True(chamada > recusa);
        Assert.DoesNotContain("dashscope", pagina, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("deepseek", pagina, StringComparison.OrdinalIgnoreCase);

        var painel = File.ReadAllText(Path.Combine(
            raiz, "OrcaFacil", "Components", "Pages", "Configuracoes", "InteligenciaArtificialPanel.razor"));
        Assert.Contains("Configuracao.MaxTokensInteligencia", painel, StringComparison.Ordinal);
        Assert.Contains("PedidoMensagem.TextoMaxTokensForaDaFaixa", painel, StringComparison.Ordinal);
        Assert.Contains("PedidoMensagem.MaxTokensNaFaixa(_maxTokens)", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("dashscope", painel, StringComparison.OrdinalIgnoreCase);

        var configuracao = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Services", "ConfiguracaoService.cs"));
        Assert.Contains("ChipsInteligencia.ChaveMaxTokens", configuracao, StringComparison.Ordinal);
        Assert.Contains("PedidoMensagem.NormalizarMaxTokens", configuracao, StringComparison.Ordinal);
        Assert.Contains("PedidoMensagem.MaxTokensNaFaixa(value)", configuracao, StringComparison.Ordinal);

        var capturar = ExtrairBloco(pagina, "Task CapturarEEnviarAsync(");
        var acima = capturar.IndexOf("MensagensLeituraComprovante.FotoAcimaDoTeto()", StringComparison.Ordinal);
        var envio = capturar.IndexOf("EnviarJpegAsync(", StringComparison.Ordinal);
        Assert.True(acima >= 0 && acima < envio);
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
