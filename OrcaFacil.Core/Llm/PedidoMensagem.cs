using System.Text.Json.Nodes;

namespace OrcaFacil.Core.Llm;

/// <summary>
/// Monta o JSON de <c>POST {base}/v1/messages</c> (Anthropic Messages).
/// <c>system</c> fica no topo. Não envia <c>temperature</c> nem <c>top_p</c>.
/// </summary>
public static class PedidoMensagem
{
    public const string ModeloAnthropic = "claude-haiku-5-5";
    public const string ModeloDeepSeek = "deepseek-flash";

    /// <summary>
    /// Modelo padrão do chip Alibaba na doc Anthropic-compatible da QwenCloud.
    /// O pedido segue com ferramenta. Este id não aceita esquema JSON de saída.
    /// </summary>
    public const string ModeloAlibaba = "deepseek-v4.1-flash-1017";

    public const string NomeFerramenta = "registrar_comprovante";

    /// <summary>
    /// Padrão do comprovante. O thinking do modelo consome esse orçamento;
    /// 4096 cortava a resposta com <c>stop_reason max_tokens</c>.
    /// </summary>
    public const int MaxTokens = 8192;

    public const int MaxTokensMinimo = 256;
    public const int MaxTokensMaximo = 32768;

    public static string TextoMaxTokensForaDaFaixa =>
        $"O máximo de tokens fica entre {MaxTokensMinimo} e {MaxTokensMaximo}.";

    public static bool MaxTokensNaFaixa(int valor) =>
        valor >= MaxTokensMinimo && valor <= MaxTokensMaximo;

    /// <summary>Valor fora da faixa volta ao padrão. Não grava número que a API recusaria.</summary>
    public static int NormalizarMaxTokens(int valor) =>
        MaxTokensNaFaixa(valor) ? valor : MaxTokens;

    public const string Sistema =
        "Você lê a foto de um comprovante de compra e registra apenas o que está visível, " +
        "pela ferramenta registrar_comprovante. Não invente loja, data, itens ou preços. " +
        "descricao_lida é o texto cru da linha. produto fica vazio e reconhecido fica falso " +
        "quando o produto não for reconhecível. preco é o total da linha em reais, já com o " +
        "desconto aplicado. quantidade e unidade são só apoio e não substituem o preço. " +
        "data, se aparecer, usa yyyy-MM-dd. Se o comprovante não puder ser lido, legivel " +
        "fica falso e itens fica uma lista vazia. O catálogo, quando vier, está em linhas id|nome. " +
        "produto_id é o id de uma dessas linhas, ou null se nenhuma corresponder. Não invente id. " +
        "confianca é um número de 0 a 1 para o casamento da linha. pendente fica verdadeiro quando " +
        "a confiança for menor que 0.8 ou quando não houver correspondência, e só destaca a linha.";

    private const string InstrucaoUsuario =
        "Leia o comprovante da imagem e registre os dados com a ferramenta.";

    private const string Esquema = """
        {
          "type": "object",
          "properties": {
            "loja": {
              "type": "string",
              "description": "Nome da loja, se estiver visível."
            },
            "data": {
              "type": "string",
              "description": "Data da compra no formato yyyy-MM-dd, se estiver visível."
            },
            "total_cupom": {
              "type": "number",
              "description": "Total impresso no cupom, em reais."
            },
            "legivel": {
              "type": "boolean",
              "description": "Falso quando o comprovante não puder ser lido."
            },
            "itens": {
              "type": "array",
              "description": "Linhas do comprovante. Pode ser vazia.",
              "items": {
                "type": "object",
                "properties": {
                  "descricao_lida": {
                    "type": "string",
                    "description": "Texto cru da linha, como impresso."
                  },
                  "produto": {
                    "type": "string",
                    "description": "Nome do produto reconhecido. Vazio se não reconhecer."
                  },
                  "reconhecido": {
                    "type": "boolean",
                    "description": "Verdadeiro somente quando o produto foi reconhecido."
                  },
                  "produto_id": {
                    "type": ["integer", "null"],
                    "description": "Id do catálogo na linha id|nome. Null quando não houver correspondência."
                  },
                  "preco": {
                    "type": "number",
                    "description": "Total da linha em reais, já com desconto."
                  },
                  "quantidade": {
                    "type": "string",
                    "description": "Apoio de leitura. Não vai para o banco."
                  },
                  "unidade": {
                    "type": "string",
                    "description": "Apoio de leitura. Não vai para o banco."
                  },
                  "desconto": {
                    "type": "number",
                    "description": "Desconto da linha em reais, se houver."
                  },
                  "confianca": {
                    "type": "number",
                    "minimum": 0,
                    "maximum": 1,
                    "description": "Confiança do casamento com o catálogo, de 0 a 1."
                  },
                  "pendente": {
                    "type": "boolean",
                    "description": "Verdadeiro quando a confiança for menor que 0.8 ou não houver correspondência. Só destaca a linha."
                  }
                },
                "required": ["descricao_lida", "produto", "reconhecido", "preco"]
              }
            }
          },
          "required": ["legivel", "itens"]
        }
        """;

