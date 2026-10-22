using System.Text.RegularExpressions;

namespace OrcaFacil.Tests.Data;

public class LogTipoDaFalhaFonteTests
{
    [Fact]
    public void MauiProgram_LogaSoOTipoDaExcecao()
    {
        var fonte = File.ReadAllText(Path.Combine(EncontrarRaiz(), "OrcaFacil", "MauiProgram.cs"));

        Assert.Contains(
            "Log.Error(\"{Mensagem} Tipo: {Tipo}\", mensagem, VarreduraPlanilhaExportada.TipoDaFalha(ex));",
            fonte,
            StringComparison.Ordinal);
        Assert.Equal(1, Contar(fonte, "VarreduraPlanilhaExportada.TipoDaFalha("));
        AssertNaoPassaExcecaoAoLog(fonte);
    }

    [Fact]
    public void InicializacaoApp_LogaSoOTipoDaExcecao()
    {
        var fonte = File.ReadAllText(Path.Combine(EncontrarRaiz(), "OrcaFacil", "Services", "InicializacaoApp.cs"));

        Assert.Contains(
            "\"Falha no passo {Passo}. Log em {PastaLogs}. O banco {Banco} não foi apagado. Tipo: {Tipo}\"",
            fonte,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"Falha no passo lembretes. O aplicativo segue aberto. Banco {Banco} não foi apagado. Tipo: {Tipo}\"",
            fonte,
            StringComparison.Ordinal);
        Assert.Contains(
            "Log.Warning(\n                    \"Falha ao avisar a tela sobre a inicialização. Tipo: {Tipo}\",\n                    VarreduraPlanilhaExportada.TipoDaFalha(ex));",
            fonte,
            StringComparison.Ordinal);
        Assert.Equal(3, Contar(fonte, "VarreduraPlanilhaExportada.TipoDaFalha("));
        AssertNaoPassaExcecaoAoLog(fonte);
    }

    [Fact]
    public void DbInitializer_LogaSoOTipoDaExcecao()
    {
        var fonte = File.ReadAllText(Path.Combine(EncontrarRaiz(), "OrcaFacil", "Data", "DbInitializer.cs"));

        Assert.Contains("VarreduraPlanilhaExportada.TipoDaFalha(excecao)", fonte, StringComparison.Ordinal);
        Assert.Contains("Log.Error(mensagem + \" Tipo: {Tipo}\", (object[])valores)", fonte, StringComparison.Ordinal);
        Assert.Equal(1, Contar(fonte, "VarreduraPlanilhaExportada.TipoDaFalha("));
        AssertNaoPassaExcecaoAoLog(fonte);
    }

    [Theory]
    [InlineData("Log.Warning(ex, \"m\");")]
    [InlineData("Log.Information(excecao, mensagem);")]
    [InlineData("Log.Debug(ex);")]
    [InlineData("Log.Fatal( ex , \"m\" );")]
    [InlineData("Log.Verbose(\r\nexcecao, \"m\");")]
    [InlineData("Log.Error(\n    excecao,\n    mensagem);")]
    public void QualquerNivel_ComExcecao_EhRejeitado(string fonte)
    {
        Assert.Matches(LogComExcecao, Compactar(fonte));
    }

    [Fact]
    public void TipoDaFalha_NaoContaComoPassarAExcecao()
    {
        const string fonte = """
            Log.Warning(
                "Falha ao avisar a tela sobre a inicialização. Tipo: {Tipo}",
                VarreduraPlanilhaExportada.TipoDaFalha(ex));
            """;

        Assert.DoesNotMatch(LogComExcecao, Compactar(fonte));
    }

    private static void AssertNaoPassaExcecaoAoLog(string fonte)
    {
        var compacto = Compactar(fonte);
        Assert.DoesNotMatch(LogComExcecao, compacto);
        Assert.Contains("VarreduraPlanilhaExportada.TipoDaFalha(", compacto, StringComparison.Ordinal);
    }

    private static string Compactar(string fonte) => Regex.Replace(fonte, @"\s+", "");

    private static readonly Regex LogComExcecao = new(
        @"Log\.[A-Za-z]+\((ex|excecao)(,|\))",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static int Contar(string texto, string trecho)
    {
        var total = 0;
        var indice = 0;
        while (true)
        {
            indice = texto.IndexOf(trecho, indice, StringComparison.Ordinal);
            if (indice < 0)
                return total;

            total++;
            indice += trecho.Length;
        }
    }

    private static string EncontrarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OrcaFacil", "MauiProgram.cs")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar MauiProgram.cs.");
    }
}
