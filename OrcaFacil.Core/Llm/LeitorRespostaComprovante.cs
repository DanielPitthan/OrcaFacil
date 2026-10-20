using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OrcaFacil.Core.Llm;

/// <summary>
/// Lê o comprovante do bloco <c>tool_use</c> ou, se ele não vier, do JSON nos blocos <c>text</c>.
/// Ignora <c>thinking</c>. Sem <c>reconhecido</c>, o item fica não reconhecido.
/// </summary>
public static class LeitorRespostaComprovante
{
    public const string MotivoJsonInvalido = "JSON inválido";
    public const string MotivoTruncada = "resposta truncada";
    public const string MotivoCortada = "A resposta foi cortada por limite de tokens.";

    private const int ObjetosMaximos = 32;
    private const int ProfundidadeMaxima = 64;
    private const int PassoCancelamento = 4096;

    private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;
    private static readonly Regex DecimalExato = new(
        @"^-?\d+(?:\.\d+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RazaoBruta = new(
        """"(stop_reason|finish_reason)"\s*:\s*"([^"]*)"""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static string MotivoCampoAusente(string campo) => "campo ausente: " + campo;

    public static string MotivoCampoInvalido(string campo) => "campo inválido: " + campo;

    public static ComprovanteLido? Ler(string? json) => Interpretar(json).Comprovante;

    public static InterpretacaoComprovante Interpretar(string? json, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(json))
            return Falha(MotivoJsonInvalido, null, null);

        var texto = json.Trim().TrimStart('\uFEFF');
        try
        {
            using var documento = JsonDocument.Parse(texto);
            return InterpretarRaiz(documento.RootElement, cancellationToken);
        }
        catch (JsonException)
        {
            var stop = ExtrairRazaoBruta(texto, "stop_reason");
            var finish = ExtrairRazaoBruta(texto, "finish_reason");
            if (string.Equals(stop, "max_tokens", StringComparison.OrdinalIgnoreCase))
                return Falha(MotivoCortada, stop, finish);

            if (!TentarRecortarObjeto(SemCerca(texto), cancellationToken, out var objeto, out var truncado))
                return Falha(truncado ? MotivoTruncada : MotivoJsonInvalido, stop, finish);

            try
            {
                using var interno = JsonDocument.Parse(objeto);
                if (interno.RootElement.ValueKind != JsonValueKind.Object)
                    return Falha(MotivoJsonInvalido, stop, finish);

                return LerComoInput(interno.RootElement.Clone(), stop, finish);
            }
            catch (JsonException)
            {
                return Falha(MotivoJsonInvalido, stop, finish);
            }
        }
    }

    private static InterpretacaoComprovante InterpretarRaiz(JsonElement raiz, CancellationToken cancellationToken)
    {
        if (raiz.ValueKind != JsonValueKind.Object)
            return Falha(MotivoJsonInvalido, null, null);

        var stop = LerTextoPropriedade(raiz, "stop_reason");
        var finish = LerTextoPropriedade(raiz, "finish_reason");
        if (string.Equals(stop, "max_tokens", StringComparison.OrdinalIgnoreCase))
            return Falha(MotivoCortada, stop, finish);

        if (!TentarPayload(raiz, cancellationToken, out var input, out var motivo))
            return Falha(motivo ?? MotivoJsonInvalido, stop, finish);

        return LerComoInput(input, stop, finish);
    }