    public static Uri? CriarUri(string? urlBase) => CriarUri(urlBase, permitirHttp: false);

    internal static Uri? CriarUri(string? urlBase, bool permitirHttp)
    {
        if (string.IsNullOrWhiteSpace(urlBase))
            return null;

        if (!Uri.TryCreate(urlBase.Trim(), UriKind.Absolute, out var uri))
            return null;

        if (uri.Scheme != Uri.UriSchemeHttps && !(permitirHttp && uri.Scheme == Uri.UriSchemeHttp))
            return null;

        if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            return null;

        var prefixo = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return new Uri(prefixo + "/v1/messages", UriKind.Absolute);
    }

    public static string Montar(
        string modelo,
        string tipoMidia,
        string imagemBase64,
        int maxTokens = MaxTokens,
        ModoThinking thinking = ModoThinking.NaoEnviar,
        string? catalogo = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelo);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoMidia);
        ArgumentException.ThrowIfNullOrWhiteSpace(imagemBase64);
        if (!MaxTokensNaFaixa(maxTokens))
            throw new ArgumentOutOfRangeException(nameof(maxTokens));

        var esquema = JsonNode.Parse(Esquema) as JsonObject
            ?? throw new InvalidOperationException("Esquema do comprovante inválido.");

        var raiz = new JsonObject
        {
            ["system"] = Sistema,
            ["model"] = modelo,
            ["max_tokens"] = maxTokens,
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "image",
                            ["source"] = new JsonObject
                            {
                                ["type"] = "base64",
                                ["media_type"] = tipoMidia,
                                ["data"] = imagemBase64
                            }
                        },
                        new JsonObject
                        {
                            ["type"] = "text",
                            ["text"] = TextoUsuario(catalogo)
                        }
                    }
                }
            },
            ["tools"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = NomeFerramenta,
                    ["description"] = "Registra a leitura de um comprovante de compra.",
                    ["input_schema"] = esquema
                }
            },
            ["tool_choice"] = new JsonObject
            {
                ["type"] = "tool",
                ["name"] = NomeFerramenta
            }
        };

        AplicarThinking(raiz, thinking);
        return raiz.ToJsonString();
    }

    /// <summary>Catálogo em linhas <c>id|nome</c>, depois da instrução. Vazio não entra no texto.</summary>
    private static string TextoUsuario(string? catalogo)
    {
        if (string.IsNullOrWhiteSpace(catalogo))
            return InstrucaoUsuario;

        return InstrucaoUsuario + "\n\nCatálogo (id|nome):\n" + catalogo.Trim();
    }

    /// <summary>
    /// Pedido mínimo de <c>POST /v1/messages</c> para testar a conexão.
    /// Sem imagem, sem ferramenta e sem a chave.
    /// </summary>
    public static string MontarTesteConexao(string modelo, ModoThinking thinking = ModoThinking.NaoEnviar)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelo);

        var raiz = new JsonObject
        {
            ["model"] = modelo.Trim(),
            ["max_tokens"] = 1,
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = "ping"
                }
            }
        };

        AplicarThinking(raiz, thinking);
        return raiz.ToJsonString();
    }

    /// <summary>
    /// <see cref="ModoThinking.Desligar"/> escreve <c>thinking.type=disabled</c>.
    /// <see cref="ModoThinking.NaoEnviar"/> omite a propriedade, inclusive como null.
    /// </summary>
    private static void AplicarThinking(JsonObject raiz, ModoThinking thinking)
    {
        switch (thinking)
        {
            case ModoThinking.NaoEnviar:
                return;
            case ModoThinking.Desligar:
                raiz["thinking"] = new JsonObject
                {
                    ["type"] = "disabled"
                };
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(thinking));
        }
    }
}
