using OrcaFacil.Core.Tema;
using OrcaFacil.Services.Interfaces;

namespace OrcaFacil.Services;

public sealed class TemaAppService : ITemaAppService
{
    private readonly PreferenciasTema _tema;

    public TemaAppService(PreferenciasTema tema)
    {
        _tema = tema;
    }

    public void AplicarPreferenciaSalva()
        => AplicarUserAppTheme(MapeamentoTema.ParaUserAppTheme(_tema.Tema));

    public void DefinirEAplicar(TemaPreferido tema)
        => AplicacaoDeTema.DefinirEAplicar(_tema, tema, AplicarUserAppTheme);

    private static void AplicarUserAppTheme(TemaUsuarioApp temaUsuario)
    {
        if (Application.Current is not Application app)
            return;

        var desejado = ParaAppTheme(temaUsuario);

        // O setter do MAUI dispara RequestedThemeChanged na hora.
        // Valor igual não deve ser regravado: o evento não sai e o layout não renderiza de novo.
        if (app.UserAppTheme == desejado)
            return;

        app.UserAppTheme = desejado;
    }

    public static AppTheme ParaAppTheme(TemaUsuarioApp tema) => tema switch
    {
        TemaUsuarioApp.Light => AppTheme.Light,
        TemaUsuarioApp.Dark => AppTheme.Dark,
        _ => AppTheme.Unspecified
    };
}
