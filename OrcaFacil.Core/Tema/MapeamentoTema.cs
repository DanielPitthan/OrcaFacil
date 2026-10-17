using System.Globalization;

namespace OrcaFacil.Core.Tema;

/// <summary>Resultado de traduzir a preferência para o tema do MAUI e para o modo escuro efetivo.</summary>
/// <param name="UserAppTheme">Valor a gravar em <c>Application.UserAppTheme</c>.</param>
/// <param name="IsDarkMode">Verdadeiro só quando a interface deve usar a paleta escura.</param>
/// <param name="ClasseCss"><c>of-tema-claro</c> ou <c>of-tema-escuro</c>, no html e no body.</param>
public readonly record struct TemaResolvido(TemaUsuarioApp UserAppTheme, bool IsDarkMode, string ClasseCss);

/// <summary>
/// Traduz a preferência Claro/Escuro/Automático para <c>UserAppTheme</c> e <c>IsDarkMode</c>.
/// Automático não força tema: <c>IsDarkMode</c> acompanha o sistema.
/// </summary>
public static class MapeamentoTema
{
    public const string TextoClaro = "Claro";
    public const string TextoEscuro = "Escuro";
    public const string TextoAutomatico = "Automatico";
    public const string ClasseClaro = "of-tema-claro";
    public const string ClasseEscuro = "of-tema-escuro";

    public static TemaPreferido Padrao => TemaPreferido.Automatico;

    public static string ParaTexto(TemaPreferido tema) => tema switch
    {
        TemaPreferido.Claro => TextoClaro,
        TemaPreferido.Escuro => TextoEscuro,
        TemaPreferido.Automatico => TextoAutomatico,
        _ => TextoAutomatico
    };

    /// <summary>
    /// Aceita o nome já gravado (<c>Claro</c>, <c>Escuro</c>, <c>Automatico</c>)
    /// ou o inteiro do enum (<c>1</c>, <c>2</c>, <c>0</c>). Não regrava a chave.
    /// </summary>
    public static TemaPreferido Interpretar(string? valor)
    {
        switch (valor)
        {
            case TextoClaro:
                return TemaPreferido.Claro;
            case TextoEscuro:
                return TemaPreferido.Escuro;
            case TextoAutomatico:
                return TemaPreferido.Automatico;
        }

        if (int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero))
        {
            var candidato = (TemaPreferido)numero;
            if (Enum.IsDefined(candidato))
                return candidato;
        }

        return Padrao;
    }

    public static TemaUsuarioApp ParaUserAppTheme(TemaPreferido preferencia) => preferencia switch
    {
        TemaPreferido.Claro => TemaUsuarioApp.Light,
        TemaPreferido.Escuro => TemaUsuarioApp.Dark,
        TemaPreferido.Automatico => TemaUsuarioApp.Unspecified,
        _ => TemaUsuarioApp.Unspecified
    };

    /// <summary>
    /// Claro e Escuro ignoram o sistema. Automático usa <paramref name="sistemaEscuro"/>.
    /// </summary>
    public static bool IsDarkMode(TemaPreferido preferencia, bool sistemaEscuro) => preferencia switch
    {
        TemaPreferido.Claro => false,
        TemaPreferido.Escuro => true,
        TemaPreferido.Automatico => sistemaEscuro,
        _ => sistemaEscuro
    };

    /// <summary>
    /// Tema já resolvido pelo MAUI (<c>RequestedTheme</c>).
    /// <see cref="TemaUsuarioApp.Unspecified"/> ainda não é escuro.
    /// </summary>
    public static bool IsDarkMode(TemaUsuarioApp temaSolicitado) => temaSolicitado == TemaUsuarioApp.Dark;

    public static string ClasseCss(bool isDarkMode) => isDarkMode ? ClasseEscuro : ClasseClaro;

    public static TemaResolvido Resolver(TemaPreferido preferencia, bool sistemaEscuro)
    {
        var escuro = IsDarkMode(preferencia, sistemaEscuro);
        return new TemaResolvido(ParaUserAppTheme(preferencia), escuro, ClasseCss(escuro));
    }
}
