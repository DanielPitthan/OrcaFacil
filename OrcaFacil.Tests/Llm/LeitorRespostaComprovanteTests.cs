using OrcaFacil.Core.Llm;

namespace OrcaFacil.Tests.Llm;

public class LeitorRespostaComprovanteTests
{
    [Fact]
    public void Le_AchaToolUse_IgnoraThinkingTextoEOutraFerramenta()
    {
        const string json = """
            {
              "content": [
                { "type": "thinking", "thinking": "{\"loja\":\"nao-usar\",\"legivel\":false,\"itens\":[]}" },
                { "type": "redacted_thinking", "data": "sigilo" },
                { "type": "text", "text": "vou registrar" },
                {
                  "type": "tool_use",
                  "name": "outra_ferramenta",
                  "input": { "loja": "Errado", "legivel": false, "itens": [] }
                },
                {
                  "type": "tool_use",
                  "name": "registrar_comprovante",
                  "input": {
                    "loja": "Mercado Central",
                    "data": "2026-10-18",
                    "total_cupom": 12.5,
                    "legivel": true,
                    "itens": [
                      {
                        "descricao_lida": "ARROZ TIPO 1 5KG",
                        "produto": "Arroz",
                        "reconhecido": true,
                        "preco": 10.0,
                        "quantidade": "1",
                        "unidade": "un",
                        "desconto": 0.5,
                        "confianca": "alta"
                      },
                      {
                        "descricao_lida": "ITEM SEM NOME",
                        "preco": 2.5,
                        "confianca": "baixa"
                      }
                    ]
                  }
                }
              ]
            }
            """;

        var comprovante = LeitorRespostaComprovante.Ler(json);

        Assert.NotNull(comprovante);
        Assert.Equal("Mercado Central", comprovante.Loja);
        Assert.Equal(new DateOnly(2026, 10, 18), comprovante.Data);
        Assert.Equal(12.5m, comprovante.TotalCupom);
        Assert.True(comprovante.Legivel);
        Assert.Equal(2, comprovante.Itens.Count);

        var arroz = comprovante.Itens[0];
        Assert.Equal("ARROZ TIPO 1 5KG", arroz.DescricaoLida);
        Assert.Equal("Arroz", arroz.Produto);
        Assert.True(arroz.Reconhecido);
        Assert.Equal(10.0m, arroz.Preco);
        Assert.Equal("1", arroz.Quantidade);
        Assert.Equal("un", arroz.Unidade);
        Assert.Equal(0.5m, arroz.Desconto);
        Assert.Equal(ConfiancaLeitura.Alta, arroz.Confianca);

        var semNome = comprovante.Itens[1];
        Assert.Equal("ITEM SEM NOME", semNome.DescricaoLida);
        Assert.Equal(string.Empty, semNome.Produto);
        Assert.False(semNome.Reconhecido);
        Assert.Equal(2.5m, semNome.Preco);
        Assert.Null(semNome.Quantidade);
        Assert.Null(semNome.Unidade);
        Assert.Null(semNome.Desconto);
        Assert.Equal(ConfiancaLeitura.Baixa, semNome.Confianca);
    }

    [Fact]
    public void SemReconhecido_LeComoNaoReconhecido_EAceitaListaVazia()
    {
        const string json = """
            {
              "content": [
                {
                  "type": "tool_use",
                  "name": "registrar_comprovante",
                  "input": {
                    "legivel": false,
                    "data": "",
                    "itens": [
                      { "descricao_lida": "TEXTO CRU", "produto": "", "preco": 0, "confianca": "media" }
                    ]
                  }
                }
              ]
            }
            """;

        var comprovante = LeitorRespostaComprovante.Ler(json);

        Assert.NotNull(comprovante);
        Assert.False(comprovante.Legivel);
        Assert.Null(comprovante.Loja);
        Assert.Null(comprovante.Data);
        Assert.Null(comprovante.TotalCupom);
        var item = Assert.Single(comprovante.Itens);
        Assert.False(item.Reconhecido);
        Assert.Equal(string.Empty, item.Produto);
        Assert.Equal("TEXTO CRU", item.DescricaoLida);
        Assert.Equal(0m, item.Preco);
        Assert.Equal(ConfiancaLeitura.Media, item.Confianca);
    }

