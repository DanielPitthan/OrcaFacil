using OrcaFacil.Core.Tema;

namespace OrcaFacil.Tests.Tema;

/// <summary>Imita o Preferences do MAUI: a mesma chave pode ser texto ou inteiro.</summary>
internal sealed class RepositorioPreferenciasMemoria : IRepositorioPreferencias
{
    private readonly Dictionary<string, object> _valores = new(StringComparer.Ordinal);

    public bool Contem(string chave) => _valores.ContainsKey(chave);

    public string? LerTexto(string chave) =>
        _valores.TryGetValue(chave, out var valor) && valor is string texto ? texto : null;

    public int? LerInteiro(string chave) =>
        _valores.TryGetValue(chave, out var valor) && valor is int numero ? numero : null;

    public void GravarTexto(string chave, string valor) => _valores[chave] = valor;

    public void GravarInteiro(string chave, int valor) => _valores[chave] = valor;

    public void Limpar() => _valores.Clear();
}