    private static bool TentarPayload(
        JsonElement raiz,
        CancellationToken cancellationToken,
        out JsonElement input,
        out string? motivo)
    {
        input = default;
        motivo = null;

        if (!raiz.TryGetProperty("content", out var content))
        {
            if (PareceComprovante(raiz))
            {
                input = raiz;
                return true;
            }

            motivo = MotivoCampoAusente("content");
            return false;
        }

        if (content.ValueKind == JsonValueKind.String)
            return PayloadDeTexto(content.GetString(), cancellationToken, out input, out motivo);

        if (content.ValueKind != JsonValueKind.Array)
        {
            motivo = MotivoCampoInvalido("content");
            return false;
        }

        string? motivoFerramenta = null;
        foreach (var bloco in content.EnumerateArray())
        {
            if (!EhFerramenta(bloco))
                continue;

            if (!bloco.TryGetProperty("input", out var candidato) ||
                candidato.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                motivoFerramenta ??= MotivoCampoAusente("input");
                continue;
            }

            if (candidato.ValueKind == JsonValueKind.Object)
            {
                var lido = LerComoInput(candidato, null, null);
                if (lido.Comprovante is not null)
                {
                    input = candidato;
                    return true;
                }

                motivoFerramenta ??= lido.Motivo;
                continue;
            }

            if (candidato.ValueKind == JsonValueKind.String)
            {
                if (!PayloadDeTexto(candidato.GetString(), cancellationToken, out var textoInput, out var motivoTexto))
                {
                    motivoFerramenta ??= motivoTexto;
                    continue;
                }

                var lido = LerComoInput(textoInput, null, null);
                if (lido.Comprovante is not null)
                {
                    input = textoInput;
                    return true;
                }

                motivoFerramenta ??= lido.Motivo;
                continue;
            }

            motivoFerramenta ??= MotivoCampoInvalido("input");
        }

        var texto = JuntarTextos(content);
        if (texto is null)
        {
            motivo = motivoFerramenta ?? MotivoCampoAusente("content");
            return false;
        }

        if (!PayloadDeTexto(texto, cancellationToken, out input, out var motivoDoTexto))
        {
            motivo = motivoFerramenta ?? motivoDoTexto;
            return false;
        }

        if (EntradaValida(input))
            return true;

        if (motivoFerramenta is not null)
        {
            motivo = motivoFerramenta;
            return false;
        }

        motivo = LerComoInput(input, null, null).Motivo ?? motivoDoTexto;
        return false;
    }

    private static bool EntradaValida(JsonElement candidato) =>
        LerComoInput(candidato, null, null).Comprovante is not null;

    private static bool PareceComprovante(JsonElement raiz) =>
        raiz.TryGetProperty("legivel", out _) || raiz.TryGetProperty("itens", out _);

    private static string? JuntarTextos(JsonElement blocos)
    {
        var texto = new StringBuilder();
        var achou = false;
        foreach (var bloco in blocos.EnumerateArray())
        {
            if (bloco.ValueKind != JsonValueKind.Object)
                continue;

            if (!bloco.TryGetProperty("type", out var tipo) || tipo.ValueKind != JsonValueKind.String)
                continue;

            if (!string.Equals(tipo.GetString(), "text", StringComparison.Ordinal))
                continue;

            achou = true;
            if (!bloco.TryGetProperty("text", out var trecho) || trecho.ValueKind != JsonValueKind.String)
                continue;

            if (texto.Length > 0)
                texto.Append('\n');

            texto.Append(trecho.GetString());
        }

        return achou ? texto.ToString() : null;
    }

    private static bool PayloadDeTexto(
        string? texto,
        CancellationToken cancellationToken,
        out JsonElement input,
        out string? motivo)
    {
        input = default;
        motivo = null;
        if (string.IsNullOrWhiteSpace(texto))
        {
            motivo = MotivoJsonInvalido;
            return false;
        }

        if (!TentarRecortarObjeto(SemCerca(texto), cancellationToken, out var json, out var truncado))
        {
            motivo = truncado ? MotivoTruncada : MotivoJsonInvalido;
            return false;
        }

        try
        {
            using var documento = JsonDocument.Parse(json);
            if (documento.RootElement.ValueKind != JsonValueKind.Object)
            {
                motivo = MotivoJsonInvalido;
                return false;
            }

            input = documento.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            motivo = MotivoJsonInvalido;
            return false;
        }
    }

    private static string SemCerca(string texto)
    {
        var inicio = texto.IndexOf("```", StringComparison.Ordinal);
        if (inicio < 0)
            return texto;

        var depois = inicio + 3;
        var quebra = texto.IndexOf('\n', depois);
        var conteudo = quebra < 0 ? depois : quebra + 1;
        var fim = texto.IndexOf("```", conteudo, StringComparison.Ordinal);
        return fim < 0 ? texto[conteudo..] : texto[conteudo..fim];
    }

