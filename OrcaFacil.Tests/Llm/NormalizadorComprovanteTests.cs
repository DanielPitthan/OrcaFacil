using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Llm;

namespace OrcaFacil.Tests.Llm;

public class NormalizadorComprovanteTests
{
    [Fact]
    public void ReconhecidoFalso_ComProdutoNoCatalogo_FicaAConfirmarSemBloquear()
    {
        var catalogo = new List<ProdutoCesta> { Produto(4, "Arroz", 2) };
        var comprovante = Cupom(
            Item("ARROZ T1 5KG", "Arroz", reconhecido: false, 10m));

        var resultado = NormalizadorComprovante.Normalizar(comprovante, catalogo);
        var linha = Assert.Single(resultado.Linhas);

        Assert.True(linha.Reconhecido);
        Assert.True(linha.Revisar);
        Assert.Equal("a confirmar", linha.Confirmacao);
        Assert.False(linha.NaoIdentificado);
        Assert.False(linha.NomeNovo);
        Assert.Equal(4, linha.ProdutoCestaId);
        Assert.Equal("Arroz", linha.Nome);
        Assert.Equal("ARROZ T1 5KG", linha.DescricaoLida);
        Assert.Equal("Arroz", linha.Rotulo);
        Assert.True(linha.ProntaParaGravar);
        Assert.True(resultado.PodeGravar);
    }

