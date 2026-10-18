using OrcaFacil.Core.Armazenamento.Espelho.Pasta;
using OrcaFacil.Core.Llm;

namespace OrcaFacil.Core.Tema;

/// <summary>
/// Apaga as preferências gravadas e devolve <c>Config.Tema</c> como estava.
/// A URI da pasta do espelho também fica: redefinir o tema não desliga o espelho.
/// Se o cofre foi informado, também esvazia a chave da API.
/// <c>Preferences.Clear</c> não alcança o SecureStorage.
/// <c>ConfiguracaoService.Redefinir</c> só delega para este método.
/// </summary>
public interface IRedefinicaoPreferencias
{
    void Redefinir();
}

/// <summary>
/// Chave <c>Config.Tema</c>. O padrão é Automático e só é gravado quando alguém escolhe.
/// <see cref="Redefinir"/> apaga as outras preferências, devolve esta chave como estava,
/// preserva <see cref="UriEscolhaPasta.ChavePreferencia"/> e, com cofre, chama
/// <see cref="ICofreChaveApi.RemoverTudo"/>.
/// </summary>
public sealed class PreferenciasTema : IRedefinicaoPreferencias
{
    public const string Chave = "Config.Tema";

    private readonly IPreferenciasSimples _preferencias;
    private readonly ICofreChaveApi? _cofre;

    public PreferenciasTema(IPreferenciasSimples preferencias, ICofreChaveApi? cofre = null)
    {
        _preferencias = preferencias;
        _cofre = cofre;
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
        var preservarPasta = _preferencias.Contem(UriEscolhaPasta.ChavePreferencia);
        var uriPasta = preservarPasta ? _preferencias.Obter(UriEscolhaPasta.ChavePreferencia, "") : null;
        _preferencias.Limpar();
        if (preservar)
            _preferencias.Definir(Chave, texto ?? MapeamentoTema.TextoAutomatico);
        if (preservarPasta)
            _preferencias.Definir(UriEscolhaPasta.ChavePreferencia, uriPasta ?? "");

        // Depois de devolver tema e pasta: uma falha do cofre não desfaz essa preservação.
        _cofre?.RemoverTudo();
    }
}
