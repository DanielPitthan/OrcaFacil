using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Llm;

namespace OrcaFacil.Tests.Llm;

public class CasamentoComprovanteTests
{
    [Fact]
    public void ComMatch_DevolveProdutoIdConfiancaENaoFicaPendente()
    {
        var catalogo = Catalogo();
        var item = Lido("ARROZ TIPO 1", produtoId: 4, informado: true, confianca: 0.91m);

        var saida = CasamentoComprovante.Casar(item, catalogo);
        var linha = Assert.Single(NormalizadorComprovante.Normalizar(Cupom(item), catalogo).Linhas);

        Assert.Equal(4, saida.ProdutoId);
        Assert.Equal(0.91m, saida.Confianca);
        Assert.False(saida.Pendente);
        Assert.False(saida.NaoIdentificado);
        Assert.False(saida.Confirmado);
        Assert.Equal(4, linha.ProdutoCestaId);
        Assert.Equal("Arroz", linha.Nome);
        Assert.False(linha.NaoIdentificado);
    }

    [Fact]
    public void SemMatch_VoltaNaoIdentificadoEPendente()
    {
        var catalogo = Catalogo();
        var item = Lido("ARROZ TIPO 1", produtoId: null, informado: true, confianca: 0.95m);

        var saida = CasamentoComprovante.Casar(item, catalogo);
        var linha = Assert.Single(NormalizadorComprovante.Normalizar(Cupom(item), catalogo).Linhas);

        Assert.Null(saida.ProdutoId);
        Assert.Equal(0.95m, saida.Confianca);
        Assert.True(saida.Pendente);
        Assert.True(saida.NaoIdentificado);
        Assert.True(linha.NaoIdentificado);
        Assert.Null(linha.ProdutoCestaId);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(0)]
    [InlineData(-3)]
    public void IdInvalido_ViraNullENaoIdentificado(int id)
    {
        var catalogo = Catalogo();
        var item = Lido("ARROZ TIPO 1", produtoId: id, informado: true, confianca: 0.99m);

        var saida = CasamentoComprovante.Casar(item, catalogo);
        var linha = Assert.Single(NormalizadorComprovante.Normalizar(Cupom(item), catalogo).Linhas);

        Assert.Null(saida.ProdutoId);
        Assert.True(saida.Pendente);
        Assert.True(saida.NaoIdentificado);
        Assert.True(linha.NaoIdentificado);
        Assert.Null(linha.ProdutoCestaId);
        Assert.DoesNotContain("Arroz", linha.Nome, StringComparison.Ordinal);
    }

    [Fact]
    public void LimiteECorte_Padrao300MandaOsMaisUsados()
    {
        Assert.Equal(300, CatalogoPedidoComprovante.LimitePadrao);
        Assert.Equal(300, RegraCatalogoComprovante.LimitePadrao);
        Assert.Equal(300, CatalogoPedidoComprovante.NormalizarLimite(null));
        Assert.Equal(300, CatalogoPedidoComprovante.NormalizarLimite(0));
        Assert.Equal(300, CatalogoPedidoComprovante.NormalizarLimite(-1));
        Assert.Equal(2, CatalogoPedidoComprovante.NormalizarLimite(2));

        var produtos = Enumerable.Range(1, 301)
            .Select(id => new ProdutoCesta { Id = id, Nome = "P" + id, CategoriaId = 1 })
            .ToList();
        var usos = new Dictionary<int, int> { [301] = 5, [2] = 9 };
        var noTeto = CatalogoPedidoComprovante.Selecionar(produtos, usos, CatalogoPedidoComprovante.LimitePadrao);

        Assert.Equal(300, noTeto.Count);
        Assert.Equal(2, noTeto[0].Id);
        Assert.Equal(301, noTeto[1].Id);
        Assert.DoesNotContain(noTeto, produto => produto.Id == 300);

        var corte = CatalogoPedidoComprovante.Selecionar(produtos, usos, 2);
        Assert.Equal([2, 301], corte.Select(produto => produto.Id).ToArray());
        Assert.Equal("2|P2\n301|P301", CatalogoPedidoComprovante.Texto(corte));
    }