    [Fact]
    public void ReconhecidoFalso_SemCasamento_PreservaADescricao()
    {
        var catalogo = new List<ProdutoCesta> { Produto(4, "Arroz", 2) };
        var comprovante = Cupom(
            Item("BISC 12345", "", reconhecido: false, 4m));

        var linha = Assert.Single(NormalizadorComprovante.Normalizar(comprovante, catalogo).Linhas);

        Assert.True(linha.NaoIdentificado);
        Assert.False(linha.Reconhecido);
        Assert.False(linha.Revisar);
        Assert.False(linha.NomeNovo);
        Assert.Null(linha.ProdutoCestaId);
        Assert.Equal("BISC 12345", linha.DescricaoLida);
        Assert.Equal("BISC 12345", linha.Nome);
        Assert.Equal("Não identificado: BISC 12345", linha.Rotulo);
        Assert.False(linha.Nome.StartsWith(NormalizadorComprovante.PrefixoNaoIdentificado));
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
        Assert.True(linhas[0].Revisar);
        Assert.Equal("a confirmar", linhas[0].Confirmacao);
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

        Assert.True(linha.Reconhecido);
        Assert.True(linha.Revisar);
        Assert.Equal("a confirmar", linha.Confirmacao);
        Assert.False(linha.NaoIdentificado);
        Assert.False(linha.NomeNovo);
        Assert.Equal(9, linha.ProdutoCestaId);
        Assert.Equal("Sabão Líquido", linha.Nome);
        Assert.Equal("SAB LIQ 500", linha.DescricaoLida);
        Assert.True(linha.ProntaParaGravar);

        const string semProduto = """
            {
              "content": [
                {
                  "type": "tool_use",
                  "name": "registrar_comprovante",
                  "input": {
                    "legivel": true,
                    "itens": [
                      { "descricao_lida": "TEXTO CRU", "preco": 1.5 }
                    ]
                  }
                }
              ]
            }
            """;

        var cru = LeitorRespostaComprovante.Ler(semProduto);
        Assert.NotNull(cru);
        Assert.False(Assert.Single(cru.Itens).Reconhecido);

        var naoIdentificado = Assert.Single(NormalizadorComprovante.Normalizar(cru, catalogo).Linhas);
        Assert.True(naoIdentificado.NaoIdentificado);
        Assert.False(naoIdentificado.Revisar);
        Assert.Equal("TEXTO CRU", naoIdentificado.Nome);
        Assert.Equal("Não identificado: TEXTO CRU", naoIdentificado.Rotulo);
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

    [Fact]
    public void NomesNovosQueSoDiferemDepoisDoCorte_NaoFundem()
    {
        var primeiro = new string('A', 80) + "1";
        var segundo = new string('A', 80) + "2";
        var comprovante = Cupom(
            Item("linha 1", primeiro, reconhecido: true, 2m),
            Item("linha 2", segundo, reconhecido: true, 3m));

        var linhas = NormalizadorComprovante.Normalizar(comprovante, []).Linhas;

        Assert.Equal(2, linhas.Count);
        Assert.Equal(2m, linhas[0].Preco);
        Assert.Equal(3m, linhas[1].Preco);
        Assert.Equal(primeiro, linhas[0].NomeLido);
        Assert.Equal(segundo, linhas[1].NomeLido);
        Assert.All(linhas, linha =>
        {
            Assert.True(linha.NomeNovo);
            Assert.True(linha.NomeExcedeLimite);
            Assert.Equal(80, linha.Nome.Length);
            Assert.Equal(new string('A', 80), linha.Nome);
            Assert.False(linha.ProntaParaGravar);
        });
    }

    [Fact]
    public void PrecoNaoPositivo_NaoEntraNaSoma()
    {
        var catalogo = new List<ProdutoCesta> { Produto(1, "Arroz", 2) };
        var resultado = NormalizadorComprovante.Normalizar(
            Cupom(
                Item("ARROZ", "Arroz", reconhecido: true, 8m),
                Item("ARROZ", "Arroz", reconhecido: true, -3m),
                Item("ARROZ", "Arroz", reconhecido: true, 0m)),
            catalogo);

        Assert.Equal(3, resultado.Linhas.Count);
        Assert.Equal(8m, resultado.SomaLinhas);
        Assert.DoesNotContain(resultado.Linhas, linha => linha.ProntaParaGravar && linha.Preco == 5m);

        var gravavel = Assert.Single(resultado.Linhas, linha => linha.ProntaParaGravar);
        Assert.Equal(8m, gravavel.Preco);
        Assert.Equal(1, gravavel.ProdutoCestaId);

        var negativos = resultado.Linhas.Where(linha => !linha.ProntaParaGravar).ToList();
        Assert.Equal(2, negativos.Count);
        Assert.Contains(negativos, linha => linha.Preco == -3m && linha.PrecoInvalido);
        Assert.Contains(negativos, linha => linha.Preco == 0m && linha.PrecoInvalido);
        Assert.All(negativos, linha =>
        {
            Assert.Equal(NormalizadorComprovante.MotivoPrecoInvalido, linha.Motivo);
            Assert.Contains(NormalizadorComprovante.MotivoPrecoInvalido, linha.Avisos);
        });
        Assert.False(resultado.PodeGravar);
    }

    [Fact]
    public void ProdutoInativo_NaoFicaGravavel()
    {
        var catalogo = new List<ProdutoCesta> { Produto(1, "Arroz", 2, ativo: false) };
        var resultado = NormalizadorComprovante.Normalizar(
            Cupom(Item("ARROZ", "Arroz", reconhecido: true, 8m)),
            catalogo);
        var linha = Assert.Single(resultado.Linhas);

        Assert.Equal(1, linha.ProdutoCestaId);
        Assert.True(linha.ProdutoInativo);
        Assert.True(linha.Reconhecido);
        Assert.False(linha.NomeNovo);
        Assert.Equal(NormalizadorComprovante.MotivoProdutoInativo, linha.Motivo);
        Assert.False(linha.ProntaParaGravar);
        Assert.False(resultado.PodeGravar);
        Assert.Equal(8m, resultado.SomaLinhas);
    }

    [Fact]
    public void CatalogoAmbiguo_NaoEscolheEmSilencio()
    {
        var catalogo = new List<ProdutoCesta>
        {
            Produto(1, "Açúcar", 2),
            Produto(2, "AÇÚCAR", 3),
            Produto(3, "Arroz", 2),
            Produto(4, "arroz", 2, ativo: false)
        };

        var acucar = Assert.Single(NormalizadorComprovante.Normalizar(
            Cupom(Item("ACUCAR", "açúcar", reconhecido: true, 4m)),
            catalogo).Linhas);

        Assert.True(acucar.Ambiguo);
        Assert.Null(acucar.ProdutoCestaId);
        Assert.Null(acucar.CategoriaId);
        Assert.False(acucar.Reconhecido);
        Assert.False(acucar.NomeNovo);
        Assert.Equal(NormalizadorComprovante.MotivoAmbiguo, acucar.Motivo);
        Assert.Equal("açúcar", acucar.NomeLido);
        Assert.False(acucar.ProntaParaGravar);

        var arroz = Assert.Single(NormalizadorComprovante.Normalizar(
            Cupom(Item("ARROZ", "Arroz", reconhecido: true, 5m)),
            catalogo).Linhas);

        Assert.True(arroz.Ambiguo);
        Assert.Null(arroz.ProdutoCestaId);
        Assert.NotEqual(3, arroz.ProdutoCestaId);
        Assert.NotEqual(4, arroz.ProdutoCestaId);
        Assert.False(arroz.ProntaParaGravar);
    }

    [Fact]
    public void ProdutoForaDoCatalogo_CasaDescricaoSoSeForUnica()
    {
        var catalogo = new List<ProdutoCesta>
        {
            Produto(1, "Arroz", 2),
            Produto(2, "Açúcar", 2),
            Produto(3, "AÇÚCAR", 3)
        };

        var unico = Assert.Single(NormalizadorComprovante.Normalizar(
            Cupom(Item("Arroz", "ARROZ T1", reconhecido: true, 6m)),
            catalogo).Linhas);

        Assert.Equal(1, unico.ProdutoCestaId);
        Assert.Equal("Arroz", unico.Nome);
        Assert.True(unico.Reconhecido);
        Assert.False(unico.NomeNovo);
        Assert.False(unico.Revisar);
        Assert.True(unico.ProntaParaGravar);

        var naoUnico = Assert.Single(NormalizadorComprovante.Normalizar(
            Cupom(Item("açúcar", "Doce", reconhecido: true, 2m)),
            catalogo).Linhas);

        Assert.True(naoUnico.NomeNovo);
        Assert.False(naoUnico.Ambiguo);
        Assert.Null(naoUnico.ProdutoCestaId);
        Assert.Equal("Doce", naoUnico.Nome);
        Assert.Equal("Doce", naoUnico.NomeLido);
        Assert.False(naoUnico.ProntaParaGravar);

        var semDescricao = Assert.Single(NormalizadorComprovante.Normalizar(
            Cupom(Item("SAB LIQ", "Sabão", reconhecido: false, 3m)),
            catalogo).Linhas);

        Assert.True(semDescricao.NomeNovo);
        Assert.False(semDescricao.NaoIdentificado);
        Assert.Equal("Sabão", semDescricao.Nome);
        Assert.Null(semDescricao.ProdutoCestaId);
        Assert.False(semDescricao.ProntaParaGravar);
    }

    [Fact]
    public void NaoIdentificado_GravaNomeCortadoSemPrefixo_RotuloSoExibe()
    {
        var descricao = "NAO IDENTIFICADO " + new string('X', 80);
        var linha = Assert.Single(NormalizadorComprovante.Normalizar(
            Cupom(Item(descricao, "", reconhecido: false, 1m)),
            []).Linhas);

        Assert.True(linha.NaoIdentificado);
        Assert.Equal(descricao, linha.DescricaoLida);
        Assert.Equal(descricao[..80], linha.Nome);
        Assert.Equal(80, linha.Nome.Length);
        Assert.DoesNotContain("Não identificado", linha.Nome);
        Assert.Equal(NormalizadorComprovante.PrefixoNaoIdentificado + descricao, linha.Rotulo);
        Assert.NotEqual(linha.Rotulo, linha.Nome);
        Assert.StartsWith("Não identificado: ", linha.Rotulo);
    }

    [Fact]
    public void ItemNulo_NaoEstouraENaoPerdeOOutroItem()
    {
        var catalogo = new List<ProdutoCesta> { Produto(1, "Arroz", 2) };
        var itens = new List<ItemComprovanteLido>
        {
            Item("ARROZ", "Arroz", reconhecido: true, 8m),
            null!
        };

        var resultado = NormalizadorComprovante.Normalizar(
            new ComprovanteLido(null, null, null, true, itens),
            catalogo);

        Assert.Equal(2, resultado.Linhas.Count);
        var arroz = Assert.Single(resultado.Linhas, linha => linha.ProntaParaGravar);
        Assert.Equal(8m, arroz.Preco);
        var ausente = Assert.Single(resultado.Linhas, linha => linha.ItemAusente);
        Assert.Equal(NormalizadorComprovante.MotivoItemAusente, ausente.Motivo);
        Assert.False(ausente.ProntaParaGravar);
        Assert.Equal(8m, resultado.SomaLinhas);
        Assert.False(resultado.PodeGravar);
    }

    [Fact]
    public void SomaDecimal_EstouroViraLinhaInvalida()
    {
        var catalogo = new List<ProdutoCesta>
        {
            Produto(1, "Arroz", 2),
            Produto(2, "Feijão", 2)
        };

        var mesmaLinha = NormalizadorComprovante.Normalizar(
            Cupom(
                Item("A", "Arroz", reconhecido: true, decimal.MaxValue),
                Item("A", "Arroz", reconhecido: true, decimal.MaxValue)),
            catalogo);

        var estourada = Assert.Single(mesmaLinha.Linhas);
        Assert.True(estourada.SomaExcedida);
        Assert.Equal(NormalizadorComprovante.MotivoSomaExcedida, estourada.Motivo);
        Assert.False(estourada.ProntaParaGravar);
        Assert.False(mesmaLinha.PodeGravar);
        Assert.Equal(0m, mesmaLinha.SomaLinhas);

        var total = NormalizadorComprovante.Normalizar(
            Cupom(
                Item("A", "Arroz", reconhecido: true, decimal.MaxValue),
                Item("B", "Feijão", reconhecido: true, decimal.MaxValue)),
            catalogo);

        Assert.Equal(2, total.Linhas.Count);
        Assert.All(total.Linhas, linha => Assert.False(linha.SomaExcedida));
        Assert.True(total.SomaExcedida);
        Assert.Contains(NormalizadorComprovante.MotivoSomaExcedida, total.Aviso);
        Assert.False(total.PodeGravar);
    }

    [Fact]
    public void LeituraIncerta_SoGravaDepoisDaConfirmacao()
    {
        Assert.True(NormalizadorComprovante.ExigirConfirmacaoLeituraIncerta);

        var catalogo = new List<ProdutoCesta> { Produto(1, "Arroz", 2) };
        var ilegivel = NormalizadorComprovante.Normalizar(
            Cupom(
                [Item("ARROZ", "Arroz", reconhecido: true, 8m)],
                legivel: false),
            catalogo);

        Assert.False(ilegivel.Legivel);
        Assert.Equal(NormalizadorComprovante.AvisoLeituraIncerta, ilegivel.Aviso);
        Assert.False(ilegivel.PodeGravar);
        Assert.True(Assert.Single(ilegivel.Linhas).ProntaParaGravar);

        var confirmado = ilegivel.ConfirmarLeituraIncerta();
        Assert.True(confirmado.LeituraIncertaConfirmada);
        Assert.Null(confirmado.Aviso);
        Assert.True(confirmado.PodeGravar);

        var comPrecoRuim = NormalizadorComprovante.Normalizar(
            Cupom(
                [Item("ARROZ", "Arroz", reconhecido: true, 0m)],
                legivel: false),
            catalogo).ConfirmarLeituraIncerta();

        Assert.Null(comPrecoRuim.Aviso);
        Assert.False(comPrecoRuim.PodeGravar);
    }

    private static ProdutoCesta Produto(int id, string nome, int categoriaId, bool ativo = true) =>
        new() { Id = id, Nome = nome, CategoriaId = categoriaId, Ativo = ativo };

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
