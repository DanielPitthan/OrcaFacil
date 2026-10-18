using System.Globalization;
using OrcaFacil.Core.Armazenamento.Espelho.Pasta;
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
    public async Task LimparAsync_Tudo_PreservaTemaGravadoComoTexto(TemaPreferido tema, string texto)
    {
        var repositorio = new RepositorioPreferenciasMemoria();
        var adaptador = new AdaptadorPreferencias(repositorio);
        var preferencias = new PreferenciasTema(adaptador, CofreChaveVazio.Instancia);
        preferencias.Tema = tema;
        repositorio.GravarTexto("Config.NotificacoesAtivas", "false");
        repositorio.GravarTexto("Config.SomAtivo", "false");

        await Redefinir(preferencias);

        Assert.Equal(texto, repositorio.LerTexto(PreferenciasTema.Chave));
        Assert.Equal(tema, preferencias.Tema);
        Assert.False(adaptador.Contem("Config.NotificacoesAtivas"));
        Assert.False(adaptador.Contem("Config.SomAtivo"));
    }

    [Theory]
    [InlineData(TemaPreferido.Automatico)]
    [InlineData(TemaPreferido.Claro)]
    [InlineData(TemaPreferido.Escuro)]
    public async Task LimparAsync_Tudo_PreservaTemaGravadoComoInteiro(TemaPreferido tema)
    {
        var repositorio = new RepositorioPreferenciasMemoria();
        repositorio.GravarInteiro(PreferenciasTema.Chave, (int)tema);
        repositorio.GravarTexto("Config.PrazosAviso", "4");
        var adaptador = new AdaptadorPreferencias(repositorio);
        var preferencias = new PreferenciasTema(adaptador, CofreChaveVazio.Instancia);

        await Redefinir(preferencias);

        Assert.Equal(
            ((int)tema).ToString(CultureInfo.InvariantCulture),
            repositorio.LerTexto(PreferenciasTema.Chave));
        Assert.Equal(tema, preferencias.Tema);
        Assert.False(adaptador.Contem("Config.PrazosAviso"));
    }

    [Fact]
    public async Task LimparAsync_Tudo_PreservaAUriDaPastaDoEspelho()
    {
        const string uri = "content://com.android.externalstorage.documents/tree/primary%3AOrca";
        var repositorio = new RepositorioPreferenciasMemoria();
        var adaptador = new AdaptadorPreferencias(repositorio);
        var preferencias = new PreferenciasTema(adaptador, CofreChaveVazio.Instancia);
        preferencias.Tema = TemaPreferido.Escuro;
        repositorio.GravarTexto(UriEscolhaPasta.ChavePreferencia, uri);
        repositorio.GravarTexto("Config.NotificacoesAtivas", "false");

        await Redefinir(preferencias);

        Assert.Equal("Escuro", repositorio.LerTexto(PreferenciasTema.Chave));
        Assert.Equal(uri, repositorio.LerTexto(UriEscolhaPasta.ChavePreferencia));
        Assert.Equal(uri, new MemoriaPastaEspelho(adaptador).LerUri());
        Assert.False(adaptador.Contem("Config.NotificacoesAtivas"));
    }

    [Fact]
    public async Task LimparAsync_Tudo_SemChave_NaoCriaTema()
    {
        var repositorio = new RepositorioPreferenciasMemoria();
        repositorio.GravarTexto("Config.GraficoLancamentosExpandido", "false");
        var adaptador = new AdaptadorPreferencias(repositorio);
        var preferencias = new PreferenciasTema(adaptador, CofreChaveVazio.Instancia);

        await Redefinir(preferencias);

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

        Assert.Contains("public Task<bool> Redefinir() => _tema.Redefinir();", configuracao, StringComparison.Ordinal);
        Assert.DoesNotContain("Preferences.Default.Clear", configuracao, StringComparison.Ordinal);
        Assert.DoesNotContain("LimpezaDePreferencias", configuracao, StringComparison.Ordinal);
        Assert.Contains("PreferenciasTema tema", configuracao, StringComparison.Ordinal);
        Assert.Contains("_tema = tema;", configuracao, StringComparison.Ordinal);

        Assert.Contains("var chaveApagada = await _configuracao.Redefinir();", armazenamento, StringComparison.Ordinal);
        Assert.Contains("ResultadoLimpeza.Total(chaveApagada)", armazenamento, StringComparison.Ordinal);
        Assert.Contains("resultado.TextoRegistro", armazenamento, StringComparison.Ordinal);
        Assert.DoesNotContain("Aplicação zerada (todos os dados removidos)", armazenamento, StringComparison.Ordinal);
        Assert.DoesNotContain("Preferences.Default.Clear", armazenamento, StringComparison.Ordinal);

        Assert.Contains("class PreferenciasTema : IRedefinicaoPreferencias", preferencias, StringComparison.Ordinal);
        Assert.Contains("Task<bool> Redefinir();", preferencias, StringComparison.Ordinal);
        Assert.Contains("ICofreChaveApi cofre", preferencias, StringComparison.Ordinal);
        Assert.DoesNotContain("ICofreChaveApi? cofre = null", preferencias, StringComparison.Ordinal);

        var maui = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "MauiProgram.cs"));
        var registroCofre = maui.IndexOf("AddSingleton<ICofreChaveApi, CofreChaveApiSecureStorage>()", StringComparison.Ordinal);
        var registroTema = maui.IndexOf("AddSingleton<PreferenciasTema>()", StringComparison.Ordinal);
        Assert.True(registroCofre >= 0 && registroTema > registroCofre);

        var painel = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Pages", "Configuracoes", "ArmazenamentoPanel.razor"));
        Assert.Contains("resultado.Mensagem", painel, StringComparison.Ordinal);
        Assert.Contains("resultado.ChavePermaneceu", painel, StringComparison.Ordinal);
        Assert.Contains("of-snack-chave", painel, StringComparison.Ordinal);
        Assert.Contains("_limpando = false", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Aplicação zerada\"", painel, StringComparison.Ordinal);
    }

    /// <summary>
    /// O mesmo contrato de <c>ConfiguracaoService.Redefinir</c>.
    /// Se <see cref="PreferenciasTema"/> deixar de implementar a interface, este teste não compila.
    /// </summary>
    private static Task Redefinir(PreferenciasTema preferencias)
    {
        IRedefinicaoPreferencias redefinicao = preferencias;
        return redefinicao.Redefinir();
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
