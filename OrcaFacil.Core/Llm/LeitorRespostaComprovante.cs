using System.Globalization;
using System.Text.Json;

namespace OrcaFacil.Core.Llm;

/// <summary>
/// Extrai o <c>input</c> do bloco <c>tool_use</c> e ignora <c>thinking</c>.
/// Sem <c>reconhecido</c>, o item é lido como não reconhecido.
/// </summary>
public static class LeitorRespostaComprovante
{
    private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;

    public static ComprovanteLido? Ler(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var documento = JsonDocument.Parse(json);
            if (!TentarInput(documento.RootElement, out var input))
                return null;

            return LerInput(input);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TentarInput(JsonElement raiz, out JsonElement input)
    {
        input = default;
        if (raiz.ValueKind != JsonValueKind.Object)
            return false;

        if (!raiz.TryGetProperty("content", out var blocos) || blocos.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var bloco in blocos.EnumerateArray())
        {
            if (!EhFerramenta(bloco))
                continue;

            if (!bloco.TryGetProperty("input", out var candidato) || candidato.ValueKind != JsonValueKind.Object)
                return false;

            input = candidato;
            return true;
        }

        return false;
    }

    private static bool EhFerramenta(JsonElement bloco)
    {
        if (bloco.ValueKind != JsonValueKind.Object)
            return false;

        if (!bloco.TryGetProperty("type", out var tipo) || tipo.ValueKind != JsonValueKind.String)
            return false;

        if (tipo.GetString() != "tool_use")
            return false;

        return bloco.TryGetProperty("name", out var nome) &&
            nome.ValueKind == JsonValueKind.String &&
            nome.GetString() == PedidoMensagem.NomeFerramenta;
    }

    private static ComprovanteLido? LerInput(JsonElement input)
    {
        if (!TextoOpcional(input, "loja", out var loja))
            return null;

        if (!DataOpcional(input, out var data))
            return null;

        if (!DecimalOpcional(input, "total_cupom", out var total))
            return null;

        if (!BoolObrigatorio(input, "legivel", out var legivel))
            return null;

        if (!input.TryGetProperty("itens", out var itens) || itens.ValueKind != JsonValueKind.Array)
            return null;

        var lista = new List<ItemComprovanteLido>();
        foreach (var item in itens.EnumerateArray())
        {
            var lido = LerItem(item);
            if (lido is null)
                return null;

            lista.Add(lido);
        }

        return new ComprovanteLido(loja, data, total, legivel, lista);
    }

    private static ItemComprovanteLido? LerItem(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
            return null;

        if (!TextoObrigatorio(item, "descricao_lida", out var descricao))
            return null;

        if (!Produto(item, out var produto))
            return null;

        if (!Reconhecido(item, out var reconhecido))
            return null;

        if (!DecimalObrigatorio(item, "preco", out var preco))
            return null;

        if (!TextoOpcional(item, "quantidade", out var quantidade))
            return null;

        if (!TextoOpcional(item, "unidade", out var unidade))
            return null;

        if (!DecimalOpcional(item, "desconto", out var desconto))
            return null;

        if (!Confianca(item, out var confianca))
            return null;

        return new ItemComprovanteLido(
            descricao, produto, reconhecido, preco, quantidade, unidade, desconto, confianca);
    }

    private static bool Ausente(JsonElement objeto, string nome)
    {
        return !objeto.TryGetProperty(nome, out var valor) ||
            valor.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;
    }

    private static bool TextoObrigatorio(JsonElement objeto, string nome, out string texto)
    {
        texto = string.Empty;
        if (!objeto.TryGetProperty(nome, out var valor) || valor.ValueKind != JsonValueKind.String)
            return false;

        texto = valor.GetString() ?? string.Empty;
        return true;
    }

    private static bool TextoOpcional(JsonElement objeto, string nome, out string? texto)
    {
        texto = null;
        if (Ausente(objeto, nome))
            return true;

        if (objeto.GetProperty(nome).ValueKind != JsonValueKind.String)
            return false;

        texto = objeto.GetProperty(nome).GetString();
        return true;
    }

    private static bool Produto(JsonElement objeto, out string produto)
    {
        produto = string.Empty;
        if (Ausente(objeto, "produto"))
            return true;

        if (objeto.GetProperty("produto").ValueKind != JsonValueKind.String)
            return false;

        produto = objeto.GetProperty("produto").GetString() ?? string.Empty;
        return true;
    }

    private static bool Reconhecido(JsonElement objeto, out bool reconhecido)
    {
        reconhecido = false;
        if (Ausente(objeto, "reconhecido"))
            return true;

        var valor = objeto.GetProperty("reconhecido");
        if (valor.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            return false;

        reconhecido = valor.GetBoolean();
        return true;
    }

    private static bool BoolObrigatorio(JsonElement objeto, string nome, out bool valor)
    {
        valor = false;
        if (!objeto.TryGetProperty(nome, out var elemento))
            return false;

        if (elemento.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            return false;

        valor = elemento.GetBoolean();
        return true;
    }

    private static bool DecimalObrigatorio(JsonElement objeto, string nome, out decimal numero)
    {
        numero = 0;
        if (!objeto.TryGetProperty(nome, out var valor))
            return false;

        return valor.ValueKind == JsonValueKind.Number && valor.TryGetDecimal(out numero);
    }

    private static bool DecimalOpcional(JsonElement objeto, string nome, out decimal? numero)
    {
        numero = null;
        if (Ausente(objeto, nome))
            return true;

        var valor = objeto.GetProperty(nome);
        if (valor.ValueKind != JsonValueKind.Number || !valor.TryGetDecimal(out var lido))
            return false;

        numero = lido;
        return true;
    }

    private static bool DataOpcional(JsonElement objeto, out DateOnly? data)
    {
        data = null;
        if (Ausente(objeto, "data"))
            return true;

        if (objeto.GetProperty("data").ValueKind != JsonValueKind.String)
            return false;

        var texto = objeto.GetProperty("data").GetString();
        if (string.IsNullOrEmpty(texto))
            return true;

        if (!DateOnly.TryParseExact(texto, "yyyy-MM-dd", Invariante, DateTimeStyles.None, out var lida))
            return false;

        data = lida;
        return true;
    }

    private static bool Confianca(JsonElement objeto, out ConfiancaLeitura? confianca)
    {
        confianca = null;
        if (Ausente(objeto, "confianca"))
            return true;

        if (objeto.GetProperty("confianca").ValueKind != JsonValueKind.String)
            return false;

        confianca = objeto.GetProperty("confianca").GetString() switch
        {
            "alta" => ConfiancaLeitura.Alta,
            "media" => ConfiancaLeitura.Media,
            "baixa" => ConfiancaLeitura.Baixa,
            _ => null
        };

        return confianca is not null;
    }
}