    [Fact]
    public async Task LimiteDaTabela_SubstituiOPadrao()
    {
        await using var conexao = new SqliteConnection("Data Source=:memory:");
        await conexao.OpenAsync();
        var opcoes = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conexao).Options;
        await using var ctx = new AppDbContext(opcoes);
        await ctx.Database.MigrateAsync();

        Assert.Equal(300, await CatalogoPedidoComprovante.LimiteAsync(ctx));
        var regra = Assert.Single(ctx.RegrasCatalogoComprovante);
        Assert.Equal(300, regra.LimiteProdutos);

        regra.LimiteProdutos = 2;
        await ctx.SaveChangesAsync();
        Assert.Equal(2, await CatalogoPedidoComprovante.LimiteAsync(ctx));

        var produtos = new List<ProdutoCesta>
        {
            new() { Id = 1, Nome = "Arroz", CategoriaId = 1 },
            new() { Id = 2, Nome = "Feijão", CategoriaId = 1 },
            new() { Id = 3, Nome = "Óleo", CategoriaId = 1 }
        };
        var usos = new Dictionary<int, int> { [1] = 1, [3] = 4, [2] = 4 };
        var texto = CatalogoPedidoComprovante.Texto(
            CatalogoPedidoComprovante.Selecionar(produtos, usos, await CatalogoPedidoComprovante.LimiteAsync(ctx)));

