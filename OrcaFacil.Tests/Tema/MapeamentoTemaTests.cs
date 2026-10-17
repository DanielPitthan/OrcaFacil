using OrcaFacil.Core.Tema;

namespace OrcaFacil.Tests.Tema;

public class MapeamentoTemaTests
{
    [Theory]
    [InlineData(TemaPreferido.Automatico, false, TemaUsuarioApp.Unspecified, false, "of-tema-claro")]
    [InlineData(TemaPreferido.Automatico, true, TemaUsuarioApp.Unspecified, true, "of-tema-escuro")]
    [InlineData(TemaPreferido.Claro, true, TemaUsuarioApp.Light, false, "of-tema-claro")]
    [InlineData(TemaPreferido.Claro, false, TemaUsuarioApp.Light, false, "of-tema-claro")]
    [InlineData(TemaPreferido.Escuro, false, TemaUsuarioApp.Dark, true, "of-tema-escuro")]
    [InlineData(TemaPreferido.Escuro, true, TemaUsuarioApp.Dark, true, "of-tema-escuro")]
    public void Resolver_MapeiaPreferenciaParaUserAppThemeEIsDarkMode(
        TemaPreferido preferencia,
        bool sistemaEscuro,
        TemaUsuarioApp userAppTheme,
        bool isDarkMode,
        string classeCss)
    {
        var resolvido = MapeamentoTema.Resolver(preferencia, sistemaEscuro);

        Assert.Equal(userAppTheme, resolvido.UserAppTheme);
        Assert.Equal(isDarkMode, resolvido.IsDarkMode);
        Assert.Equal(classeCss, resolvido.ClasseCss);
        Assert.Equal(userAppTheme, MapeamentoTema.ParaUserAppTheme(preferencia));
        Assert.Equal(isDarkMode, MapeamentoTema.IsDarkMode(preferencia, sistemaEscuro));
    }

    [Fact]
    public void DefaultDoEnum_EAutomatico_ENaoClaro()
    {
        Assert.Equal(TemaPreferido.Automatico, default(TemaPreferido));
        Assert.Equal(0, (int)TemaPreferido.Automatico);
        Assert.Equal(1, (int)TemaPreferido.Claro);
        Assert.Equal(2, (int)TemaPreferido.Escuro);
        Assert.NotEqual(default, TemaPreferido.Claro);
        Assert.NotEqual(default, TemaPreferido.Escuro);
    }

    [Theory]
    [InlineData("Claro", TemaPreferido.Claro)]
    [InlineData("Escuro", TemaPreferido.Escuro)]
    [InlineData("Automatico", TemaPreferido.Automatico)]
    [InlineData("1", TemaPreferido.Claro)]
    [InlineData("2", TemaPreferido.Escuro)]
    [InlineData("0", TemaPreferido.Automatico)]
    [InlineData(null, TemaPreferido.Automatico)]
    [InlineData("", TemaPreferido.Automatico)]
    [InlineData("claro", TemaPreferido.Automatico)]
    [InlineData("3", TemaPreferido.Automatico)]
    public void Interpretar_LeNomeOuInteiroJaGravado(string? valor, TemaPreferido tema)
    {
        Assert.Equal(tema, MapeamentoTema.Interpretar(valor));
    }

    [Fact]
    public void Automatico_TemCaseExplicitoEmTextoUserAppThemeEIsDarkMode()
    {
        var fonte = File.ReadAllText(Path.Combine(EncontrarRaiz(), "OrcaFacil.Core", "Tema", "MapeamentoTema.cs"));

        Assert.Contains("TemaPreferido.Automatico => TextoAutomatico", fonte, StringComparison.Ordinal);
        Assert.Contains("TemaPreferido.Automatico => TemaUsuarioApp.Unspecified", fonte, StringComparison.Ordinal);
        Assert.Contains("TemaPreferido.Automatico => sistemaEscuro", fonte, StringComparison.Ordinal);
        Assert.Equal(MapeamentoTema.TextoAutomatico, MapeamentoTema.ParaTexto(TemaPreferido.Automatico));
        Assert.Equal(TemaUsuarioApp.Unspecified, MapeamentoTema.ParaUserAppTheme(TemaPreferido.Automatico));
        Assert.False(MapeamentoTema.IsDarkMode(TemaPreferido.Automatico, sistemaEscuro: false));
        Assert.True(MapeamentoTema.IsDarkMode(TemaPreferido.Automatico, sistemaEscuro: true));
    }

    [Theory]
    [InlineData(TemaUsuarioApp.Dark, true)]
    [InlineData(TemaUsuarioApp.Light, false)]
    [InlineData(TemaUsuarioApp.Unspecified, false)]
    public void IsDarkMode_DoTemaSolicitado_SoEEscuroQuandoDark(TemaUsuarioApp temaSolicitado, bool isDarkMode)
    {
        Assert.Equal(isDarkMode, MapeamentoTema.IsDarkMode(temaSolicitado));
        Assert.Equal(isDarkMode ? MapeamentoTema.ClasseEscuro : MapeamentoTema.ClasseClaro, MapeamentoTema.ClasseCss(isDarkMode));
    }

    private static string EncontrarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OrcaFacil.Core", "Tema", "MapeamentoTema.cs")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar MapeamentoTema.cs a partir de " + AppContext.BaseDirectory);
    }
}
