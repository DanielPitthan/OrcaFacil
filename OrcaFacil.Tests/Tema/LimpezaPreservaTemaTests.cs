using System.Globalization;
using OrcaFacil.Core.Tema;

namespace OrcaFacil.Tests.Tema;

/// <summary>
/// <c>ArmazenamentoService.LimparAsync</c> chama <c>ConfiguracaoService.Redefinir</c>,
/// que só delega para <see cref="IRedefinicaoPreferencias.Redefinir"/>.
/// </summary>
public class LimpezaPreservaTemaTests
{
    [Theory]
    [InlineData(TemaPreferido.Claro, "Claro")]
    [InlineData(TemaPreferido.Escuro, "Escuro")]
    [InlineData(TemaPreferido.Automatico, "Automatico")]
    public void LimparAsync_Tudo_PreservaTemaGravadoComoTexto(TemaPreferido tema, string texto)
    {
        var repositorio = new RepositorioPreferenciasMemoria();
        var adaptador = new AdaptadorPreferencias(repositorio);
        var preferencias = new PreferenciasTema(adaptador);
        preferencias.Tema = tema;
        repositorio.GravarTexto("Config.NotificacoesAtivas", "false");
        repositorio.GravarTexto("Config.SomAtivo", "false");

        Redefinir(preferencias);

        Assert.Equal(texto, repositorio.LerTexto(PreferenciasTema.Chave));
        Assert.Equal(tema, preferencias.Tema);
        Assert.False(adaptador.Contem("Config.NotificacoesAtivas"));
        Assert.False(adaptador.Contem("Config.SomAtivo"));
    }

    [Theory]
    [InlineData(TemaPreferido.Automatico)]
    [InlineData(TemaPreferido.Claro)]
    [InlineData(TemaPreferido.Escuro)]
    public void LimparAsync_Tudo_PreservaTemaGravadoComoInteiro(TemaPreferido tema)
    {
        var repositorio = new RepositorioPreferenciasMemoria();
        repositorio.GravarInteiro(PreferenciasTema.Chave, (int)tema);
        repositorio.GravarTexto("Config.PrazosAviso", "4");
        var adaptador = new AdaptadorPreferencias(repositorio);
        var preferencias = new PreferenciasTema(adaptador);

        Redefinir(preferencias);

        Assert.Equal(
            ((int)tema).ToString(CultureInfo.InvariantCulture),
            repositorio.LerTexto(PreferenciasTema.Chave));
        Assert.Equal(tema, preferencias.Tema);
        Assert.False(adaptador.Contem("Config.PrazosAviso"));
    }

    [Fact]
    public void LimparAsync_Tudo_SemChave_NaoCriaTema()
    {
        var repositorio = new RepositorioPreferenciasMemoria();
        repositorio.GravarTexto("Config.GraficoLancamentosExpandido", "false");
        var adaptador = new AdaptadorPreferencias(repositorio);
        var preferencias = new PreferenciasTema(adaptador);

        Redefinir(preferencias);

        Assert.False(adaptador.Contem(PreferenciasTema.Chave));
        Assert.Equal(TemaPreferido.Automatico, preferencias.Tema);
        Assert.False(adaptador.Contem("Config.GraficoLancamentosExpandido"));
    }

    [Fact]
    public void ConfiguracaoService_Redefinir_SoDelegaParaAInterface()
    {
        var raiz = EncontrarRaiz();
        var configuracao = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Services", "ConfiguracaoService.cs"));
        var armazenamento = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Services", "ArmazenamentoService.cs"));
        var preferencias = File.ReadAllText(Path.Combine(raiz, "OrcaFacil.Core", "Tema", "PreferenciasTema.cs"));

        Assert.Contains("public void Redefinir() => _tema.Redefinir();", configuracao, StringComparison.Ordinal);
        Assert.DoesNotContain("Preferences.Default.Clear", configuracao, StringComparison.Ordinal);
        Assert.DoesNotContain("LimpezaDePreferencias", configuracao, StringComparison.Ordinal);
        Assert.Contains("PreferenciasTema tema", configuracao, StringComparison.Ordinal);
        Assert.Contains("_tema = tema;", configuracao, StringComparison.Ordinal);

        Assert.Contains("_configuracao.Redefinir();", armazenamento, StringComparison.Ordinal);
        Assert.DoesNotContain("Preferences.Default.Clear", armazenamento, StringComparison.Ordinal);

        Assert.Contains("class PreferenciasTema : IRedefinicaoPreferencias", preferencias, StringComparison.Ordinal);
        Assert.Contains("void Redefinir();", preferencias, StringComparison.Ordinal);
    }

    /// <summary>
    /// O mesmo contrato de <c>ConfiguracaoService.Redefinir</c>.
    /// Se <see cref="PreferenciasTema"/> deixar de implementar a interface, este teste não compila.
    /// </summary>
    private static void Redefinir(PreferenciasTema preferencias)
    {
        IRedefinicaoPreferencias redefinicao = preferencias;
        redefinicao.Redefinir();
    }

    private static string EncontrarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OrcaFacil", "Services", "ConfiguracaoService.cs")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar ConfiguracaoService.cs a partir de " + AppContext.BaseDirectory);
    }
}
