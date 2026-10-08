using OrcaFacil.Core.Enums;

namespace OrcaFacil.Services.Interfaces;

/// <summary>Preferências do usuário persistidas localmente (Preferences do MAUI).</summary>
public interface IConfiguracaoService
{
    bool NotificacoesAtivas { get; set; }
    bool SomAtivo { get; set; }
    PrazoAviso PrazosAviso { get; set; }

    /// <summary>Remove todas as preferências gravadas, voltando aos valores padrão.</summary>
    void Redefinir();
}
