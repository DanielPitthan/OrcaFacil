namespace OrcaFacil.Core.Tema;

/// <summary>
/// Apaga as preferências gravadas e devolve <c>Config.Tema</c> como estava.
/// <c>ConfiguracaoService.Redefinir</c> só delega para este método.
/// </summary>
public interface IRedefinicaoPreferencias
{
    void Redefinir();
}

/// <summary>
/// Chave <c>Config.Tema</c>. O padrão é Automático e só é gravado quando alguém escolhe.
/// <see cref="Redefinir"/> apaga as outras preferências e devolve esta chave como estava.
/// </summary>
public sealed class PreferenciasTema : IRedefinicaoPreferencias
{
    public const string Chave = "Config.Tema";

    private readonly IPreferenciasSimples _preferencias;

    public PreferenciasTema(IPreferenciasSimples preferencias)
    {
        _preferencias = preferencias;
    }

    public TemaPreferido Tema
    {
        get => MapeamentoTema.Interpretar(_preferencias.Obter(Chave, MapeamentoTema.TextoAutomatico));
        set => _preferencias.Definir(Chave, MapeamentoTema.ParaTexto(value));
    }

    public void Redefinir()
    {
        var preservar = _preferencias.Contem(Chave);
        var texto = preservar ? _preferencias.Obter(Chave, MapeamentoTema.TextoAutomatico) : null;
        _preferencias.Limpar();
        if (preservar)
            _preferencias.Definir(Chave, texto ?? MapeamentoTema.TextoAutomatico);
    }
}
