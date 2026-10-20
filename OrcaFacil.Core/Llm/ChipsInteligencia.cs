namespace OrcaFacil.Core.Llm;

public enum ProvedorIa
{
    Anthropic,
    Alibaba,
    DeepSeek
}

/// <summary>
/// Valores que o chip grava em Preferences. Não carrega a chave da API.
/// </summary>
public readonly record struct AjusteChipIa(string? Url, string? Modelo, string? RotuloModelo)
{
    public override string ToString() => "AjusteChipIa";
}

/// <summary>
/// Provedor, URL e modelo usados na tela e na leitura.
/// Não carrega a chave da API.
/// </summary>
public readonly record struct ConfiguracaoIaEfetiva(ProvedorIa? Provedor, string Url, string Modelo)
{
    public override string ToString() => "ConfiguracaoIaEfetiva";
}

/// <summary>
/// Chaves de Preferences e o padrão de cada chip.
/// DeepSeek preenche URL, modelo e o rótulo DeepSeek Flash.
/// Anthropic preenche URL, modelo e o rótulo Haiku 5.5.
/// Alibaba não preenche URL nem modelo: a URL fica editável e o modelo é livre.
/// </summary>
public static class ChipsInteligencia
{
    public const string ChaveUrl = "Config.Ia.Url";
    public const string ChaveModelo = "Config.Ia.Modelo";
    public const string ChaveProvedor = "Config.Ia.Provedor";
    public const string ChaveMaxTokens = "Config.Ia.MaxTokens";

    public const string UrlDeepSeek = "https://api.deepseek.com/anthropic";
    public const string ModeloDeepSeek = PedidoMensagem.ModeloDeepSeek;
    public const string RotuloDeepSeek = "DeepSeek Flash";

    public const string UrlAnthropic = "https://api.anthropic.com";
    public const string ModeloAnthropic = PedidoMensagem.ModeloAnthropic;
    public const string RotuloAnthropic = "Haiku 5.5";

    public static IReadOnlyList<ProvedorIa> Provedores { get; } =
        [ProvedorIa.DeepSeek, ProvedorIa.Anthropic, ProvedorIa.Alibaba];

    public static IReadOnlyList<string> ChavesPreferencia { get; } =
        [ChaveUrl, ChaveModelo, ChaveProvedor, ChaveMaxTokens];

    public static string Texto(ProvedorIa provedor) => provedor switch
    {
        ProvedorIa.DeepSeek => "DeepSeek",
        ProvedorIa.Anthropic => "Anthropic",
        ProvedorIa.Alibaba => "Alibaba",
        _ => throw new ArgumentOutOfRangeException(nameof(provedor))
    };

    public static ProvedorIa? Interpretar(string? texto)
    {
        if (string.Equals(texto, Texto(ProvedorIa.DeepSeek), StringComparison.Ordinal))
            return ProvedorIa.DeepSeek;
        if (string.Equals(texto, Texto(ProvedorIa.Anthropic), StringComparison.Ordinal))
            return ProvedorIa.Anthropic;
        if (string.Equals(texto, Texto(ProvedorIa.Alibaba), StringComparison.Ordinal))
            return ProvedorIa.Alibaba;
        return null;
    }

    public static AjusteChipIa Padrao(ProvedorIa provedor) => provedor switch
    {
        ProvedorIa.DeepSeek => new(UrlDeepSeek, ModeloDeepSeek, RotuloDeepSeek),
        ProvedorIa.Anthropic => new(UrlAnthropic, ModeloAnthropic, RotuloAnthropic),
        ProvedorIa.Alibaba => new(null, null, null),
        _ => throw new ArgumentOutOfRangeException(nameof(provedor))
    };

    /// <summary>
    /// Sem provedor, URL e modelo salvos, o padrão efetivo é o DeepSeek.
    /// Qualquer um dos três preenchido permanece como foi gravado.
    /// </summary>
    public static ConfiguracaoIaEfetiva Efetiva(ProvedorIa? provedor, string? url, string? modelo)
    {
        if (provedor is null && string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(modelo))
        {
            var padrao = Padrao(ProvedorIa.DeepSeek);
            return new(ProvedorIa.DeepSeek, padrao.Url ?? "", padrao.Modelo ?? "");
        }

        return new(provedor, url ?? "", modelo ?? "");
    }

    public static string? RotuloDoModelo(string? modelo)
    {
        var texto = modelo?.Trim();
        if (string.Equals(texto, ModeloDeepSeek, StringComparison.Ordinal))
            return RotuloDeepSeek;
        if (string.Equals(texto, ModeloAnthropic, StringComparison.Ordinal))
            return RotuloAnthropic;
        return null;
    }
}
