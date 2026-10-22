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
        AssertSemExcecaoNoLogError(fonte);
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
        Assert.Equal(2, Contar(fonte, "VarreduraPlanilhaExportada.TipoDaFalha("));
        AssertSemExcecaoNoLogError(fonte);
    }

    [Fact]
    public void DbInitializer_LogaSoOTipoDaExcecao()
    {
        var fonte = File.ReadAllText(Path.Combine(EncontrarRaiz(), "OrcaFacil", "Data", "DbInitializer.cs"));

        Assert.Contains("VarreduraPlanilhaExportada.TipoDaFalha(excecao)", fonte, StringComparison.Ordinal);
        Assert.Contains("Log.Error(mensagem + \" Tipo: {Tipo}\", valores)", fonte, StringComparison.Ordinal);
        Assert.Equal(1, Contar(fonte, "VarreduraPlanilhaExportada.TipoDaFalha("));
        AssertSemExcecaoNoLogError(fonte);
    }

    private static void AssertSemExcecaoNoLogError(string fonte)
    {
        var compacto = Regex.Replace(fonte, @"\s+", "");
        Assert.DoesNotContain("Log.Error(ex,", compacto, StringComparison.Ordinal);
        Assert.DoesNotContain("Log.Error(ex)", compacto, StringComparison.Ordinal);
        Assert.DoesNotContain("Log.Error(excecao", compacto, StringComparison.Ordinal);
        Assert.Contains("VarreduraPlanilhaExportada.TipoDaFalha(", compacto, StringComparison.Ordinal);
    }

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
