namespace OrcaFacil.Core.Llm;

public enum ProvedorIa
{
    Anthropic,
    Alibaba
}

/// <summary>
/// Valores que o chip grava em Preferences. Não carrega a chave da API.
/// </summary>
public readonly record struct AjusteChipIa(string? Url, string? Modelo, string? RotuloModelo)
{
    public override string ToString() => "AjusteChipIa";
}

/// <summary>
/// Chaves de Preferences e o padrão de cada chip.
/// Anthropic preenche URL, modelo e o rótulo Haiku 5.5.
/// Alibaba não preenche URL nem modelo: a URL fica editável e o modelo é livre.
/// </summary>
public static class ChipsInteligencia
{
    public const string ChaveUrl = "Config.Ia.Url";
    public const string ChaveModelo = "Config.Ia.Modelo";
    public const string ChaveProvedor = "Config.Ia.Provedor";
    public const string ChaveMaxTokens = "Config.Ia.MaxTokens";

    public const string UrlAnthropic = "https://api.anthropic.com";
    public const string ModeloAnthropic = PedidoMensagem.ModeloAnthropic;
    public const string RotuloAnthropic = "Haiku 5.5";

    public static IReadOnlyList<ProvedorIa> Provedores { get; } = [ProvedorIa.Anthropic, ProvedorIa.Alibaba];

    public static IReadOnlyList<string> ChavesPreferencia { get; } =
        [ChaveUrl, ChaveModelo, ChaveProvedor, ChaveMaxTokens];

    public static string Texto(ProvedorIa provedor) => provedor switch
    {
        ProvedorIa.Anthropic => "Anthropic",
        ProvedorIa.Alibaba => "Alibaba",
        _ => throw new ArgumentOutOfRangeException(nameof(provedor))
    };

    public static ProvedorIa? Interpretar(string? texto)
    {
        if (string.Equals(texto, Texto(ProvedorIa.Anthropic), StringComparison.Ordinal))
            return ProvedorIa.Anthropic;
        if (string.Equals(texto, Texto(ProvedorIa.Alibaba), StringComparison.Ordinal))
            return ProvedorIa.Alibaba;
        return null;
    }

    public static AjusteChipIa Padrao(ProvedorIa provedor) => provedor switch
    {
        ProvedorIa.Anthropic => new(UrlAnthropic, ModeloAnthropic, RotuloAnthropic),
        ProvedorIa.Alibaba => new(null, null, null),
        _ => throw new ArgumentOutOfRangeException(nameof(provedor))
    };

    public static string? RotuloDoModelo(string? modelo) =>
        string.Equals(modelo?.Trim(), ModeloAnthropic, StringComparison.Ordinal) ? RotuloAnthropic : null;
}