    [Fact]
    public void ItensVazios_SaoValidos()
    {
        const string json = """
            {
              "content": [
                {
                  "type": "tool_use",
                  "name": "registrar_comprovante",
                  "input": { "legivel": true, "itens": [] }
                }
              ]
            }
            """;

        var comprovante = LeitorRespostaComprovante.Ler(json);

        Assert.NotNull(comprovante);
        Assert.Empty(comprovante.Itens);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nao-json")]
    [InlineData("""{"content":[{"type":"thinking","thinking":"so isso"}]}""")]
    [InlineData("""{"content":[{"type":"tool_use","name":"outra","input":{"legivel":true,"itens":[]}}]}""")]
    [InlineData("""{"content":[{"type":"tool_use","name":"registrar_comprovante"}]}""")]
    [InlineData("""{"content":[{"type":"tool_use","name":"registrar_comprovante","input":{"itens":[]}}]}""")]
    [InlineData("""{"content":[{"type":"tool_use","name":"registrar_comprovante","input":{"legivel":"true","itens":[]}}]}""")]
    [InlineData("""{"content":[{"type":"tool_use","name":"registrar_comprovante","input":{"legivel":true}}]}""")]
    [InlineData("""{"content":[{"type":"tool_use","name":"registrar_comprovante","input":{"legivel":true,"itens":[{"preco":1}]}}]}""")]
    [InlineData("""{"content":[{"type":"tool_use","name":"registrar_comprovante","input":{"legivel":true,"data":"18/10/2026","itens":[]}}]}""")]
    [InlineData("""{"content":[{"type":"tool_use","name":"registrar_comprovante","input":{"legivel":true,"itens":[{"descricao_lida":"A","preco":1,"confianca":"altissima"}]}}]}""")]
    [InlineData("""{"content":[{"type":"tool_use","name":"registrar_comprovante","input":{"legivel":true,"itens":[{"descricao_lida":"A","preco":1,"reconhecido":"false"}]}}]}""")]
    public void ForaDoSchema_DevolveNulo(string? json)
    {
        var excecao = Record.Exception(() => LeitorRespostaComprovante.Interpretar(json));

        Assert.Null(excecao);
        Assert.Null(LeitorRespostaComprovante.Ler(json));
    }

    [Fact]
    public void PrecoComoTextoOuVirgula_EntraNoItem()
    {
        const string json = """
            {
              "content": [
                {
                  "type": "tool_use",
                  "name": "registrar_comprovante",
                  "input": {
                    "legivel": true,
                    "total_cupom": "1.234,56",
                    "itens": [
                      { "descricao_lida": "A", "preco": "12,90" },
                      { "descricao_lida": "B", "preco": "3.50" }
                    ]
                  }
                }
              ]
            }
            """;

        var comprovante = LeitorRespostaComprovante.Ler(json);

        Assert.NotNull(comprovante);
        Assert.Equal(1234.56m, comprovante.TotalCupom);
        Assert.Equal(12.90m, comprovante.Itens[0].Preco);
        Assert.Equal(3.50m, comprovante.Itens[1].Preco);
    }

    [Fact]
    public void CamposNulosOpcionais_NaoInvalidamOComprovante()
    {
        const string json = """
            {
              "content": [
                {
                  "type": "tool_use",
                  "name": "registrar_comprovante",
                  "input": {
                    "loja": null,
                    "data": null,
                    "total_cupom": null,
                    "legivel": true,
                    "itens": [
                      {
                        "descricao_lida": "LEITE",
                        "produto": null,
                        "reconhecido": null,
                        "preco": 4,
                        "quantidade": null,
                        "unidade": null,
                        "desconto": null,
                        "confianca": null
                      }
                    ]
                  }
                }
              ]
            }
            """;

        var comprovante = LeitorRespostaComprovante.Ler(json);

        Assert.NotNull(comprovante);
        Assert.Null(comprovante.Loja);
        Assert.Null(comprovante.Data);
        Assert.Null(comprovante.TotalCupom);
        var item = Assert.Single(comprovante.Itens);
        Assert.Equal(string.Empty, item.Produto);
        Assert.False(item.Reconhecido);
        Assert.Null(item.Quantidade);
        Assert.Null(item.Confianca);
    }

    [Fact]
    public void CampoAusente_ExplicaOMotivoSemExcecao()
    {
        const string json = """
            {"content":[{"type":"tool_use","name":"registrar_comprovante","input":{"legivel":true,"itens":[{"descricao_lida":"A"}]}}]}
            """;

        var leitura = LeitorRespostaComprovante.Interpretar(json);

        Assert.Null(leitura.Comprovante);
        Assert.Equal("campo ausente: itens[0].preco", leitura.Motivo);
        Assert.Null(LeitorRespostaComprovante.Ler(json));
    }

    [Theory]
    [InlineData("json")]
    [InlineData("")]
    public void CercaETextoEmVolta_LeemOJsonDoBlocoText(string rotulo)
    {
        var cerca = string.IsNullOrEmpty(rotulo) ? "```" : "```" + rotulo;
        var json = """
            {
              "stop_reason": "end_turn",
              "finish_reason": "stop",
              "content": [
                { "type": "thinking", "thinking": "{\"loja\":\"nao-usar\",\"legivel\":false,\"itens\":[]}" },
                { "type": "text", "text": "vou ignorar este bloco" },
                { "type": "text", "text": "Antes da leitura.\nROTURO\n{\"loja\":\"Mercado\",\"legivel\":true,\"total_cupom\":\"12,90\",\"itens\":[{\"descricao_lida\":\"ARROZ\",\"preco\":\"12,90\"}]}\n```\nDepois." }
              ]
            }
            """.Replace("ROTURO", cerca, StringComparison.Ordinal);

        var leitura = LeitorRespostaComprovante.Interpretar(json);

        Assert.Null(leitura.Motivo);
        Assert.Equal("end_turn", leitura.StopReason);
        Assert.Equal("stop", leitura.FinishReason);
        Assert.NotNull(leitura.Comprovante);
        Assert.Equal("Mercado", leitura.Comprovante.Loja);
        Assert.Equal(12.90m, leitura.Comprovante.TotalCupom);
        Assert.Equal(12.90m, Assert.Single(leitura.Comprovante.Itens).Preco);
    }

    [Fact]
    public void ToolUse_TemPreferenciaSobreOTexto()
    {
        const string json = """
            {
              "content": [
                {
                  "type": "tool_use",
                  "name": "registrar_comprovante",
                  "input": {
                    "loja": "Ferramenta",
                    "legivel": true,
                    "itens": [ { "descricao_lida": "A", "preco": 2 } ]
                  }
                },
                {
                  "type": "text",
                  "text": "{\"loja\":\"Texto\",\"legivel\":true,\"itens\":[{\"descricao_lida\":\"B\",\"preco\":9}]}"
                }
              ]
            }
            """;

        var comprovante = LeitorRespostaComprovante.Ler(json);

        Assert.NotNull(comprovante);
        Assert.Equal("Ferramenta", comprovante.Loja);
        Assert.Equal(2m, Assert.Single(comprovante.Itens).Preco);
    }

    [Fact]
    public void SemToolUse_VenceOUltimoObjetoComItens()
    {
        const string json = """
            {
              "content": [
                {
                  "type": "text",
                  "text": "Exemplo:\n{\"loja\":\"Exemplo\",\"legivel\":true,\"itens\":[{\"descricao_lida\":\"X\",\"preco\":1}]}\nCupom:\n{\"loja\":\"Mercado\",\"legivel\":true,\"itens\":[{\"descricao_lida\":\"ARROZ\",\"preco\":\"12,90\"}]}"
                }
              ]
            }
            """;

        var comprovante = LeitorRespostaComprovante.Ler(json);

        Assert.NotNull(comprovante);
        Assert.Equal("Mercado", comprovante.Loja);
        Assert.Equal(12.90m, Assert.Single(comprovante.Itens).Preco);
    }

    [Fact]
    public void FerramentaInvalida_NaoEncerraEUsaAProxima()
    {
        const string json = """
            {
              "content": [
                {
                  "type": "tool_use",
                  "name": "registrar_comprovante",
                  "input": {
                    "legivel": true,
                    "itens": [ { "descricao_lida": "A", "preco": "12," } ]
                  }
                },
                {
                  "type": "tool_use",
                  "name": "registrar_comprovante",
                  "input": {
                    "loja": "Segunda",
                    "legivel": true,
                    "itens": [ { "descricao_lida": "ARROZ", "preco": 4 } ]
                  }
                }
              ]
            }
            """;

        var comprovante = LeitorRespostaComprovante.Ler(json);

        Assert.NotNull(comprovante);
        Assert.Equal("Segunda", comprovante.Loja);
        Assert.Equal(4m, Assert.Single(comprovante.Itens).Preco);
    }

    [Fact]
    public void FerramentaInvalida_CaiNoJsonDoTexto()
    {
        const string json = """
            {
              "content": [
                {
                  "type": "tool_use",
                  "name": "registrar_comprovante",
                  "input": {
                    "legivel": true,
                    "itens": [ { "descricao_lida": "A", "preco": "12," } ]
                  }
                },
                {
                  "type": "text",
                  "text": "{\"loja\":\"Texto\",\"legivel\":true,\"itens\":[{\"descricao_lida\":\"B\",\"preco\":9}]}"
                }
              ]
            }
            """;

        var comprovante = LeitorRespostaComprovante.Ler(json);

        Assert.NotNull(comprovante);
        Assert.Equal("Texto", comprovante.Loja);
        Assert.Equal(9m, Assert.Single(comprovante.Itens).Preco);
    }

    [Fact]
    public void SemToolUse_ItensQueNaoEArray_NaoApagaOCupomAnterior()
    {
        const string json = """
            {
              "content": [
                {
                  "type": "text",
                  "text": "{\"loja\":\"Mercado\",\"legivel\":true,\"itens\":[{\"descricao_lida\":\"ARROZ\",\"preco\":\"12,90\"}]}\n{\"itens\":\"x\"}"
                }
              ]
            }
            """;

        var comprovante = LeitorRespostaComprovante.Ler(json);

        Assert.NotNull(comprovante);
        Assert.Equal("Mercado", comprovante.Loja);
        Assert.Equal(12.90m, Assert.Single(comprovante.Itens).Preco);
    }

    [Fact]
    public void ObjetosSemItens_NaoContamNoTeto()
    {
        var ruido = string.Join(string.Empty, Enumerable.Repeat("{\"n\":1}", 40));
        var cupom = "{\"loja\":\"Mercado\",\"legivel\":true,\"itens\":[{\"descricao_lida\":\"ARROZ\",\"preco\":1}]}";
        var json = "{\"content\":[{\"type\":\"text\",\"text\":" +
            System.Text.Json.JsonSerializer.Serialize(ruido + cupom) +
            "}]}";

        var comprovante = LeitorRespostaComprovante.Ler(json);

        Assert.NotNull(comprovante);
        Assert.Equal("Mercado", comprovante.Loja);
        Assert.Equal(1m, Assert.Single(comprovante.Itens).Preco);
    }

    [Fact]
    public void ItensInvalidos_NaoOcupamAsVagasDoTeto()
    {
        var invalido = "{\"legivel\":true,\"itens\":[{\"descricao_lida\":\"A\",\"preco\":\"12,\"}]}";
        var ruido = string.Join(string.Empty, Enumerable.Repeat(invalido, 40));
        var cupom = "{\"loja\":\"Mercado\",\"legivel\":true,\"itens\":[{\"descricao_lida\":\"ARROZ\",\"preco\":1}]}";
        var json = "{\"content\":[{\"type\":\"text\",\"text\":" +
            System.Text.Json.JsonSerializer.Serialize(ruido + cupom) +
            "}]}";

        var comprovante = LeitorRespostaComprovante.Ler(json);

        Assert.NotNull(comprovante);
        Assert.Equal("Mercado", comprovante.Loja);
        Assert.Equal(1m, Assert.Single(comprovante.Itens).Preco);
    }

    [Fact]
    public void FerramentaInvalida_MantemMotivoQuandoOTextoFalha()
    {
        const string json = """
            {
              "content": [
                {
                  "type": "tool_use",
                  "name": "registrar_comprovante",
                  "input": {
                    "legivel": true,
                    "itens": [ { "descricao_lida": "A", "preco": "12," } ]
                  }
                },
                {
                  "type": "text",
                  "text": "{\"itens\":[{\"descricao_lida\":\"B\",\"preco\":1}]}"
                }
              ]
            }
            """;

        var leitura = LeitorRespostaComprovante.Interpretar(json);

        Assert.Null(leitura.Comprovante);
        Assert.Equal("campo inválido: itens[0].preco", leitura.Motivo);
    }

    [Theory]
    [InlineData("12,")]
    [InlineData("12.")]
    [InlineData("nao-e-preco")]
    public void PrecoInvalido_NaoViraNumero(string preco)
    {
        var json = """
            {
              "content": [
                {
                  "type": "tool_use",
                  "name": "registrar_comprovante",
                  "input": {
                    "legivel": true,
                    "itens": [ { "descricao_lida": "A", "preco": "PRECO" } ]
                  }
                }
              ]
            }
            """.Replace("PRECO", preco, StringComparison.Ordinal);

        var leitura = LeitorRespostaComprovante.Interpretar(json);

        Assert.Null(leitura.Comprovante);
        Assert.Equal("campo inválido: itens[0].preco", leitura.Motivo);
        Assert.Null(LeitorRespostaComprovante.Ler(json));
    }

    [Fact]
    public void Cancelado_NaoInterpreta()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var excecao = Record.Exception(() => LeitorRespostaComprovante.Interpretar(
            """{"legivel":true,"itens":[]}""",
            cts.Token));

        Assert.IsAssignableFrom<OperationCanceledException>(excecao);
    }

