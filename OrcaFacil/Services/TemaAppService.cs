using OrcaFacil.Core.Tema;
using OrcaFacil.Services.Interfaces;

namespace OrcaFacil.Services;

public sealed class TemaAppService : ITemaAppService
{
    private readonly IConfiguracaoService _configuracao;

    public TemaAppService(IConfiguracaoService configuracao)
    {
        _configuracao = configuracao;
    }

    public void AplicarPreferenciaSalva()
    {
        if (Application.Current is not Application app)
            return;

        var desejado = ParaAppTheme(MapeamentoTema.ParaUserAppTheme(_configuracao.Tema));

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
