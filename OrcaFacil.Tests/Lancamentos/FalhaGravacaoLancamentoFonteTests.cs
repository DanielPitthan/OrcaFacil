using System.Text.RegularExpressions;

namespace OrcaFacil.Tests.Lancamentos;

public class FalhaGravacaoLancamentoFonteTests
{
    [Fact]
    public void CategoriaService_DbUpdateException_ViraMensagemELogSoComOTipo()
    {
        var fonte = File.ReadAllText(Path.Combine(Raiz(), "OrcaFacil", "Services", "CategoriaService.cs"));
        var bloco = BlocoCatch(fonte, "catch (DbUpdateException ex)");

        Assert.Contains("FalhaDeGravacao.ParaOUsuario(ex, FalhaDeGravacao.MensagemCategoria)", bloco, StringComparison.Ordinal);
        Assert.Contains("VarreduraPlanilhaExportada.TipoDaFalha(ex)", bloco, StringComparison.Ordinal);
        Assert.Contains("_cadastro.CriarAsync", fonte, StringComparison.Ordinal);
        AssertSemDetalhe(bloco);
    }

    [Fact]
    public void TransacaoService_DbUpdateException_ViraMensagemELogSoComOTipo()
    {
        var fonte = File.ReadAllText(Path.Combine(Raiz(), "OrcaFacil", "Services", "TransacaoService.cs"));
        var bloco = BlocoCatch(fonte, "catch (DbUpdateException ex)");

        Assert.Contains("FalhaDeGravacao.ParaOUsuario(ex, FalhaDeGravacao.MensagemLancamento)", bloco, StringComparison.Ordinal);
        Assert.Contains("VarreduraPlanilhaExportada.TipoDaFalha(ex)", bloco, StringComparison.Ordinal);
        Assert.Contains("GravarAsync(() => _transacaoRepository.AddAsync(transacao))", fonte, StringComparison.Ordinal);

        var atualizar = BlocoMetodo(fonte, "public async Task AtualizarAsync(int id, TransacaoFormModel dto)");
        var gravar = atualizar.IndexOf("GravarAsync", StringComparison.Ordinal);
        var update = atualizar.IndexOf("_transacaoRepository.UpdateAsync(transacao)", StringComparison.Ordinal);
        Assert.True(gravar >= 0 && update > gravar);
        AssertSemDetalhe(bloco);
    }

    [Fact]
    public void SalvarAsync_DbUpdateException_ChegaAoUsuarioSemDetalhe()
    {
        var fonte = File.ReadAllText(Path.Combine(
            Raiz(), "OrcaFacil", "Components", "Pages", "Lancamentos", "LancamentoForm.razor"));
        var metodo = BlocoMetodo(fonte, "private async Task SalvarAsync()");
        var bloco = BlocoCatch(metodo, "catch (DbUpdateException ex)");

        Assert.Contains("Snackbar.Add(FalhaDeGravacao.MensagemLancamento, Severity.Error)", bloco, StringComparison.Ordinal);
        Assert.Contains("VarreduraPlanilhaExportada.TipoDaFalha(ex)", bloco, StringComparison.Ordinal);
        AssertSemDetalhe(bloco);
    }

    private static void AssertSemDetalhe(string bloco)
    {
        Assert.DoesNotContain("ex.Message", bloco, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", bloco, StringComparison.Ordinal);
        Assert.DoesNotContain("ToString()", bloco, StringComparison.Ordinal);
        Assert.DoesNotContain("InnerException", bloco, StringComparison.Ordinal);
        Assert.DoesNotMatch(LogComExcecao, Regex.Replace(bloco, @"\s+", ""));
    }

    private static readonly Regex LogComExcecao = new(
        @"Log\.[A-Za-z]+\((?:excecao|ex|e)(?:\.Message|\.StackTrace|\.ToString\(\))?(?:,|\))",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static string BlocoCatch(string fonte, string assinatura)
    {
        var indice = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(indice >= 0, assinatura);
        return ExtrairChaves(fonte, fonte.IndexOf('{', indice));
    }

    private static string BlocoMetodo(string fonte, string assinatura)
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
}
