using System.Globalization;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Tema;

namespace OrcaFacil.Tests.Tema;

/// <summary>
/// <c>ArmazenamentoService.LimparAsync</c> com tudo chama <c>ConfiguracaoService.Redefinir</c>,
/// que usa <see cref="LimpezaDePreferencias"/> no mesmo adaptador de <c>PreferenciasMaui</c>.
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

        LimpezaDePreferencias.Aplicar(TipoLimpezaDados.Tudo, preferencias);

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

        LimpezaDePreferencias.Aplicar(TipoLimpezaDados.Tudo, preferencias);

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

        LimpezaDePreferencias.Aplicar(TipoLimpezaDados.Tudo, preferencias);

        Assert.False(adaptador.Contem(PreferenciasTema.Chave));
        Assert.Equal(TemaPreferido.Automatico, preferencias.Tema);
        Assert.False(adaptador.Contem("Config.GraficoLancamentosExpandido"));
    }

    [Fact]
    public void LimparAsync_Parcial_NaoMexeNasPreferencias()
    {
        var repositorio = new RepositorioPreferenciasMemoria();
        var adaptador = new AdaptadorPreferencias(repositorio);
        var preferencias = new PreferenciasTema(adaptador);
        preferencias.Tema = TemaPreferido.Escuro;
        repositorio.GravarTexto("Config.SomAtivo", "false");

        LimpezaDePreferencias.Aplicar(TipoLimpezaDados.Lancamentos, preferencias);

        Assert.Equal("Escuro", repositorio.LerTexto(PreferenciasTema.Chave));
        Assert.Equal(TemaPreferido.Escuro, preferencias.Tema);
        Assert.Equal("false", repositorio.LerTexto("Config.SomAtivo"));
    }
}