    [Fact]
    public void ObjetoProfundoDemais_NaoEstoura()
    {
        var json = new string('{', 80);

        var leitura = LeitorRespostaComprovante.Interpretar(json);

        Assert.Null(leitura.Comprovante);
        Assert.Equal("resposta truncada", leitura.Motivo);
    }

    [Fact]
    public void TextoAntesEDepoisDoJson_SemCerca_LeOObjeto()
    {
        const string json = """
            {
              "content": [
                { "type": "thinking", "thinking": "ignorar {\"legivel\":false,\"itens\":[]}" },
                { "type": "text", "text": "Aqui está o cupom:\n{\"loja\":null,\"legivel\":true,\"itens\":[{\"descricao_lida\":\"FEIJAO\",\"preco\":\"4,50\"}]}\nObrigado." }
              ]
            }
            """;

        var comprovante = LeitorRespostaComprovante.Ler(json);

        Assert.NotNull(comprovante);
        Assert.Null(comprovante.Loja);
        Assert.True(comprovante.Legivel);
        Assert.Equal(4.50m, Assert.Single(comprovante.Itens).Preco);
    }

    [Fact]
    public void StopReasonMaxTokens_ERespostaCortada()
    {
        const string json = """
            {
              "stop_reason": "max_tokens",
              "finish_reason": "length",
              "content": [
                {
                  "type": "tool_use",
                  "name": "registrar_comprovante",
                  "input": { "legivel": true, "itens": [] }
                }
              ]
            }
            """;

        var leitura = LeitorRespostaComprovante.Interpretar(json);

        Assert.Null(leitura.Comprovante);
        Assert.Null(LeitorRespostaComprovante.Ler(json));
        Assert.Equal("A resposta foi cortada por limite de tokens.", leitura.Motivo);
        Assert.Equal(LeitorRespostaComprovante.MotivoCortada, leitura.Motivo);
        Assert.Equal("max_tokens", leitura.StopReason);
        Assert.Equal("length", leitura.FinishReason);
    }

