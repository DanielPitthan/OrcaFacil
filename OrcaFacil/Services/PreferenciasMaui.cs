using OrcaFacil.Core.Tema;

namespace OrcaFacil.Services;

/// <summary>Adaptador de <see cref="IPreferenciasSimples"/> sobre <c>Preferences.Default</c>.</summary>
public sealed class PreferenciasMaui : IPreferenciasSimples
{
    public bool Contem(string chave) => Preferences.Default.ContainsKey(chave);

    public string Obter(string chave, string padrao) => Preferences.Default.Get(chave, padrao);

    public void Definir(string chave, string valor) => Preferences.Default.Set(chave, valor);

    public void Limpar() => Preferences.Default.Clear();
}
