using System.Text;
using System.Text.RegularExpressions;

namespace OrcaFacil.Tests.Data;

public class LogTipoDaFalhaFonteTests
{
    [Fact]
    public void MauiProgram_LogaSoOTipoDaExcecao()
    {
        var fonte = Ler("OrcaFacil", "MauiProgram.cs");

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
        var fonte = Ler("OrcaFacil", "Services", "InicializacaoApp.cs");

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
        var fonte = Ler("OrcaFacil", "Data", "DbInitializer.cs");

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
    [InlineData("Log.Error(e, \"m\");")]
    [InlineData("Log.Warning(e);")]
    [InlineData("Log.Warning(ex.Message);")]
    [InlineData("Log.Error(ex.StackTrace);")]
    [InlineData("Log.Error(ex.ToString());")]
    public void QualquerNivel_ComExcecao_EhRejeitado(string fonte)
    {
        Assert.Matches(LogComExcecao, Compactar(fonte));
    }

    [Fact]
    public void LinhaBoaComentada_NaoCobreLogComE()
    {
        const string fonte = """
            // Log.Error("{Mensagem} Tipo: {Tipo}", mensagem, VarreduraPlanilhaExportada.TipoDaFalha(ex));
            Log.Error(e, "falha");
            """;

        var limpo = SemComentarios(fonte);
        Assert.DoesNotContain(
            "Log.Error(\"{Mensagem} Tipo: {Tipo}\", mensagem, VarreduraPlanilhaExportada.TipoDaFalha(ex));",
            limpo,
            StringComparison.Ordinal);
        Assert.Matches(LogComExcecao, Compactar(limpo));
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
        @"Log\.[A-Za-z]+\((?:excecao|ex|e)(?:\.Message|\.StackTrace|\.ToString\(\))?(?:,|\))",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static string Ler(params string[] partes)
        => SemComentarios(File.ReadAllText(Path.Combine([EncontrarRaiz(), .. partes])));

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
