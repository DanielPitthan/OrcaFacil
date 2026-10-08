using OrcaFacil.Core.Enums;
using OrcaFacil.Services.Interfaces;

namespace OrcaFacil.Services;

public class ConfiguracaoService : IConfiguracaoService
{
    private const string ChaveNotificacoesAtivas = "Config.NotificacoesAtivas";
    private const string ChaveSomAtivo = "Config.SomAtivo";
    private const string ChavePrazosAviso = "Config.PrazosAviso";

    public const PrazoAviso PrazosPadrao = PrazoAviso.UmDiaAntes | PrazoAviso.NoDia | PrazoAviso.Vencidas;

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

    public void Redefinir() => Preferences.Default.Clear();
}