    /// <summary>
    /// Uma passagem. Só ocupa vaga no teto o objeto cujo <c>itens</c> é um array e que passa na validação.
    /// O último válido vence. Se nenhum passar, fica o último array.
    /// </summary>
    private static bool TentarRecortarObjeto(
        string texto,
        CancellationToken cancellationToken,
        out string json,
        out bool truncado)
    {
        json = string.Empty;
        truncado = false;
        string? ultimoValido = null;
        string? ultimoCandidato = null;
        var busca = 0;
        var vistos = 0;
        while (busca < texto.Length && vistos < ObjetosMaximos)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var inicio = texto.IndexOf('{', busca);
            if (inicio < 0)
                break;

            if (!TentarFecharObjeto(texto, inicio, cancellationToken, out var fim))
            {
                truncado = true;
                break;
            }

            var candidato = texto[inicio..(fim + 1)];
            busca = fim + 1;
            if (!ClassificarObjeto(candidato, out var valido))
                continue;

            if (!valido)
            {
                ultimoCandidato = candidato;
                continue;
            }

            vistos++;
            ultimoValido = candidato;
        }

        var escolhido = ultimoValido ?? ultimoCandidato;
        if (escolhido is null)
            return false;

        json = escolhido;
        truncado = false;
        return true;
    }

    private static bool ClassificarObjeto(string json, out bool valido)
    {
        valido = false;
        try
        {
            using var documento = JsonDocument.Parse(json);
            if (documento.RootElement.ValueKind != JsonValueKind.Object ||
                !documento.RootElement.TryGetProperty("itens", out var itens) ||
                itens.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            valido = EntradaValida(documento.RootElement);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TentarFecharObjeto(
        string texto,
        int inicio,
        CancellationToken cancellationToken,
        out int fim)
    {
        fim = -1;
        var profundidade = 0;
        var emString = false;
        var escape = false;
        for (var i = inicio; i < texto.Length; i++)
        {
            if (((i - inicio) & (PassoCancelamento - 1)) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            var caractere = texto[i];
            if (emString)
            {
                if (escape)
                {
                    escape = false;
                    continue;
                }

                if (caractere == '\\')
                {
                    escape = true;
                    continue;
                }

                if (caractere == '"')
                    emString = false;

                continue;
            }

            if (caractere == '"')
            {
                emString = true;
                continue;
            }

            if (caractere == '{')
            {
                profundidade++;
                if (profundidade > ProfundidadeMaxima)
                    return false;
            }
            else if (caractere == '}')
            {
                profundidade--;
                if (profundidade == 0)
                {
                    fim = i;
                    return true;
                }
            }
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

    private static InterpretacaoComprovante LerComoInput(JsonElement input, string? stop, string? finish)
    {
        if (input.ValueKind != JsonValueKind.Object)
            return Falha(MotivoJsonInvalido, stop, finish);

        var falha = new MotivoCaixa();
        if (!TextoOpcional(input, "loja", falha, out var loja) ||
            !DataOpcional(input, falha, out var data) ||
            !DecimalOpcional(input, "total_cupom", falha, out var total) ||
            !BoolObrigatorio(input, "legivel", falha, out var legivel) ||
            !LerItens(input, falha, out var itens))
        {
            return Falha(falha.Texto ?? MotivoJsonInvalido, stop, finish);
        }

        return new InterpretacaoComprovante(new ComprovanteLido(loja, data, total, legivel, itens), null, stop, finish);
    }

    private static bool LerItens(JsonElement input, MotivoCaixa falha, out List<ItemComprovanteLido> itens)
    {
        itens = [];
        if (!input.TryGetProperty("itens", out var lista) ||
            lista.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return falha.Recusar(MotivoCampoAusente("itens"));
        }

        if (lista.ValueKind != JsonValueKind.Array)
            return falha.Recusar(MotivoCampoInvalido("itens"));

        var indice = 0;
        foreach (var item in lista.EnumerateArray())
        {
            if (!LerItem(item, indice, falha, out var lido) || lido is null)
                return false;

            itens.Add(lido);
            indice++;
        }

        return true;
    }

    private static bool LerItem(JsonElement item, int indice, MotivoCaixa falha, out ItemComprovanteLido? lido)
    {
        lido = null;
        var prefixo = "itens[" + indice.ToString(Invariante) + "].";
        if (item.ValueKind != JsonValueKind.Object)
            return falha.Recusar(MotivoCampoInvalido(prefixo.TrimEnd('.')));

        if (!TextoObrigatorio(item, "descricao_lida", prefixo, falha, out var descricao) ||
            !Produto(item, prefixo, falha, out var produto) ||
            !Reconhecido(item, prefixo, falha, out var reconhecido) ||
            !DecimalObrigatorio(item, "preco", prefixo, falha, out var preco) ||
            !TextoOpcional(item, "quantidade", falha, out var quantidade, prefixo) ||
            !TextoOpcional(item, "unidade", falha, out var unidade, prefixo) ||
            !DecimalOpcional(item, "desconto", falha, out var desconto, prefixo) ||
            !Confianca(item, prefixo, falha, out var confianca))
        {
            return false;
        }

        lido = new ItemComprovanteLido(
            descricao, produto, reconhecido, preco, quantidade, unidade, desconto, confianca);
        return true;
    }

    private static bool Ausente(JsonElement objeto, string nome) =>
        !objeto.TryGetProperty(nome, out var valor) ||
        valor.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;

    private static bool TextoObrigatorio(
        JsonElement objeto,
        string nome,
        string prefixo,
        MotivoCaixa falha,
        out string texto)
    {
        texto = string.Empty;
        if (Ausente(objeto, nome))
            return falha.Recusar(MotivoCampoAusente(prefixo + nome));

        if (objeto.GetProperty(nome).ValueKind != JsonValueKind.String)
            return falha.Recusar(MotivoCampoInvalido(prefixo + nome));

        texto = objeto.GetProperty(nome).GetString() ?? string.Empty;
        return true;
    }

    private static bool TextoOpcional(
        JsonElement objeto,
        string nome,
        MotivoCaixa falha,
        out string? texto,
        string prefixo = "")
    {
        texto = null;
        if (Ausente(objeto, nome))
            return true;

        if (objeto.GetProperty(nome).ValueKind != JsonValueKind.String)
            return falha.Recusar(MotivoCampoInvalido(prefixo + nome));

        texto = objeto.GetProperty(nome).GetString();
        return true;
    }

    private static bool Produto(JsonElement objeto, string prefixo, MotivoCaixa falha, out string produto)
    {
        produto = string.Empty;
        if (Ausente(objeto, "produto"))
            return true;

        if (objeto.GetProperty("produto").ValueKind != JsonValueKind.String)
            return falha.Recusar(MotivoCampoInvalido(prefixo + "produto"));

        produto = objeto.GetProperty("produto").GetString() ?? string.Empty;
        return true;
    }

    private static bool Reconhecido(JsonElement objeto, string prefixo, MotivoCaixa falha, out bool reconhecido)
    {
        reconhecido = false;
        if (Ausente(objeto, "reconhecido"))
            return true;

        var valor = objeto.GetProperty("reconhecido");
        if (valor.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            return falha.Recusar(MotivoCampoInvalido(prefixo + "reconhecido"));

        reconhecido = valor.GetBoolean();
        return true;
    }

    private static bool BoolObrigatorio(JsonElement objeto, string nome, MotivoCaixa falha, out bool valor)
    {
        valor = false;
        if (Ausente(objeto, nome))
            return falha.Recusar(MotivoCampoAusente(nome));

        var elemento = objeto.GetProperty(nome);
        if (elemento.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            return falha.Recusar(MotivoCampoInvalido(nome));

        valor = elemento.GetBoolean();
        return true;
    }

    private static bool DecimalObrigatorio(
        JsonElement objeto,
        string nome,
        string prefixo,
        MotivoCaixa falha,
        out decimal numero)
    {
        numero = 0;
        if (Ausente(objeto, nome))
            return falha.Recusar(MotivoCampoAusente(prefixo + nome));

        if (!TentarNumero(objeto.GetProperty(nome), out numero))
            return falha.Recusar(MotivoCampoInvalido(prefixo + nome));

        return true;
    }

    private static bool DecimalOpcional(
        JsonElement objeto,
        string nome,
        MotivoCaixa falha,
        out decimal? numero,
        string prefixo = "")
    {
        numero = null;
        if (Ausente(objeto, nome))
            return true;

        var valor = objeto.GetProperty(nome);
        if (valor.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(valor.GetString()))
            return true;

        if (!TentarNumero(valor, out var lido))
            return falha.Recusar(MotivoCampoInvalido(prefixo + nome));

        numero = lido;
        return true;
    }

    private static bool TentarNumero(JsonElement valor, out decimal numero)
    {
        numero = 0;
        if (valor.ValueKind == JsonValueKind.Number)
            return valor.TryGetDecimal(out numero);

        if (valor.ValueKind != JsonValueKind.String)
            return false;

        var texto = valor.GetString();
        return !string.IsNullOrWhiteSpace(texto) && TentarDecimalTexto(texto, out numero);
    }

    private static bool TentarDecimalTexto(string texto, out decimal numero)
    {
        numero = 0;
        texto = texto.Trim().Replace(" ", "", StringComparison.Ordinal).Replace("\u00a0", "", StringComparison.Ordinal);
        if (texto.StartsWith("R$", StringComparison.OrdinalIgnoreCase))
            texto = texto[2..];

        var virgula = texto.LastIndexOf(',');
        var ponto = texto.LastIndexOf('.');
        if (virgula >= 0 && ponto >= 0)
        {
            texto = virgula > ponto
                ? texto.Replace(".", "", StringComparison.Ordinal).Replace(',', '.')
                : texto.Replace(",", "", StringComparison.Ordinal);
        }
        else if (virgula >= 0)
        {
            texto = texto.Replace(',', '.');
        }

        if (!DecimalExato.IsMatch(texto))
            return false;

        return decimal.TryParse(texto, NumberStyles.Number, Invariante, out numero);
    }

    private static bool DataOpcional(JsonElement objeto, MotivoCaixa falha, out DateOnly? data)
    {
        data = null;
        if (Ausente(objeto, "data"))
            return true;

        if (objeto.GetProperty("data").ValueKind != JsonValueKind.String)
            return falha.Recusar(MotivoCampoInvalido("data"));

        var texto = objeto.GetProperty("data").GetString();
        if (string.IsNullOrEmpty(texto))
            return true;

        if (!DateOnly.TryParseExact(texto, "yyyy-MM-dd", Invariante, DateTimeStyles.None, out var lida))
            return falha.Recusar(MotivoCampoInvalido("data"));

        data = lida;
        return true;
    }

    private static bool Confianca(JsonElement objeto, string prefixo, MotivoCaixa falha, out ConfiancaLeitura? confianca)
    {
        confianca = null;
        if (Ausente(objeto, "confianca"))
            return true;

        if (objeto.GetProperty("confianca").ValueKind != JsonValueKind.String)
            return falha.Recusar(MotivoCampoInvalido(prefixo + "confianca"));

        confianca = objeto.GetProperty("confianca").GetString() switch
        {
            "alta" => ConfiancaLeitura.Alta,
            "media" => ConfiancaLeitura.Media,
            "baixa" => ConfiancaLeitura.Baixa,
            _ => null
        };

        return confianca is not null || falha.Recusar(MotivoCampoInvalido(prefixo + "confianca"));
    }

    private static string? LerTextoPropriedade(JsonElement objeto, string nome)
    {
        if (!objeto.TryGetProperty(nome, out var valor) || valor.ValueKind != JsonValueKind.String)
            return null;

        return valor.GetString();
    }

    private static string? ExtrairRazaoBruta(string texto, string nome)
    {
        foreach (Match correspondencia in RazaoBruta.Matches(texto))
        {
            if (string.Equals(correspondencia.Groups[1].Value, nome, StringComparison.OrdinalIgnoreCase))
                return correspondencia.Groups[2].Value;
        }

        return null;
    }

    private static InterpretacaoComprovante Falha(string motivo, string? stop, string? finish) =>
        new(null, motivo, stop, finish);

    private sealed class MotivoCaixa
    {
        public string? Texto { get; private set; }

        public bool Recusar(string motivo)
        {
            Texto ??= motivo;
            return false;
        }
    }
}

/// <summary>Leitura do corpo. O comprovante só vem quando o motivo é nulo.</summary>
public sealed record InterpretacaoComprovante(
    ComprovanteLido? Comprovante,
    string? Motivo,
    string? StopReason,
    string? FinishReason)
{
    public override string ToString() =>
        Comprovante is null ? "InterpretacaoComprovante(falha)" : "InterpretacaoComprovante(ok)";
}
