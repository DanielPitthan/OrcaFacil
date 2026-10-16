using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Tema;
using OrcaFacil.Services.Interfaces;

namespace OrcaFacil.Services;

public class ConfiguracaoService : IConfiguracaoService
{
    private const string ChaveNotificacoesAtivas = "Config.NotificacoesAtivas";
    private const string ChaveSomAtivo = "Config.SomAtivo";
    private const string ChavePrazosAviso = "Config.PrazosAviso";
    private const string ChaveGraficoLancamentos = "Config.GraficoLancamentosExpandido";

    public const PrazoAviso PrazosPadrao = PrazoAviso.UmDiaAntes | PrazoAviso.NoDia | PrazoAviso.Vencidas;

    private readonly PreferenciasTema _tema;

    public ConfiguracaoService(PreferenciasTema tema)
    {
        _tema = tema;
    }

    public bool NotificacoesAtivas
    {
        get => Preferences.Default.Get(ChaveNotificacoesAtivas, true);
        set => Preferences.Default.Set(ChaveNotificacoesAtivas, value);
    }

    public bool SomAtivo
    {
        get => Preferences.Default.Get(ChaveSomAtivo, true);
        set => Preferences.Default.Set(ChaveSomAtivo, value);
    }

    public PrazoAviso PrazosAviso
    {
        get => (PrazoAviso)Preferences.Default.Get(ChavePrazosAviso, (int)PrazosPadrao);
        set => Preferences.Default.Set(ChavePrazosAviso, (int)value);
    }

    public bool GraficoLancamentosExpandido
    {
        get => Preferences.Default.Get(ChaveGraficoLancamentos, true);
        set => Preferences.Default.Set(ChaveGraficoLancamentos, value);
    }

    public TemaPreferido Tema
    {
        get => _tema.Tema;
        set => _tema.Tema = value;
    }

    public void Redefinir() => LimpezaDePreferencias.Aplicar(TipoLimpezaDados.Tudo, _tema);
}
