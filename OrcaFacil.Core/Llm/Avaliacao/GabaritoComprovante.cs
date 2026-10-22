using System.Globalization;
using System.Text.Json;

namespace OrcaFacil.Core.Llm.Avaliacao;

public sealed record GabaritoItem(string Descricao, decimal Preco)
{
    public override string ToString() => "GabaritoItem";
}

/// <summary>
/// Gabarito de um comprovante sintético ou, no futuro, do corpus local.
/// O <see cref="Id"/> não é nome de arquivo nem dado pessoal.
/// </summary>
public sealed record GabaritoComprovante(
    string Id,
    DateOnly Data,
    decimal TotalCupom,
    IReadOnlyList<GabaritoItem> Itens)
{
    public override string ToString() => "GabaritoComprovante";
}

/// <summary>
/// Lê o JSON do gabarito. Números só no formato invariante (ponto decimal).
/// A exceção traz motivo fixo, sem ecoar o JSON.
/// </summary>
public static class LeitorGabarito
{
    public const string MotivoJsonInvalido = "JSON inválido";

    private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;

    public static string MotivoCampoAusente(string campo) => "campo ausente: " + campo;

    public static string MotivoCampoInvalido(string campo) => "campo inválido: " + campo;

    public static GabaritoComprovante Ler(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new FormatException(MotivoJsonInvalido);

        var texto = json.Trim().TrimStart('\uFEFF');
        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(texto);
        }
        catch (JsonException)
        {
            throw new FormatException(MotivoJsonInvalido);
        }

        using (documento)
        {
            var raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object)
                throw new FormatException(MotivoJsonInvalido);

            var id = ExigirTexto(raiz, "id");
            var data = ExigirData(raiz);
            var total = ExigirDecimal(raiz, "total");
            var itens = ExigirItens(raiz);
            return new GabaritoComprovante(id, data, total, itens);
        }
    }

    private static string ExigirTexto(JsonElement objeto, string campo)
    {
        if (!objeto.TryGetProperty(campo, out var valor))
            throw new FormatException(MotivoCampoAusente(campo));

        if (valor.ValueKind != JsonValueKind.String)
            throw new FormatException(MotivoCampoInvalido(campo));

        var texto = valor.GetString()?.Trim();
        if (string.IsNullOrEmpty(texto))
            throw new FormatException(MotivoCampoInvalido(campo));

        return texto;
    }

    private static DateOnly ExigirData(JsonElement objeto)
    {
        if (!objeto.TryGetProperty("data", out var valor))
            throw new FormatException(MotivoCampoAusente("data"));

        if (valor.ValueKind != JsonValueKind.String)
            throw new FormatException(MotivoCampoInvalido("data"));

        var texto = valor.GetString();
        if (!DateOnly.TryParseExact(texto, "yyyy-MM-dd", Invariante, DateTimeStyles.None, out var data))
            throw new FormatException(MotivoCampoInvalido("data"));

        return data;
    }

    private static decimal ExigirDecimal(JsonElement objeto, string campo)
    {
        if (!objeto.TryGetProperty(campo, out var valor))
            throw new FormatException(MotivoCampoAusente(campo));

        if (valor.ValueKind != JsonValueKind.Number || !valor.TryGetDecimal(out var numero))
            throw new FormatException(MotivoCampoInvalido(campo));

        return numero;
    }

    private static IReadOnlyList<GabaritoItem> ExigirItens(JsonElement objeto)
    {
        if (!objeto.TryGetProperty("itens", out var valor))
            throw new FormatException(MotivoCampoAusente("itens"));

        if (valor.ValueKind != JsonValueKind.Array)
            throw new FormatException(MotivoCampoInvalido("itens"));

        var itens = new List<GabaritoItem>();
        foreach (var item in valor.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                throw new FormatException(MotivoCampoInvalido("itens"));

            itens.Add(new GabaritoItem(ExigirTexto(item, "descricao"), ExigirDecimal(item, "preco")));
        }

        return itens;
    }
}
