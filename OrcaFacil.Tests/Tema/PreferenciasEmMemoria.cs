using OrcaFacil.Core.Tema;

namespace OrcaFacil.Tests.Tema;

internal sealed class PreferenciasEmMemoria : IPreferenciasSimples
{
    private readonly Dictionary<string, string> _valores = new(StringComparer.Ordinal);

    public bool Contem(string chave) => _valores.ContainsKey(chave);

    public string Obter(string chave, string padrao) =>
        _valores.TryGetValue(chave, out var valor) ? valor : padrao;

    public void Definir(string chave, string valor) => _valores[chave] = valor;

    public void Limpar() => _valores.Clear();

    public string? ValorOuNulo(string chave) =>
        _valores.TryGetValue(chave, out var valor) ? valor : null;
}
