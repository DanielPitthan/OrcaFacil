using System.Text.Json.Nodes;

namespace OrcaFacil.Core.Llm;

/// <summary>
/// Monta o JSON de <c>POST {base}/v1/messages</c> (Anthropic Messages).
/// <c>system</c> fica no topo. Não envia <c>temperature</c> nem <c>top_p</c>.
/// </summary>
public static class PedidoMensagem
{
    public const string ModeloAnthropic = "claude-haiku-5-5";
    public const string NomeFerramenta = "registrar_comprovante";
    public const int MaxTokens = 4096;

    public const string Sistema =
        "Você lê a foto de um comprovante de compra e registra apenas o que está visível, " +
        "pela ferramenta registrar_comprovante. Não invente loja, data, itens ou preços. " +
        "descricao_lida é o texto cru da linha. produto fica vazio e reconhecido fica falso " +
        "quando o produto não for reconhecível. preco é o total da linha em reais, já com o " +
        "desconto aplicado. quantidade e unidade são só apoio e não substituem o preço. " +
        "data, se aparecer, usa yyyy-MM-dd. Se o comprovante não puder ser lido, legivel " +
        "fica falso e itens fica uma lista vazia.";

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
                    "type": "string",
                    "enum": ["alta", "media", "baixa"],
                    "description": "Confiança da leitura da linha."
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

    public static string Montar(string modelo, string tipoMidia, string imagemBase64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelo);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoMidia);
        ArgumentException.ThrowIfNullOrWhiteSpace(imagemBase64);

        var esquema = JsonNode.Parse(Esquema) as JsonObject
            ?? throw new InvalidOperationException("Esquema do comprovante inválido.");

        var raiz = new JsonObject
        {
            ["system"] = Sistema,
            ["model"] = modelo,
            ["max_tokens"] = MaxTokens,
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
                            ["text"] = InstrucaoUsuario
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

        return raiz.ToJsonString();
    }
}
