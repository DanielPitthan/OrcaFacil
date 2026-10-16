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

    [Theory]
    [InlineData(TemaUsuarioApp.Dark, true)]
    [InlineData(TemaUsuarioApp.Light, false)]
    [InlineData(TemaUsuarioApp.Unspecified, false)]
    public void IsDarkMode_DoTemaSolicitado_SoEEscuroQuandoDark(TemaUsuarioApp temaSolicitado, bool isDarkMode)
    {
        Assert.Equal(isDarkMode, MapeamentoTema.IsDarkMode(temaSolicitado));
        Assert.Equal(isDarkMode ? MapeamentoTema.ClasseEscuro : MapeamentoTema.ClasseClaro, MapeamentoTema.ClasseCss(isDarkMode));
    }
}
