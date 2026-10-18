using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Llm;

namespace OrcaFacil.Tests.Llm;

public class NormalizadorComprovanteTests
{
    [Fact]
    public void ReconhecidoFalso_PreservaADescricaoLida()
    {
        var catalogo = new List<ProdutoCesta> { Produto(4, "Arroz", 2) };
        var comprovante = Cupom(
            Item("ARROZ T1 5KG", "Arroz", reconhecido: false, 10m));

        var linha = Assert.Single(NormalizadorComprovante.Normalizar(comprovante, catalogo).Linhas);

        Assert.True(linha.NaoIdentificado);
        Assert.False(linha.Reconhecido);
        Assert.False(linha.NomeNovo);
        Assert.Null(linha.ProdutoCestaId);
        Assert.Equal("ARROZ T1 5KG", linha.DescricaoLida);
        Assert.Equal("Não identificado: ARROZ T1 5KG", linha.Rotulo);
        Assert.False(linha.ProntaParaGravar);
    }

    [Fact]
    public void DescricaoQueCasaNoCatalogo_ViraReconhecida()
    {
        var catalogo = new List<ProdutoCesta>
        {
            Produto(4, "Arroz", 2),
            Produto(5, "Açúcar", 2)
        };
        var comprovante = Cupom(
            Item("  arroz ", "", reconhecido: false, 8.1m),
            Item("AÇÚCAR", "", reconhecido: false, 3m));

        var linhas = NormalizadorComprovante.Normalizar(comprovante, catalogo).Linhas;

        Assert.Equal(2, linhas.Count);

        Assert.True(linhas[0].Reconhecido);
        Assert.False(linhas[0].NaoIdentificado);
        Assert.False(linhas[0].NomeNovo);
        Assert.False(linhas[0].CategoriaFaltando);
        Assert.Equal(4, linhas[0].ProdutoCestaId);
        Assert.Equal(2, linhas[0].CategoriaId);
        Assert.Equal("Arroz", linhas[0].Nome);
        Assert.Equal(8.10m, linhas[0].Preco);
        Assert.True(linhas[0].ProntaParaGravar);

        Assert.Equal(5, linhas[1].ProdutoCestaId);
        Assert.Equal("Açúcar", linhas[1].Nome);
        Assert.True(linhas[1].Reconhecido);
    }

    [Fact]
    public void RespostaSemReconhecido_AssumeNaoReconhecido()
    {
        const string json = """
            {
              "content": [
                {
                  "type": "tool_use",
                  "name": "registrar_comprovante",
                  "input": {
                    "legivel": true,
                    "itens": [
                      { "descricao_lida": "SAB LIQ 500", "produto": "Sabão Líquido", "preco": 4.5 }
                    ]
                  }
                }
              ]
            }
            """;

        var lido = LeitorRespostaComprovante.Ler(json);
        Assert.NotNull(lido);
        Assert.False(Assert.Single(lido.Itens).Reconhecido);

        var catalogo = new List<ProdutoCesta> { Produto(9, "Sabão Líquido", 3) };
        var linha = Assert.Single(NormalizadorComprovante.Normalizar(lido, catalogo).Linhas);

        Assert.True(linha.NaoIdentificado);
        Assert.False(linha.Reconhecido);
        Assert.False(linha.NomeNovo);
        Assert.Null(linha.ProdutoCestaId);
        Assert.Equal("SAB LIQ 500", linha.DescricaoLida);
        Assert.Equal("Não identificado: SAB LIQ 500", linha.Rotulo);
    }