        Assert.Equal("2|Feijão\n3|Óleo", texto);
    }

    [Theory]
    [InlineData("0.79", true)]
    [InlineData("0.8", false)]
    [InlineData("1", false)]
    [InlineData("0", true)]
    public void CorteDe08_SoDestacaAbaixoDoLimite(string texto, bool pendente)
    {
        var confianca = decimal.Parse(texto, System.Globalization.CultureInfo.InvariantCulture);
        var saida = CasamentoComprovante.Casar(Lido("ARROZ", produtoId: 4, informado: true, confianca: confianca), Catalogo());

        Assert.Equal(4, saida.ProdutoId);
        Assert.Equal(confianca, saida.Confianca);
        Assert.Equal(pendente, saida.Pendente);
        Assert.False(saida.PodeSalvar);
    }

    [Fact]
    public void SemCorrespondencia_FicaPendenteMesmoComConfiancaAlta()
    {
        var saida = CasamentoComprovante.Casar(
            Lido("DESCONHECIDO", produtoId: null, informado: true, confianca: 1m),
            Catalogo());

        Assert.Null(saida.ProdutoId);
        Assert.Equal(1m, saida.Confianca);
        Assert.True(saida.Pendente);
    }

    [Fact]
    public async Task ItemComProdutoId_ExigeConfirmacaoAntesDeSalvar()
    {
        await using var conexao = new SqliteConnection("Data Source=:memory:");
        await conexao.OpenAsync();
        var opcoes = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conexao).Options;
        await using var ctx = new AppDbContext(opcoes);
        await ctx.Database.MigrateAsync();

        var saida = CasamentoComprovante.Casar(
            Lido("ARROZ", produtoId: 4, informado: true, confianca: 0.95m),
            Catalogo());
        var outro = CasamentoComprovante.Casar(
            Lido("FEIJAO", produtoId: 8, informado: true, confianca: 0.99m),
            Catalogo());

        Assert.Equal(4, saida.ProdutoId);
        Assert.False(saida.Pendente);
        Assert.True(saida.ExigeConfirmacao);
        Assert.False(saida.PodeSalvar);
        Assert.False(CasamentoComprovante.PodeSalvar([saida]));

        var antes = await ctx.ItensCompraCesta.CountAsync();
        var excecao = Assert.Throws<InvalidOperationException>(() => CasamentoComprovante.GarantirConfirmacao([saida]));
        Assert.Equal(CasamentoComprovante.MensagemConfirmacaoObrigatoria, excecao.Message);
        Assert.Equal(antes, await ctx.ItensCompraCesta.CountAsync());

        var confirmado = saida.Confirmar();
        Assert.True(confirmado.PodeSalvar);
        Assert.Equal(4, confirmado.ProdutoId);
        Assert.False(confirmado.Pendente);
        var ainda = Assert.Throws<InvalidOperationException>(
            () => CasamentoComprovante.GarantirConfirmacao([confirmado, outro]));
        Assert.Equal(CasamentoComprovante.MensagemConfirmacaoObrigatoria, ainda.Message);
        Assert.Equal(antes, await ctx.ItensCompraCesta.CountAsync());

        CasamentoComprovante.GarantirConfirmacao([confirmado, outro.Confirmar()]);
        Assert.Equal(antes, await ctx.ItensCompraCesta.CountAsync());
        Assert.Equal(0, await ctx.ComprasCesta.CountAsync());
    }

    [Theory]
    [InlineData("https://api.anthropic.com", false, PedidoMensagem.ModeloAnthropic)]
    [InlineData("https://token-plan.maas.qwencloudapi.com/apps/anthropic", true, PedidoMensagem.ModeloAlibaba)]
    public void Pedido_AnthropicEQwen_LevamCatalogoSemEsquemaDeSaida(string url, bool qwen, string modelo)
    {
        var perfil = PerfilProvedor.DaUrl(url);
        Assert.Equal(qwen, PerfilProvedor.EhQwenCloud(new Uri(url).Host));
        Assert.Equal(qwen ? ProvedorDetectado.QwenCloud : ProvedorDetectado.Anthropic, perfil.Provedor);

        var json = PedidoMensagem.Montar(modelo, "image/jpeg", "dGVzdGU=", thinking: perfil.Thinking, catalogo: "4|Arroz\n8|Feijao");

        Assert.Contains("4|Arroz", json, StringComparison.Ordinal);
        Assert.Contains("8|Feijao", json, StringComparison.Ordinal);
        Assert.Contains("\"produto_id\"", json, StringComparison.Ordinal);
        Assert.Contains("\"pendente\"", json, StringComparison.Ordinal);
        Assert.Contains("\"confianca\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("output_config", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"enum\"", json, StringComparison.Ordinal);
        Assert.Equal(qwen, json.Contains("\"thinking\"", StringComparison.Ordinal));
        if (qwen)
            Assert.Contains("\"disabled\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Leitor_NumeroEId_ChegamNoContrato()
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
                      {
                        "descricao_lida": "ARROZ TIPO 1",
                        "produto": "Arroz",
                        "reconhecido": true,
                        "produto_id": 4,
                        "preco": 10,
                        "confianca": 0.86,
                        "pendente": false
                      }
                    ]
                  }
                }
              ]
            }
            """;

        var comprovante = LeitorRespostaComprovante.Ler(json);
        Assert.NotNull(comprovante);
        var saida = Assert.Single(CasamentoComprovante.Casar(comprovante!, Catalogo()));
        Assert.Equal(4, saida.ProdutoId);
        Assert.Equal(0.86m, saida.Confianca);
        Assert.False(saida.Pendente);
        Assert.False(saida.PodeSalvar);
    }

    private static List<ProdutoCesta> Catalogo() =>
    [
        new() { Id = 4, Nome = "Arroz", CategoriaId = 2, Ativo = true },
        new() { Id = 8, Nome = "Feijão", CategoriaId = 2, Ativo = true }
    ];

    private static ComprovanteLido Cupom(ItemComprovanteLido item) =>
        new(null, null, null, true, [item]);

    private static ItemComprovanteLido Lido(string descricao, int? produtoId, bool informado, decimal confianca) =>
        new(descricao, "Arroz", true, 10m, null, null, null, null, produtoId, informado, confianca);
}
