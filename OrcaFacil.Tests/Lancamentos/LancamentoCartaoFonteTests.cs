namespace OrcaFacil.Tests.Lancamentos;

public class LancamentoCartaoFonteTests
{
    [Fact]
    public void Seletores_AbremPeloContextoDoMenu_ENaoZeramARecorrencia()
    {
        var painel = File.ReadAllText(Path.Combine(
            EncontrarRaiz(), "OrcaFacil", "Components", "Pages", "Lancamentos", "FaturaCartaoPainel.razor"));

        var cartao = Antes(painel, "AriaLabel=\"Selecionar cartão\"", 500);
        var recorrencia = Antes(painel, "AriaLabel=\"Selecionar recorrência\"", 500);
        var escolherCartao = ExtrairBloco(painel, "Task SelecionarCartaoAsync(");
        var escolherRecorrencia = ExtrairBloco(painel, "void EscolherRecorrencia(");

        Assert.Contains("ToggleAsync", cartao, StringComparison.Ordinal);
        Assert.Contains("ToggleAsync", recorrencia, StringComparison.Ordinal);
        Assert.Contains("FluxoLancamento.EscolherCartao(", escolherCartao, StringComparison.Ordinal);
        Assert.DoesNotContain("Recorrencia", escolherCartao, StringComparison.Ordinal);
        Assert.Contains("FluxoLancamento.EscolherRecorrencia(", escolherRecorrencia, StringComparison.Ordinal);
        Assert.DoesNotContain("CartaoId", escolherRecorrencia, StringComparison.Ordinal);
    }

    [Fact]
    public void NovoLancamento_ZeraNoOnParametersSet()
    {
        var formulario = File.ReadAllText(Path.Combine(
            EncontrarRaiz(), "OrcaFacil", "Components", "Pages", "Lancamentos", "LancamentoForm.razor"));
        var parameters = ExtrairBloco(formulario, "Task OnParametersSetAsync()");
        var sessao = ExtrairBloco(formulario, "Task AbrirSessaoAsync()");

        Assert.Contains("AbrirSessaoAsync", parameters, StringComparison.Ordinal);
        Assert.Contains("FluxoLancamento.IniciarNovo(", sessao, StringComparison.Ordinal);
        Assert.True(sessao.IndexOf("IniciarNovo", StringComparison.Ordinal) > sessao.IndexOf("Consumir", StringComparison.Ordinal));
    }

    private static string Antes(string fonte, string marca, int tamanho)
    {
        var indice = fonte.IndexOf(marca, StringComparison.Ordinal);
        Assert.True(indice >= 0, marca);
        var inicio = Math.Max(0, indice - tamanho);
        return fonte[inicio..indice];
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