    [Fact]
    public void RespostaTruncada_NaoLancaExcecao()
    {
        const string json = """{"content":[{"type":"text","text":"{\"legivel\":true,\"itens\":[{\"descricao_lida\":\"ARROZ\""}]""";

        var excecao = Record.Exception(() => LeitorRespostaComprovante.Interpretar(json));
        var leitura = LeitorRespostaComprovante.Interpretar(json);

        Assert.Null(excecao);
        Assert.Null(leitura.Comprovante);
        Assert.Equal("resposta truncada", leitura.Motivo);
    }

    [Fact]
    public void JsonInvalido_TemMotivoClaro()
    {
        var leitura = LeitorRespostaComprovante.Interpretar("nao-json");

        Assert.Null(leitura.Comprovante);
        Assert.Equal("JSON inválido", leitura.Motivo);
        Assert.Equal("InterpretacaoComprovante(falha)", leitura.ToString());
    }

    [Theory]
    [InlineData("4.5")]
    [InlineData("true")]
    [InlineData("\"abc\"")]
    [InlineData("3000000000")]
    public void ProdutoId_FormaInvalida_NaoViraId(string literal)
    {
        var json = """
            {
              "content": [
                {
                  "type": "tool_use",
                  "name": "registrar_comprovante",
                  "input": {
                    "legivel": true,
                    "itens": [
                      {
                        "descricao_lida": "ARROZ",
                        "produto": "Arroz",
                        "reconhecido": true,
                        "preco": 10,
                        "produto_id": VALOR
                      }
                    ]
                  }
                }
              ]
            }
            """.Replace("VALOR", literal, StringComparison.Ordinal);

        var comprovante = LeitorRespostaComprovante.Ler(json);

        Assert.NotNull(comprovante);
        var item = Assert.Single(comprovante!.Itens);
        Assert.True(item.ProdutoIdInformado);
        Assert.Null(item.ProdutoId);
    }