    [Fact]
    public void Duplicatas_SomaOsPrecosNumaSoLinha()
    {
        var catalogo = new List<ProdutoCesta> { Produto(1, "Arroz", 2) };
        var comprovante = Cupom(
            Item("ARROZ T1", "Arroz", reconhecido: true, 10.005m, desconto: 3m),
            Item("arroz", "arroz", reconhecido: true, 2.004m, desconto: 1m));

        var resultado = NormalizadorComprovante.Normalizar(comprovante, catalogo);
        var linha = Assert.Single(resultado.Linhas);

        Assert.Equal(1, linha.ProdutoCestaId);
        Assert.Equal("Arroz", linha.Nome);
        Assert.Equal(12.01m, linha.Preco);
        Assert.Equal(12.01m, resultado.SomaLinhas);
        Assert.True(linha.ProntaParaGravar);

        var novos = NormalizadorComprovante.Normalizar(
            Cupom(
                Item("DET", "Detergente", reconhecido: true, 3m),
                Item("DET", "detergente", reconhecido: true, 4m)),
            catalogo);
        var detergente = Assert.Single(novos.Linhas);

        Assert.True(detergente.NomeNovo);
        Assert.True(detergente.CategoriaFaltando);
        Assert.Null(detergente.ProdutoCestaId);
        Assert.Equal("Detergente", detergente.Nome);
        Assert.Equal(7m, detergente.Preco);
        Assert.False(detergente.ProntaParaGravar);
    }

    [Fact]
    public void NomeMaiorQue80_MarcaLimiteECorta()
    {
        var longo = new string('A', 81);
        var comprovante = Cupom(Item("linha crua", longo, reconhecido: true, 1.2m));

        var linha = Assert.Single(NormalizadorComprovante.Normalizar(comprovante, []).Linhas);

        Assert.True(linha.NomeNovo);
        Assert.True(linha.CategoriaFaltando);
        Assert.True(linha.NomeExcedeLimite);
        Assert.Equal(longo, linha.NomeLido);
        Assert.Equal(80, linha.Nome.Length);
        Assert.Equal(new string('A', 80), linha.Nome);
        Assert.Equal("linha crua", linha.DescricaoLida);
        Assert.False(linha.NaoIdentificado);
        Assert.False(linha.ProntaParaGravar);

        var noLimite = new string('B', 80);
        var exato = Assert.Single(NormalizadorComprovante.Normalizar(
            Cupom(Item("outra", noLimite, reconhecido: true, 1m)),
            []).Linhas);

        Assert.False(exato.NomeExcedeLimite);
        Assert.Equal(80, exato.Nome.Length);
        Assert.Equal(noLimite, exato.Nome);
    }

    [Fact]
    public void TotalDivergente_DevolveCupomESomaDasLinhas()
    {
        var catalogo = new List<ProdutoCesta>
        {
            Produto(1, "Arroz", 2),
            Produto(2, "Feijão", 2)
        };
        var itens = new[]
        {
            Item("ARROZ", "Arroz", reconhecido: true, 10.10m),
            Item("FEIJAO", "Feijão", reconhecido: true, 5.005m)
        };

        var divergente = NormalizadorComprovante.Normalizar(
            Cupom(itens, total: 15.00m, loja: "Mercado Central", data: new DateOnly(2026, 10, 18)),
            catalogo);

        Assert.Equal(15.00m, divergente.TotalCupom);
        Assert.Equal(15.11m, divergente.SomaLinhas);
        Assert.True(divergente.TotalDivergente);
        Assert.Equal("Mercado Central", divergente.Loja);
        Assert.Equal(new DateOnly(2026, 10, 18), divergente.Data);
        Assert.Equal(2, divergente.Linhas.Count);
        Assert.Equal(["Arroz", "Feijão"], divergente.Linhas.Select(linha => linha.Nome).ToArray());

        var igual = NormalizadorComprovante.Normalizar(Cupom(itens, total: 15.105m), catalogo);

        Assert.Equal(15.11m, igual.TotalCupom);
        Assert.Equal(15.11m, igual.SomaLinhas);
        Assert.False(igual.TotalDivergente);

        var semTotal = NormalizadorComprovante.Normalizar(Cupom(itens), catalogo);

        Assert.Null(semTotal.TotalCupom);
        Assert.Equal(15.11m, semTotal.SomaLinhas);
        Assert.False(semTotal.TotalDivergente);
    }

