using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Llm;
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

    /// <summary>URL da API. Chave <c>Config.Ia.Url</c>. A chave da API não fica em Preferences.</summary>
    string UrlInteligencia { get; set; }

    /// <summary>Modelo da API. Chave <c>Config.Ia.Modelo</c>.</summary>
    string ModeloInteligencia { get; set; }

    /// <summary>Chip escolhido. Chave <c>Config.Ia.Provedor</c>. Nulo quando nenhum foi salvo.</summary>
    ProvedorIa? ProvedorInteligencia { get; set; }

    /// <summary>
    /// Remove as preferências gravadas e volta ao padrão.
    /// Preserva <c>Config.Tema</c> e a URI da pasta do espelho.
    /// Também tenta apagar a chave da API no SecureStorage, que <c>Preferences.Clear</c> não alcança.
    /// <c>false</c> quando a chave pode ter permanecido. Pode ser chamado de novo.
    /// </summary>
    Task<bool> Redefinir();
}
