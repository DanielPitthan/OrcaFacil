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
    [InlineData("""{"content":[{"type":"tool_use","name":"registrar_comprovante","input":{"legivel":true,"itens":[{"descricao_lida":"A","preco":"1"}]}}]}""")]
    [InlineData("""{"content":[{"type":"tool_use","name":"registrar_comprovante","input":{"legivel":true,"data":"18/10/2026","itens":[]}}]}""")]
    [InlineData("""{"content":[{"type":"tool_use","name":"registrar_comprovante","input":{"legivel":true,"itens":[{"descricao_lida":"A","preco":1,"confianca":"altissima"}]}}]}""")]
    [InlineData("""{"content":[{"type":"tool_use","name":"registrar_comprovante","input":{"legivel":true,"itens":[{"descricao_lida":"A","preco":1,"reconhecido":"false"}]}}]}""")]
    public void ForaDoSchema_DevolveNulo(string? json)
    {
        Assert.Null(LeitorRespostaComprovante.Ler(json));
    }
}
