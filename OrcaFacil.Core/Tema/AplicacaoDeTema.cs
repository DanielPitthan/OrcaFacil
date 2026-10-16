namespace OrcaFacil.Core.Tema;

/// <summary>
/// Caminho único que grava <c>Config.Tema</c> e devolve o tema a aplicar no app.
/// A aba Aparência usa este método; o MAUI só executa o <paramref name="aplicar"/>.
/// </summary>
public static class AplicacaoDeTema
{
    public static TemaUsuarioApp DefinirEAplicar(
        PreferenciasTema preferencias,
        TemaPreferido tema,
        Action<TemaUsuarioApp> aplicar)
    {
        ArgumentNullException.ThrowIfNull(preferencias);
        ArgumentNullException.ThrowIfNull(aplicar);

        preferencias.Tema = tema;
        var userAppTheme = MapeamentoTema.ParaUserAppTheme(tema);
        aplicar(userAppTheme);
        return userAppTheme;
    }
}
