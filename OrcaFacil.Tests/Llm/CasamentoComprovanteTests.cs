using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Llm;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Core.Services;

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
    [InlineData("https://api.anthropic.com", ProvedorDetectado.Anthropic, false, PedidoMensagem.ModeloAnthropic)]
    [InlineData("https://api.deepseek.com", ProvedorDetectado.DeepSeek, true, PedidoMensagem.ModeloDeepSeek)]
    [InlineData("https://token-plan.maas.qwencloudapi.com/apps/anthropic", ProvedorDetectado.QwenCloud, true, PedidoMensagem.ModeloAlibaba)]
    public void Pedido_AnthropicDeepSeekEQwen_LevamCatalogoSemEsquemaDeSaida(
        string url,
        ProvedorDetectado provedor,
        bool thinkingDesligado,
        string modelo)
    {
        var perfil = PerfilProvedor.DaUrl(url);
        Assert.Equal(provedor == ProvedorDetectado.QwenCloud, PerfilProvedor.EhQwenCloud(new Uri(url).Host));
        Assert.Equal(provedor, perfil.Provedor);
        Assert.Equal(thinkingDesligado, perfil.Thinking == ModoThinking.Desligar);

        var json = PedidoMensagem.Montar(modelo, "image/jpeg", "dGVzdGU=", thinking: perfil.Thinking, catalogo: "4|Arroz\n8|Feijao");

        Assert.Contains("4|Arroz", json, StringComparison.Ordinal);
        Assert.Contains("8|Feijao", json, StringComparison.Ordinal);
        Assert.Contains("\"produto_id\"", json, StringComparison.Ordinal);
        Assert.Contains("\"pendente\"", json, StringComparison.Ordinal);
        Assert.Contains("\"confianca\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("output_config", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"enum\"", json, StringComparison.Ordinal);
        Assert.Contains("\"thinking\"", json, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"disabled\"", json, StringComparison.Ordinal);
        Assert.Contains("\"tool_choice\"", json, StringComparison.Ordinal);
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

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void IdNaoPositivo_PresenteNoCatalogo_ERejeitado(int id)
    {
        var catalogo = new List<ProdutoCesta>
        {
            new() { Id = id, Nome = "Arroz", CategoriaId = 2, Ativo = true },
            new() { Id = 4, Nome = "Feijão", CategoriaId = 2, Ativo = true }
        };
        var item = Lido("ARROZ TIPO 1", produtoId: id, informado: true, confianca: 0.99m);

        var saida = CasamentoComprovante.Casar(item, catalogo);
        var linha = Assert.Single(NormalizadorComprovante.Normalizar(Cupom(item), catalogo).Linhas);

        Assert.Null(saida.ProdutoId);
        Assert.True(linha.NaoIdentificado);
        Assert.Null(linha.ProdutoCestaId);
        Assert.NotEqual("Arroz", linha.Nome);
    }

    [Theory]
    [InlineData("0.81", "0.81")]
    [InlineData("1.01", "0")]
    [InlineData("-0.01", "0")]
    public void Confianca_ForaDeZeroAUm_CaiParaZero(string entrada, string esperada)
    {
        var confianca = decimal.Parse(entrada, CultureInfo.InvariantCulture);
        var saida = CasamentoComprovante.Casar(
            Lido("ARROZ", produtoId: 4, informado: true, confianca: confianca),
            Catalogo());

        Assert.Equal(decimal.Parse(esperada, CultureInfo.InvariantCulture), saida.Confianca);
    }

    [Fact]
    public void Sanitizar_TiraPipeEQuebra_ELimitaONome()
    {
        Assert.Equal(
            "4|A 99 B C",
            CatalogoPedidoComprovante.Texto([new ProdutoCesta { Id = 4, Nome = "A\n99|B | C" }]));

        Assert.Equal(80, CatalogoPedidoComprovante.TamanhoMaximoNome);
        var longo = new string('N', CatalogoPedidoComprovante.TamanhoMaximoNome + 5);
        var texto = CatalogoPedidoComprovante.Texto([new ProdutoCesta { Id = 1, Nome = longo }]);
        var nome = texto[(texto.IndexOf('|') + 1)..];
        Assert.Equal(CatalogoPedidoComprovante.TamanhoMaximoNome, nome.Length);
        Assert.Equal(new string('N', CatalogoPedidoComprovante.TamanhoMaximoNome), nome);
    }

    [Fact]
    public void Selecionar_IgnoraIdNaoPositivoENomeEmBranco()
    {
        var escolhidos = CatalogoPedidoComprovante.Selecionar(
            [
                new ProdutoCesta { Id = 0, Nome = "Zero" },
                new ProdutoCesta { Id = 9, Nome = "   " },
                new ProdutoCesta { Id = 6, Nome = "Arroz" }
            ],
            new Dictionary<int, int>(),
            CatalogoPedidoComprovante.LimitePadrao);

        Assert.Equal([6], escolhidos.Select(produto => produto.Id).ToArray());
    }

    [Theory]
    [InlineData("0.79", true)]
    [InlineData("0.8", false)]
    [InlineData(null, true)]
    public void ConfiancaBaixaNoId_MarcaRevisarEPendenteNaPrevia(string? texto, bool revisar)
    {
        decimal? confianca = texto is null
            ? null
            : decimal.Parse(texto, CultureInfo.InvariantCulture);
        var catalogo = new List<ProdutoCesta>
        {
            new()
            {
                Id = 4,
                Nome = "Arroz",
                CategoriaId = 2,
                Ativo = true,
                Categoria = new Categoria { Id = 2, Nome = "Alimentos" }
            }
        };
        var item = new ItemComprovanteLido(
            "ARROZ", "Arroz", true, 10m, null, null, null, null, 4, true, confianca);

        var normalizada = Assert.Single(NormalizadorComprovante.Normalizar(Cupom(item), catalogo).Linhas);
        Assert.Equal(revisar, normalizada.Revisar);
        Assert.Equal(4, normalizada.ProdutoCestaId);

        var previa = PreviaComprovante.Criar(
            NormalizadorComprovante.Normalizar(Cupom(item), catalogo),
            [],
            catalogo,
            new DateTime(2026, 10, 1));
        var linha = Assert.Single(previa.Linhas);
        Assert.Equal(revisar, linha.Pendente);
        Assert.Contains("confiança", linha.MarcadorCasamento, StringComparison.Ordinal);
        if (revisar)
            Assert.Contains("pendente", linha.MarcadorCasamento, StringComparison.Ordinal);
        else
            Assert.DoesNotContain("pendente", linha.MarcadorCasamento, StringComparison.Ordinal);
    }

    [Fact]
    public void ParaCompra_NomeExato_ExigeCasamentoConfirmado()
    {
        var catalogo = new List<ProdutoCesta>
        {
            new()
            {
                Id = 4,
                Nome = "Arroz",
                CategoriaId = 2,
                Ativo = true,
                Categoria = new Categoria { Id = 2, Nome = "Alimentos" }
            }
        };
        var item = new ItemComprovanteLido("pacote", "Arroz", true, 8m, null, null, null, null);
        var previa = PreviaComprovante.Criar(
            NormalizadorComprovante.Normalizar(new ComprovanteLido(null, null, null, true, [item]), catalogo),
            [],
            catalogo,
            new DateTime(2026, 10, 1));
        var linha = Assert.Single(previa.Linhas);

        Assert.Equal(4, linha.ProdutoCestaId);
        Assert.False(linha.CasadoPelaDescricao);
        Assert.True(linha.Pendente);
        Assert.Equal("pendente", linha.MarcadorCasamento);
        Assert.DoesNotContain("confiança", linha.MarcadorCasamento, StringComparison.Ordinal);
        Assert.False(linha.Marcado);
        Assert.False(linha.CasamentoConfirmado);
        Assert.False(previa.PodeConfirmar);
        Assert.Equal(CasamentoComprovante.MensagemConfirmacaoObrigatoria, previa.MensagemBloqueio);

        var excecao = Assert.Throws<InvalidOperationException>(() => previa.ParaCompra());
        Assert.Equal(CasamentoComprovante.MensagemConfirmacaoObrigatoria, excecao.Message);

        previa.ConfirmarCasamentos();
        Assert.False(linha.CasamentoConfirmado);

        linha.Marcado = true;
        previa.ConfirmarMarcados();
        Assert.True(linha.CasamentoConfirmado);
        Assert.StartsWith("confirmado", linha.MarcadorCasamento, StringComparison.Ordinal);
        var compra = previa.ParaCompra();
        Assert.Equal(4, Assert.Single(compra.Itens).ProdutoCestaId);
        var saida = Assert.Single(compra.ItensSaida);
        Assert.Equal(4, saida.ProdutoId);
        Assert.True(saida.Confirmado);
    }

    [Fact]
    public void Classificar_MesmoProduto_MantemAConfirmacao()
    {
        var catalogo = Catalogo();
        var item = new ItemComprovanteLido("ARROZ", "Arroz", true, 8m, null, null, null, null, 4, true, 0.95m);
        var previa = PreviaComprovante.Criar(
            NormalizadorComprovante.Normalizar(Cupom(item), catalogo),
            [],
            catalogo,
            new DateTime(2026, 10, 1));
        var linha = Assert.Single(previa.Linhas);
        Assert.False(linha.CasamentoConfirmado);
        linha.Marcado = true;
        previa.ConfirmarMarcados();
        Assert.True(linha.CasamentoConfirmado);

        previa.Classificar(linha, catalogo[0]);
        Assert.Equal(4, linha.ProdutoCestaId);
        Assert.True(linha.Marcado);
        Assert.True(linha.CasamentoConfirmado);
        Assert.True(Assert.Single(previa.ParaCompra().ItensSaida).Confirmado);
    }

    [Fact]
    public void Classificar_DepoisDeConfirmar_RearmaALinha()
    {
        var catalogo = Catalogo();
        var item = new ItemComprovanteLido("ARROZ", "Arroz", true, 8m, null, null, null, null, 4, true, 0.95m);
        var previa = PreviaComprovante.Criar(
            NormalizadorComprovante.Normalizar(Cupom(item), catalogo),
            [],
            catalogo,
            new DateTime(2026, 10, 1));
        var linha = Assert.Single(previa.Linhas);
        Assert.Equal(4, linha.ProdutoCestaId);
        Assert.False(linha.CasamentoConfirmado);
        linha.Marcado = true;
        previa.ConfirmarMarcados();
        Assert.True(linha.CasamentoConfirmado);

        previa.Classificar(linha, catalogo[1]);
        Assert.Equal(8, linha.ProdutoCestaId);
        Assert.False(linha.CasamentoConfirmado);
        Assert.False(linha.Marcado);
        var excecao = Assert.Throws<InvalidOperationException>(() => previa.ParaCompra());
        Assert.Equal(CasamentoComprovante.MensagemConfirmacaoObrigatoria, excecao.Message);
    }

    [Fact]
    public void MarcarTodos_Confianca079_NaoMarca()
    {
        var catalogo = Catalogo();
        var item = new ItemComprovanteLido(
            "ARROZ", "Arroz", true, 10m, null, null, null, null, 4, true, 0.79m);
        var previa = PreviaComprovante.Criar(
            NormalizadorComprovante.Normalizar(Cupom(item), catalogo),
            [],
            catalogo,
            new DateTime(2026, 10, 1));
        var linha = Assert.Single(previa.Linhas);
        var saida = Assert.Single(linha.Saidas);
        Assert.Equal(0.79m, saida.Confianca);
        Assert.True(saida.Pendente);
        Assert.True(linha.Pendente);
        Assert.False(linha.Marcado);
        Assert.False(linha.CasamentoConfirmado);

        previa.MarcarNaoPendentes();
        Assert.False(linha.Marcado);
        Assert.False(linha.CasamentoConfirmado);
        Assert.False(previa.PodeConfirmar);
    }

    [Fact]
    public void AlternarMarcacao_MarcarEDesmarcarVoltaAFalse()
    {
        var catalogo = Catalogo();
        var item = new ItemComprovanteLido(
            "ARROZ", "Arroz", true, 10m, null, null, null, null, 4, true, 0.95m);
        var previa = PreviaComprovante.Criar(
            NormalizadorComprovante.Normalizar(Cupom(item), catalogo),
            [],
            catalogo,
            new DateTime(2026, 10, 1));
        var linha = Assert.Single(previa.Linhas);
        Assert.False(linha.Pendente);
        Assert.False(linha.Marcado);
        Assert.False(previa.PodeConfirmar);

        previa.AlternarMarcacao(linha);
        Assert.True(linha.Marcado);
        Assert.True(previa.PodeConfirmar);

        previa.AlternarMarcacao(linha);
        Assert.False(linha.Marcado);
        Assert.False(previa.PodeConfirmar);
        var excecao = Assert.Throws<InvalidOperationException>(() => previa.ParaCompra());
        Assert.Equal(CasamentoComprovante.MensagemConfirmacaoObrigatoria, excecao.Message);
    }

    [Fact]
    public async Task MarcarTodos_IgnoraPendente_ESoGravaDepoisDoCheckManual()
    {
        using var factory = new TestDbContextFactory();
        var cestas = new CestaRepository(factory);
        var service = new CestaCadastroService(cestas);
        var membros = new MembroFamiliaRepository(factory);
        var membro = new MembroFamilia { Nome = "Usuário" };
        await membros.AddAsync(membro);

        int categoriaId;
        await using (var ctx = factory.CreateDbContext())
        {
            var categoria = new Categoria { Nome = "Alimentos", Icone = "A", CorHex = "#112233" };
            ctx.Categorias.Add(categoria);
            await ctx.SaveChangesAsync();
            categoriaId = categoria.Id;
        }

        var arroz = await service.SalvarProdutoAsync(new ProdutoCestaFormModel { Nome = "Arroz", CategoriaId = categoriaId });
        var feijao = await service.SalvarProdutoAsync(new ProdutoCestaFormModel { Nome = "Feijão", CategoriaId = categoriaId });
        arroz.Categoria = new Categoria { Id = categoriaId, Nome = "Alimentos" };
        feijao.Categoria = arroz.Categoria;
        var local = await service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        var catalogo = new List<ProdutoCesta> { arroz, feijao };
        var comprovante = new ComprovanteLido(
            "Mercado",
            new DateOnly(2026, 10, 5),
            null,
            true,
            [
                new ItemComprovanteLido("ARROZ", "Arroz", true, 10m, null, null, null, null, arroz.Id, true, 0.95m),
                new ItemComprovanteLido("pacote", "Feijão", true, 4m, null, null, null, null)
            ]);
        var previa = PreviaComprovante.Criar(
            NormalizadorComprovante.Normalizar(comprovante, catalogo),
            [local],
            catalogo,
            new DateTime(2026, 10, 5));
        var porId = Assert.Single(previa.Linhas, linha => linha.ProdutoCestaId == arroz.Id);
        var porNome = Assert.Single(previa.Linhas, linha => linha.ProdutoCestaId == feijao.Id);

        Assert.False(porId.Pendente);
        Assert.True(porNome.Pendente);
        Assert.Equal("pendente", porNome.MarcadorCasamento);
        Assert.Contains("não confirmado", porId.MarcadorCasamento, StringComparison.Ordinal);
        Assert.DoesNotContain("pendente", porId.MarcadorCasamento, StringComparison.Ordinal);
        Assert.All(previa.Linhas, linha =>
        {
            Assert.False(linha.Marcado);
            Assert.False(linha.CasamentoConfirmado);
        });
        Assert.False(previa.PodeConfirmar);
        Assert.Throws<InvalidOperationException>(() => previa.ParaCompra());

        previa.MarcarNaoPendentes();
        Assert.True(porId.Marcado);
        Assert.False(porId.CasamentoConfirmado);
        Assert.False(porNome.Marcado);
        Assert.False(porNome.CasamentoConfirmado);
        Assert.StartsWith("confirmado", porId.MarcadorCasamento, StringComparison.Ordinal);
        Assert.Equal("pendente", porNome.MarcadorCasamento);
        Assert.False(previa.PodeConfirmar);
        var bloqueio = Assert.Throws<InvalidOperationException>(() => previa.ParaCompra());
        Assert.Equal(CasamentoComprovante.MensagemConfirmacaoObrigatoria, bloqueio.Message);

        await using (var antes = factory.CreateDbContext())
        {
            Assert.Equal(0, await antes.ComprasCesta.CountAsync());
            Assert.Equal(0, await antes.ItensCompraCesta.CountAsync());
        }

        porNome.Marcado = true;
        Assert.True(previa.PodeConfirmar);
        previa.ConfirmarMarcados();
        Assert.True(porId.CasamentoConfirmado);
        Assert.True(porNome.CasamentoConfirmado);
        Assert.StartsWith("confirmado", porNome.MarcadorCasamento, StringComparison.Ordinal);
        var compra = previa.ParaCompra();
        Assert.Equal(2, compra.ItensSaida.Count);
        Assert.All(compra.ItensSaida, saida => Assert.True(saida.Confirmado));

        var gravada = await service.SalvarCompraAsync(compra, membro.Id);
        Assert.Equal([arroz.Id, feijao.Id], gravada.Itens.Select(item => item.ProdutoCestaId).Order().ToArray());

        await using var depois = factory.CreateDbContext();
        Assert.Equal(1, await depois.ComprasCesta.CountAsync());
        Assert.Equal(2, await depois.ItensCompraCesta.CountAsync());
    }

    [Fact]
    public async Task SalvarCompra_ProdutoIdSemConfirmacao_NaoGrava()
    {
        using var factory = new TestDbContextFactory();
        var cestas = new CestaRepository(factory);
        var service = new CestaCadastroService(cestas);
        var membros = new MembroFamiliaRepository(factory);
        var membro = new MembroFamilia { Nome = "Usuário" };
        await membros.AddAsync(membro);

        int categoriaId;
        await using (var ctx = factory.CreateDbContext())
        {
            var categoria = new Categoria { Nome = "Alimentos", Icone = "A", CorHex = "#112233" };
            ctx.Categorias.Add(categoria);
            await ctx.SaveChangesAsync();
            categoriaId = categoria.Id;
        }

        var arroz = await service.SalvarProdutoAsync(new ProdutoCestaFormModel { Nome = "Arroz", CategoriaId = categoriaId });
        var local = await service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        var saida = new ItemSaidaComprovante
        {
            DescricaoLida = "ARROZ",
            ProdutoId = arroz.Id,
            Confianca = 0.95m,
            Pendente = false
        };
        var modelo = new CompraCestaFormModel
        {
            LocalCompraId = local.Id,
            Data = new DateTime(2026, 10, 5),
            Itens = [new ItemCompraCestaFormModel { ProdutoCestaId = arroz.Id, Preco = 10m }],
            ItensSaida = [saida]
        };

        await using (var antes = factory.CreateDbContext())
        {
            Assert.Equal(0, await antes.ComprasCesta.CountAsync());
            Assert.Equal(0, await antes.ItensCompraCesta.CountAsync());
        }

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SalvarCompraAsync(modelo, membro.Id));
        Assert.Equal(CasamentoComprovante.MensagemConfirmacaoObrigatoria, excecao.Message);

        await using (var depois = factory.CreateDbContext())
        {
            Assert.Equal(0, await depois.ComprasCesta.CountAsync());
            Assert.Equal(0, await depois.ItensCompraCesta.CountAsync());
        }

        modelo.ItensSaida = [saida.Confirmar()];
        var compra = await service.SalvarCompraAsync(modelo, membro.Id);
        Assert.Equal(arroz.Id, Assert.Single(compra.Itens).ProdutoCestaId);

        await using var gravado = factory.CreateDbContext();
        Assert.Equal(1, await gravado.ComprasCesta.CountAsync());
        Assert.Equal(1, await gravado.ItensCompraCesta.CountAsync());
    }

    [Fact]
    public async Task IdForaDoRecorte_ExisteNoBanco_NaoCasa()
    {
        using var factory = new TestDbContextFactory();
        await using var ctx = factory.CreateDbContext();
        var categoria = new Categoria { Nome = "Alimentos", Icone = "A", CorHex = "#112233" };
        var local = new LocalCompra { Nome = "Mercado" };
        ctx.AddRange(categoria, local);
        await ctx.SaveChangesAsync();

        var fora = new ProdutoCesta { Nome = "Fora", CategoriaId = categoria.Id, Ativo = true };
        var dentro = new ProdutoCesta { Nome = "Dentro", CategoriaId = categoria.Id, Ativo = true };
        ctx.ProdutosCesta.AddRange(fora, dentro);
        await ctx.SaveChangesAsync();

        ctx.ComprasCesta.Add(new CompraCesta
        {
            LocalCompraId = local.Id,
            Data = new DateTime(2026, 10, 1),
            Mes = 10,
            Ano = 2026,
            Itens = [new CompraCestaItem { ProdutoCestaId = dentro.Id, Preco = 1m }]
        });
        var regra = await ctx.RegrasCatalogoComprovante.SingleAsync();
        regra.LimiteProdutos = 1;
        await ctx.SaveChangesAsync();

        var escolhidos = await new CestaRepository(factory).ProdutosCatalogoLeituraAsync();
        Assert.Equal([dentro.Id], escolhidos.Select(produto => produto.Id).ToArray());
        Assert.Contains(fora.Id, await ctx.ProdutosCesta.Select(produto => produto.Id).ToListAsync());

        var item = new ItemComprovanteLido(
            "FORA", "Fora", true, 2m, null, null, null, null, fora.Id, true, 0.95m);
        var linha = Assert.Single(
            NormalizadorComprovante.Normalizar(new ComprovanteLido(null, null, null, true, [item]), escolhidos).Linhas);
        Assert.Null(linha.ProdutoCestaId);
        Assert.True(linha.NaoIdentificado);
        Assert.NotEqual("Fora", linha.Nome);

        var todos = await ctx.ProdutosCesta.AsNoTracking().ToListAsync();
        var casada = Assert.Single(
            NormalizadorComprovante.Normalizar(new ComprovanteLido(null, null, null, true, [item]), todos).Linhas);
        Assert.Equal(fora.Id, casada.ProdutoCestaId);
    }

    [Fact]
    public void Barreira_EstaNoConfirmarNaPreviaENoServico()
    {
        var raiz = EncontrarRaiz();
        var confirmar = ExtrairBloco(
            File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Pages", "Cesta", "PreviaComprovanteDialog.razor")),
            "void Confirmar()");
        var fechar = confirmar.IndexOf("MudDialog.Close", StringComparison.Ordinal);
        var barreiraDialogo = confirmar.IndexOf("CasamentoComprovante.GarantirConfirmacao", StringComparison.Ordinal);
        var soMarcados = confirmar.IndexOf("Previa.ConfirmarMarcados()", StringComparison.Ordinal);
        Assert.True(soMarcados >= 0 && soMarcados < barreiraDialogo);
        Assert.True(barreiraDialogo >= 0 && barreiraDialogo < fechar);
        Assert.DoesNotContain("ConfirmarCasamentos()", confirmar, StringComparison.Ordinal);
        var dialog = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Pages", "Cesta", "PreviaComprovanteDialog.razor"));
        var subtitulo = ExtrairBloco(dialog, "string Subtitulo(LinhaPreviaComprovante linha)");
        Assert.DoesNotContain("MarcadorCasamento", subtitulo, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Marcar todos os itens não pendentes\"", dialog, StringComparison.Ordinal);
        Assert.Contains(">Marcar todos<", dialog, StringComparison.Ordinal);
        Assert.Contains("pendente, não confirmado", dialog, StringComparison.Ordinal);
        Assert.Contains("AriaMarcacao", dialog, StringComparison.Ordinal);
        var ficha = ExtrairBloco(dialog, "void ConfirmarSheet(");
        var classificar = ficha.IndexOf("Previa.Classificar(", StringComparison.Ordinal);
        var preco = ficha.IndexOf("Previa.AlterarPreco(", classificar, StringComparison.Ordinal);
        var marcar = ficha.IndexOf("Previa.Marcar(", preco, StringComparison.Ordinal);
        Assert.True(classificar >= 0 && preco > classificar && marcar > preco);
        var lista = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Shared", "ItemList.razor"));
        var texto = lista.IndexOf("class=\"of-lista-texto\"", StringComparison.Ordinal);
        var titulo = lista.IndexOf("of-lista-titulo", texto, StringComparison.Ordinal);
        var chip = lista.IndexOf("of-lista-chip", titulo, StringComparison.Ordinal);
        var sub = lista.IndexOf("class=\"of-lista-sub\"", chip, StringComparison.Ordinal);
        Assert.True(texto >= 0 && titulo > texto && chip > titulo && sub > chip);
        var css = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Shared", "ItemList.razor.css"));
        var regraChip = css.IndexOf(".of-lista-chip {", StringComparison.Ordinal);
        Assert.True(regraChip >= 0);
        var regra = css[regraChip..css.IndexOf('}', regraChip)];
        Assert.Contains("max-width: 100%;", regra, StringComparison.Ordinal);
        Assert.Contains("min-width: 0;", regra, StringComparison.Ordinal);
        Assert.Contains("white-space: normal;", regra, StringComparison.Ordinal);
        Assert.DoesNotContain("flex:", regra, StringComparison.Ordinal);
        Assert.Contains("role=\"checkbox\"", lista, StringComparison.Ordinal);
        Assert.Contains("aria-label=", lista, StringComparison.Ordinal);
        var alternar = ExtrairBloco(dialog, "void AlternarMarcacao(");
        Assert.Contains("Previa.AlternarMarcacao(linha)", alternar, StringComparison.Ordinal);
        Assert.Contains("id=\"previa-motivo-bloqueio\" class=\"of-previa-erro\" role=\"alert\"", dialog, StringComparison.Ordinal);
        Assert.Contains("AriaDescribedBy=\"@(Previa.MensagemBloqueio is null ? null : \"previa-motivo-bloqueio\")\"", dialog, StringComparison.Ordinal);
        var barra = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Shared", "StickyActionBar.razor"));
        Assert.Contains("aria-describedby=\"@AriaDescribedBy\"", barra, StringComparison.Ordinal);

        var paraCompra = ExtrairBloco(
            File.ReadAllText(Path.Combine(raiz, "OrcaFacil.Core", "Llm", "PreviaComprovante.cs")),
            "CompraCestaFormModel ParaCompra()");
        var gravarItens = paraCompra.IndexOf("ProdutoCestaId = linha.ProdutoCestaId", StringComparison.Ordinal);
        var barreiraPrevia = paraCompra.IndexOf("CasamentoComprovante.GarantirConfirmacao", StringComparison.Ordinal);
        Assert.True(barreiraPrevia >= 0 && barreiraPrevia < gravarItens);

        var salvar = ExtrairBloco(
            File.ReadAllText(Path.Combine(raiz, "OrcaFacil.Core", "Services", "CestaCadastroService.cs")),
            "Task<CompraCesta> SalvarCompraAsync(CompraCestaFormModel modelo, int membroFamiliaId)");
        var escrita = salvar.IndexOf("_cestas.SalvarCompraAsync", StringComparison.Ordinal);
        var barreiraServico = salvar.IndexOf("CasamentoComprovante.GarantirConfirmacao", StringComparison.Ordinal);
        Assert.True(barreiraServico >= 0 && barreiraServico < escrita);

        var servicoPagina = ExtrairBloco(
            File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Services", "CestaService.cs")),
            "Task<CompraCesta> SalvarCompraAsync(CompraCestaFormModel modelo)");
        var delega = servicoPagina.IndexOf("_cadastro.SalvarCompraAsync", StringComparison.Ordinal);
        var barreiraPaginaServico = servicoPagina.IndexOf("CasamentoComprovante.GarantirConfirmacao", StringComparison.Ordinal);
        Assert.True(barreiraPaginaServico >= 0 && barreiraPaginaServico < delega);

        var abrir = ExtrairBloco(
            File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Pages", "Cesta", "CestaPage.razor")),
            "Task AbrirPreviaAsync()");
        var salvarPagina = abrir.IndexOf("SalvarCompraAsync(\"Nova compra\"", StringComparison.Ordinal);
        var barreiraPagina = abrir.IndexOf("CasamentoComprovante.GarantirConfirmacao", StringComparison.Ordinal);
        Assert.True(barreiraPagina >= 0 && barreiraPagina < salvarPagina);

        var leitura = ExtrairBloco(
            File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Pages", "Cesta", "CestaPage.razor")),
            "Task EnviarJpegAsync(");
        Assert.Contains("ProdutosCatalogoLeituraAsync", leitura, StringComparison.Ordinal);
        Assert.Contains("CatalogoPedidoComprovante.Texto(produtosCatalogo)", leitura, StringComparison.Ordinal);
        Assert.Contains("Normalizar(resultado.Comprovante, produtosCatalogo)", leitura, StringComparison.Ordinal);
        Assert.DoesNotContain("Normalizar(resultado.Comprovante, _produtos)", leitura, StringComparison.Ordinal);
        var textoCatalogo = leitura.IndexOf("CatalogoPedidoComprovante.Texto(produtosCatalogo)", StringComparison.Ordinal);
        var normalizar = leitura.IndexOf("Normalizar(resultado.Comprovante, produtosCatalogo)", StringComparison.Ordinal);
        Assert.True(textoCatalogo >= 0 && normalizar > textoCatalogo);
        Assert.DoesNotContain("ProdutosCatalogoLeituraAsync", leitura[textoCatalogo..normalizar], StringComparison.Ordinal);
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

    private static string EncontrarRaiz()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null)
        {
            if (File.Exists(Path.Combine(diretorio.FullName, "OrcaFacil.slnx")))
                return diretorio.FullName;

            diretorio = diretorio.Parent;
        }

        throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }

    private static string ExtrairBloco(string fonte, string assinatura)
    {
        var indice = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(indice >= 0, assinatura);
        return ExtrairChaves(fonte, fonte.IndexOf('{', indice));
    }

    private static string ExtrairChaves(string fonte, int abre)
    {
        var profundidade = 0;
        for (var i = abre; i < fonte.Length; i++)
        {
            if (fonte[i] == '{')
                profundidade++;
            else if (fonte[i] == '}')
            {
                profundidade--;
                if (profundidade == 0)
                    return fonte[abre..(i + 1)];
            }
        }

        throw new InvalidOperationException("Bloco sem fechamento.");
    }
}
