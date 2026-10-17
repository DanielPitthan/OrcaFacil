using System.Globalization;

namespace OrcaFacil.Core.Tema;

/// <summary>
/// Armazém de preferências que pode ter gravado texto ou inteiro na mesma chave.
/// <see cref="PreferenciasMaui"/> é o adaptador sobre <c>Preferences.Default</c>.
/// </summary>
public interface IRepositorioPreferencias
{
    bool Contem(string chave);

    /// <summary>Texto gravado, ou null quando a chave não é texto.</summary>
    string? LerTexto(string chave);

    /// <summary>Inteiro gravado, ou null quando a chave não é inteiro.</summary>
    int? LerInteiro(string chave);

    void GravarTexto(string chave, string valor);
    void Limpar();
}

/// <summary>
/// Lê <c>Config.Tema</c> como texto (<c>Escuro</c>) ou como o inteiro do enum (<c>2</c>).
/// A leitura não regrava a chave.
/// </summary>
public sealed class AdaptadorPreferencias : IPreferenciasSimples
{
    private readonly IRepositorioPreferencias _repositorio;

    public AdaptadorPreferencias(IRepositorioPreferencias repositorio)
    {
        _repositorio = repositorio;
    }

    public bool Contem(string chave) => _repositorio.Contem(chave);

    public string Obter(string chave, string padrao)
    {
        if (!_repositorio.Contem(chave))
            return padrao;

        var texto = _repositorio.LerTexto(chave);
        if (!string.IsNullOrEmpty(texto))
            return texto;

        if (_repositorio.LerInteiro(chave) is int numero)
            return numero.ToString(CultureInfo.InvariantCulture);

        return padrao;
    }

    public void Definir(string chave, string valor) => _repositorio.GravarTexto(chave, valor);

    public void Limpar() => _repositorio.Limpar();
}
