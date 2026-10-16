using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Tema;

namespace OrcaFacil.Services.Interfaces;

/// <summary>Preferências do usuário persistidas localmente (Preferences do MAUI).</summary>
public interface IConfiguracaoService
{
    bool NotificacoesAtivas { get; set; }
    bool SomAtivo { get; set; }
    PrazoAviso PrazosAviso { get; set; }

    /// <summary>Gráfico da listagem de lançamentos. Começa expandido.</summary>
    bool GraficoLancamentosExpandido { get; set; }

    /// <summary>
    /// Tema da interface. Chave <c>Config.Tema</c>: Claro, Escuro ou Automatico. Padrão: Automatico.
    /// Para gravar e aplicar juntos, use <see cref="ITemaAppService.DefinirEAplicar"/>.
    /// </summary>
    TemaPreferido Tema { get; set; }

    /// <summary>Remove as preferências gravadas e volta ao padrão. A chave <c>Config.Tema</c> é preservada.</summary>
    void Redefinir();
}
