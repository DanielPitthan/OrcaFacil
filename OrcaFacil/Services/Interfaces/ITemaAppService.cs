using OrcaFacil.Core.Tema;

namespace OrcaFacil.Services.Interfaces;

/// <summary>
/// Aplica a preferência <c>Config.Tema</c> em <c>Application.UserAppTheme</c>.
/// Automático grava <c>Unspecified</c> e o sistema continua mandando.
/// Claro e Escuro gravam Light e Dark. O método não renderiza o Blazor:
/// <c>RequestedThemeChanged</c> é quem avisa o layout.
/// </summary>
public interface ITemaAppService
{
    void AplicarPreferenciaSalva();

    /// <summary>Grava <c>Config.Tema</c> e aplica no MAUI. Caminho único da aba Aparência.</summary>
    void DefinirEAplicar(TemaPreferido tema);
}
