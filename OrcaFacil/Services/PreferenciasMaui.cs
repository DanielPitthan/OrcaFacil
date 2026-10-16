using OrcaFacil.Core.Tema;

namespace OrcaFacil.Services;

/// <summary>Adaptador de <see cref="IPreferenciasSimples"/> sobre <c>Preferences.Default</c>.</summary>
public sealed class PreferenciasMaui : IPreferenciasSimples
{
    private readonly AdaptadorPreferencias _adaptador = new(new RepositorioPreferences());

    public bool Contem(string chave) => _adaptador.Contem(chave);

    public string Obter(string chave, string padrao) => _adaptador.Obter(chave, padrao);

    public void Definir(string chave, string valor) => _adaptador.Definir(chave, valor);

    public void Limpar() => _adaptador.Limpar();

    private sealed class RepositorioPreferences : IRepositorioPreferencias
    {
        public bool Contem(string chave) => Preferences.Default.ContainsKey(chave);

        public string? LerTexto(string chave)
        {
            try
            {
                return Preferences.Default.Get(chave, "");
            }
            catch (Exception ex) when (TipoIncompativel(ex))
            {
                return null;
            }
        }

        public int? LerInteiro(string chave)
        {
            try
            {
                return Preferences.Default.Get(chave, 0);
            }
            catch (Exception ex) when (TipoIncompativel(ex))
            {
                return null;
            }
        }

        public void GravarTexto(string chave, string valor) => Preferences.Default.Set(chave, valor);

        public void Limpar() => Preferences.Default.Clear();

        private static bool TipoIncompativel(Exception ex)
        {
            for (var atual = ex; atual is not null; atual = atual.InnerException)
            {
                if (atual.GetType().Name.Contains("Cast", StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