    [Fact]
    public void QuantidadeEUnidade_NaoEntramNoPrecoNemNaLinha()
    {
        var catalogo = new List<ProdutoCesta> { Produto(1, "Arroz", 2) };
        var comprovante = Cupom(
            Item("ARROZ", "Arroz", reconhecido: true, 5.5m, quantidade: "2", unidade: "kg"),
            Item("ARROZ", "Arroz", reconhecido: true, 1m, quantidade: "1", unidade: "un"));

        var resultado = NormalizadorComprovante.Normalizar(comprovante, catalogo);
        var linha = Assert.Single(resultado.Linhas);

        Assert.Equal(6.5m, linha.Preco);
        Assert.Equal(6.5m, resultado.SomaLinhas);
        Assert.Equal("2", linha.Quantidade);
        Assert.Equal("kg", linha.Unidade);
        Assert.Equal("2 kg", linha.TextoApoio);
        Assert.Equal(1, linha.ProdutoCestaId);
    }

    [Fact]
    public void NenhumItemEPerdido_ELinhaGravavelTemProduto()
    {
        var catalogo = new List<ProdutoCesta> { Produto(1, "Arroz", 2) };
        var comprovante = Cupom(
            Item("ARROZ", "Arroz", reconhecido: true, 10m),
            Item("ARROZ", "Arroz", reconhecido: true, 2m),
            Item("BISC 123", "", reconhecido: false, 3m),
            Item("", "", reconhecido: false, 0m),
            Item("SAB", "Sabão", reconhecido: true, 4m));

        var resultado = NormalizadorComprovante.Normalizar(comprovante, catalogo);

        Assert.Equal(4, resultado.Linhas.Count);
        Assert.Equal(19m, resultado.SomaLinhas);
        Assert.Contains(resultado.Linhas, linha => linha.NaoIdentificado && linha.DescricaoLida == "BISC 123");
        Assert.Contains(resultado.Linhas, linha => linha.NaoIdentificado && linha.DescricaoLida == "");
        Assert.Contains(resultado.Linhas, linha => linha.NomeNovo && linha.Nome == "Sabão");
        Assert.All(
            resultado.Linhas.Where(linha => linha.ProntaParaGravar),
            linha => Assert.True(linha.ProdutoCestaId is > 0));
        Assert.DoesNotContain(resultado.Linhas, linha => linha.ProntaParaGravar && linha.ProdutoCestaId is null);
        Assert.False(resultado.PodeGravar);

        var soCatalogo = NormalizadorComprovante.Normalizar(
            Cupom(Item("ARROZ", "Arroz", reconhecido: true, 10m)),
            catalogo);

        Assert.True(soCatalogo.PodeGravar);
        Assert.All(soCatalogo.Linhas, linha => Assert.True(linha.ProdutoCestaId is > 0));
    }

    private static ProdutoCesta Produto(int id, string nome, int categoriaId) =>
        new() { Id = id, Nome = nome, CategoriaId = categoriaId, Ativo = true };

    private static ComprovanteLido Cupom(
        params ItemComprovanteLido[] itens) =>
        new(null, null, null, true, itens);

    private static ComprovanteLido Cupom(
        IReadOnlyList<ItemComprovanteLido> itens,
        decimal? total = null,
        string? loja = null,
        DateOnly? data = null,
        bool legivel = true) =>
        new(loja, data, total, legivel, itens);

    private static ItemComprovanteLido Item(
        string descricao,
        string produto,
        bool reconhecido,
        decimal preco,
        string? quantidade = null,
        string? unidade = null,
        decimal? desconto = null) =>
        new(descricao, produto, reconhecido, preco, quantidade, unidade, desconto, null);
}