    [Theory]
    [InlineData("0.81", true)]
    [InlineData("1.01", false)]
    [InlineData("-0.01", false)]
    [InlineData("1.1", false)]
    public void Confianca_ForaDeZeroAUm_RecusaOComprovante(string valor, bool aceita)
    {
        var json = """
            {
              "content": [
                {
                  "type": "tool_use",
                  "name": "registrar_comprovante",
                  "input": {
                    "legivel": true,
                    "itens": [
                      {
                        "descricao_lida": "ARROZ",
                        "produto": "Arroz",
                        "reconhecido": true,
                        "preco": 10,
                        "confianca": VALOR
                      }
                    ]
                  }
                }
              ]
            }
            """.Replace("VALOR", valor, StringComparison.Ordinal);

        var comprovante = LeitorRespostaComprovante.Ler(json);
        if (!aceita)
        {
            Assert.Null(comprovante);
            return;
        }

        Assert.NotNull(comprovante);
        Assert.Equal(
            decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture),
            Assert.Single(comprovante!.Itens).ConfiancaNumero);
    }

    [Theory]
    [InlineData("thinking")]
    [InlineData("reasoning_content")]
    [InlineData("tool_calls")]
    public void JsonSoNaFonte_ELido(string fonte)
    {
        const string cupom = """{"loja":"FonteUnica","legivel":true,"itens":[{"descricao_lida":"ARROZ","preco":4.5}]}""";
        var embutido = cupom.Replace("\"", "\\\"", StringComparison.Ordinal);
        var json = (fonte switch
        {
            "thinking" => """
                {
                  "stop_reason": "end_turn",
                  "content": [
                    { "type": "thinking", "thinking": "CUPOM" }
                  ]
                }
                """,
            "reasoning_content" => """
                {
                  "choices": [
                    {
                      "finish_reason": "stop",
                      "message": { "reasoning_content": "CUPOM" }
                    }
                  ]
                }
                """,
            _ => """
                {
                  "choices": [
                    {
                      "finish_reason": "stop",
                      "message": {
                        "reasoning_content": "{\"loja\":\"nao-usar\",\"legivel\":false,\"itens\":[]}",
                        "tool_calls": [
                          {
                            "type": "function",
                            "function": { "name": "registrar_comprovante", "arguments": "CUPOM" }
                          }
                        ]
                      }
                    }
                  ]
                }
                """,
        }).Replace("CUPOM", embutido, StringComparison.Ordinal);

        var comprovante = LeitorRespostaComprovante.Ler(json);

        Assert.NotNull(comprovante);
        Assert.Equal("FonteUnica", comprovante.Loja);
        Assert.Equal(4.5m, Assert.Single(comprovante.Itens).Preco);
    }

    [Theory]
    [InlineData("anthropic")]
    [InlineData("openai")]
    public void FinishReasonLength_ComCupomValido_ERespostaCortada(string formato)
    {
        var json = formato == "openai"
            ? """
              {
                "choices": [
                  {
                    "finish_reason": "length",
                    "message": {
                      "tool_calls": [
                        {
                          "function": {
                            "name": "registrar_comprovante",
                            "arguments": "{\"legivel\":true,\"itens\":[]}"
                          }
                        }
                      ]
                    }
                  }
                ]
              }
              """
            : """
              {
                "finish_reason": "length",
                "content": [
                  {
                    "type": "tool_use",
                    "name": "registrar_comprovante",
                    "input": { "legivel": true, "itens": [] }
                  }
                ]
              }
              """;

        var leitura = LeitorRespostaComprovante.Interpretar(json);

        Assert.Null(leitura.Comprovante);
        Assert.Null(LeitorRespostaComprovante.Ler(json));
        Assert.Equal(LeitorRespostaComprovante.MotivoCortada, leitura.Motivo);
        Assert.Equal("length", leitura.FinishReason);
        Assert.Null(leitura.StopReason);
    }

    [Theory]
    [InlineData("anthropic")]
    [InlineData("openai")]
    public void JsonInvalidoNaFerramenta_PrevaleceSobreORascunho(string formato)
    {
        const string rascunho = """{"loja":"Rascunho","legivel":true,"itens":[]}""";
        var embutido = rascunho.Replace("\"", "\\\"", StringComparison.Ordinal);
        var json = (formato == "openai"
            ? """
              {
                "choices": [
                  {
                    "finish_reason": "stop",
                    "message": {
                      "reasoning_content": "RASCUNHO",
                      "thinking": "RASCUNHO",
                      "content": [
                        { "type": "thinking", "thinking": "RASCUNHO" }
                      ],
                      "tool_calls": [
                        {
                          "type": "function",
                          "function": {
                            "name": "registrar_comprovante",
                            "arguments": "nao-json"
                          }
                        }
                      ]
                    }
                  }
                ]
              }
              """
            : """
              {
                "stop_reason": "end_turn",
                "reasoning_content": "RASCUNHO",
                "content": [
                  { "type": "thinking", "thinking": "RASCUNHO" },
                  {
                    "type": "tool_use",
                    "name": "registrar_comprovante",
                    "input": "nao-json"
                  }
                ]
              }
              """).Replace("RASCUNHO", embutido, StringComparison.Ordinal);

        var leitura = LeitorRespostaComprovante.Interpretar(json);

        Assert.Null(leitura.Comprovante);
        Assert.Null(LeitorRespostaComprovante.Ler(json));
        Assert.Equal(LeitorRespostaComprovante.MotivoJsonInvalido, leitura.Motivo);
    }

    [Fact]
    public void ThinkingEmConflitoComReasoning_FicaComOBlocoThinking()
    {
        const string thinking = """{"loja":"PeloThinking","legivel":true,"itens":[]}""";
        const string reasoning = """{"loja":"PeloReasoning","legivel":true,"itens":[]}""";
        var json = """
            {
              "content": [
                { "type": "thinking", "thinking": "THINKING" }
              ],
              "reasoning_content": "REASONING"
            }
            """
            .Replace("THINKING", thinking.Replace("\"", "\\\"", StringComparison.Ordinal), StringComparison.Ordinal)
            .Replace("REASONING", reasoning.Replace("\"", "\\\"", StringComparison.Ordinal), StringComparison.Ordinal);

        var comprovante = LeitorRespostaComprovante.Ler(json);

        Assert.NotNull(comprovante);
        Assert.Equal("PeloThinking", comprovante.Loja);
    }

    [Fact]
    public void ReasoningNaRaizSemContent_PerdeParaFerramentaInvalida()
    {
        const string rascunho = """{"loja":"Rascunho","legivel":true,"itens":[]}""";
        var json = """
            {
              "reasoning_content": "RASCUNHO",
              "choices": [
                {
                  "finish_reason": "stop",
                  "message": {
                    "tool_calls": [
                      {
                        "type": "function",
                        "function": {
                          "name": "registrar_comprovante",
                          "arguments": "nao-json"
                        }
                      }
                    ]
                  }
                }
              ]
            }
            """.Replace(
            "RASCUNHO",
            rascunho.Replace("\"", "\\\"", StringComparison.Ordinal),
            StringComparison.Ordinal);

        var leitura = LeitorRespostaComprovante.Interpretar(json);

        Assert.Null(leitura.Comprovante);
        Assert.Null(LeitorRespostaComprovante.Ler(json));
        Assert.Equal(LeitorRespostaComprovante.MotivoJsonInvalido, leitura.Motivo);
    }
}
