using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Core.Services;

namespace OrcaFacil.Tests.Cesta;

public class CartaoCestaFaturaTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly CestaRepository _cestas;
    private readonly CartaoRepository _cartoesRepo;
    private readonly CestaCadastroService _cestaService;
    private readonly CartaoCadastroService _cartaoService;
    private readonly TransacaoRepository _transacaoRepo;
    private readonly int _membroId;
    private readonly Categoria _alimentacao;

    public CartaoCestaFaturaTests()
    {
        _cestas = new CestaRepository(_factory);
        _cartoesRepo = new CartaoRepository(_factory);
        _cestaService = new CestaCadastroService(_cestas, _cartoesRepo);
        _cartaoService = new CartaoCadastroService(_cartoesRepo);
        _transacaoRepo = new TransacaoRepository(_factory);

        var membros = new MembroFamiliaRepository(_factory);
        var membro = new MembroFamilia { Nome = "Usuário" };
        membros.AddAsync(membro).GetAwaiter().GetResult();
        _membroId = membro.Id;

        using var contexto = _factory.CreateDbContext();
        _alimentacao = new Categoria { Nome = "Alimentação", Icone = "🛒", CorHex = "#B2CFE6" };
        contexto.Categorias.Add(_alimentacao);
        contexto.SaveChanges();
    }

    [Fact]
    public async Task Beneficio_RecargaESaldo_BloqueiaUsoAcimaDoSaldoERecuperaNaEdicao()
    {
        var vale = await _cartaoService.SalvarAsync(new CartaoFormModel
        {
            Nome = "Alelo Alimentação",
            Bandeira = "Alelo",
            UltimosDigitos = "4321",
            Tipo = TipoCartao.Beneficio,
            TipoBeneficio = TipoCartaoBeneficio.ValeAlimentacao
        });

        await _cartaoService.RecarregarAsync(new RecargaCartaoFormModel
        {
            CartaoId = vale.Id,
            Data = new DateTime(2026, 10, 1),
            Valor = 100m
        });

        Assert.Equal(100m, await _cartaoService.ObterSaldoBeneficioAsync(vale.Id));

        var arroz = await _cestaService.SalvarProdutoAsync(new ProdutoCestaFormModel { Nome = "Arroz", CategoriaId = _alimentacao.Id });
        var local = await _cestaService.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Boa Supermercado" });

        // Acima do saldo: deve falhar
        var modeloExcesso = new CompraCestaFormModel
        {
            LocalCompraId = local.Id,
            Data = new DateTime(2026, 10, 5),
            ValorBeneficio = 120m,
            CartaoBeneficioId = vale.Id,
            Itens = [new ItemCompraCestaFormModel { ProdutoCestaId = arroz.Id, Preco = 150m }]
        };
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => _cestaService.SalvarCompraAsync(modeloExcesso, _membroId));
        Assert.Contains("Saldo insuficiente", erro.Message);

        // Uso dentro do saldo (80 de 100)
        var modeloValido = new CompraCestaFormModel
        {
            LocalCompraId = local.Id,
            Data = new DateTime(2026, 10, 5),
            ValorBeneficio = 80m,
            CartaoBeneficioId = vale.Id,
            ValorDinheiro = 20m,
            Itens = [new ItemCompraCestaFormModel { ProdutoCestaId = arroz.Id, Preco = 100m }]
        };
        var compra = await _cestaService.SalvarCompraAsync(modeloValido, _membroId);
        Assert.Equal(20m, await _cartaoService.ObterSaldoBeneficioAsync(vale.Id));

        // Editar a mesma compra para usar 95 (o valor anterior de 80 volta para o saldo disponível)
        modeloValido.Id = compra.Id;
        modeloValido.ValorBeneficio = 95m;
        modeloValido.ValorDinheiro = 5m;
        await _cestaService.SalvarCompraAsync(modeloValido, _membroId);
        Assert.Equal(5m, await _cartaoService.ObterSaldoBeneficioAsync(vale.Id));
    }

    [Fact]
    public async Task Credito_SemFaturaExistente_CriaFaturaNovaNaoPagaESoDinheiroViraDespesaCesta()
    {
        var nubank = await _cartaoService.SalvarAsync(new CartaoFormModel
        {
            Nome = "Nubank",
            Bandeira = "Mastercard",
            UltimosDigitos = "9876",
            Tipo = TipoCartao.Credito
        });

        var arroz = await _cestaService.SalvarProdutoAsync(new ProdutoCestaFormModel { Nome = "Arroz", CategoriaId = _alimentacao.Id });
        var local = await _cestaService.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Fort Atacadista" });

        var compra = await _cestaService.SalvarCompraAsync(new CompraCestaFormModel
        {
            LocalCompraId = local.Id,
            Data = new DateTime(2026, 10, 10),
            ValorDinheiro = 30m,
            ValorCartaoCredito = 70m,
            CartaoCreditoId = nubank.Id,
            FaturaTransacaoId = null,
            Itens = [new ItemCompraCestaFormModel { ProdutoCestaId = arroz.Id, Preco = 100m }]
        }, _membroId);

        await using var contexto = _factory.CreateDbContext();
        var transacoes = await contexto.Transacoes.Include(t => t.Itens).Include(t => t.Categoria).ToListAsync();
        Assert.Equal(2, transacoes.Count);

        var despesaDinheiro = transacoes.Single(t => t.Categoria!.Nome == "Cesta Básica");
        Assert.Equal(30m, despesaDinheiro.Valor);
        Assert.True(despesaDinheiro.Pago);

        var fatura = transacoes.Single(t => t.Categoria!.Nome == "Cartão de Crédito");
        Assert.Equal(70m, fatura.Valor);
        Assert.False(fatura.Pago);
        Assert.Equal(nubank.Id, fatura.CartaoId);
        var itemFatura = Assert.Single(fatura.Itens);
        Assert.Equal(compra.ItemFaturaId, itemFatura.Id);
        Assert.Equal(70m, itemFatura.Valor);
        Assert.Equal("Cesta básica — Fort Atacadista", itemFatura.Descricao);

        // Não dobra despesa total: 30 (dinheiro) + 70 (fatura) = 100
        var somas = await _transacaoRepo.SomaPorCategoriaAsync(compra.Data, compra.Data, TipoTransacao.Despesa);
        Assert.Equal(100m, somas.Values.Sum());
    }

    [Fact]
    public async Task Credito_SoCartaoSemDinheiro_NaoCriaDespesaCesta_EExclusaoRemoveFaturaZerada()
    {
        var nubank = await _cartaoService.SalvarAsync(new CartaoFormModel
        {
            Nome = "Nubank",
            Bandeira = "Mastercard",
            UltimosDigitos = "9876",
            Tipo = TipoCartao.Credito
        });

        var arroz = await _cestaService.SalvarProdutoAsync(new ProdutoCestaFormModel { Nome = "Arroz", CategoriaId = _alimentacao.Id });
        var local = await _cestaService.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Fort Atacadista" });

        var compra = await _cestaService.SalvarCompraAsync(new CompraCestaFormModel
        {
            LocalCompraId = local.Id,
            Data = new DateTime(2026, 10, 10),
            ValorDinheiro = 0m,
            ValorCartaoCredito = 60m,
            CartaoCreditoId = nubank.Id,
            Itens = [new ItemCompraCestaFormModel { ProdutoCestaId = arroz.Id, Preco = 60m }]
        }, _membroId);

        Assert.Null(compra.TransacaoId);
        Assert.NotNull(compra.ItemFaturaId);

        await _cestaService.ExcluirCompraAsync(compra.Id);

        await using var contexto = _factory.CreateDbContext();
        Assert.Empty(contexto.ComprasCesta);
        Assert.Empty(contexto.Transacoes);
        Assert.Empty(contexto.ItensLancamento);
    }

    [Fact]
    public async Task Credito_ComFaturaExistente_AnexaItemEAjustaNaEdicaoENaExclusao()
    {
        var nubank = await _cartaoService.SalvarAsync(new CartaoFormModel
        {
            Nome = "Nubank",
            Bandeira = "Mastercard",
            UltimosDigitos = "1111",
            Tipo = TipoCartao.Credito
        });

        var categoriaCartaoId = await _cestas.ObterOuCriarCategoriaCartaoCreditoAsync();

        // Fatura já aberta com R$ 200 de outros gastos (sem CartaoId ainda)
        var faturaExistente = new Transacao
        {
            Descricao = "Fatura Outubro",
            Valor = 200m,
            Data = new DateTime(2026, 10, 20),
            Tipo = TipoTransacao.Despesa,
            Pago = false,
            CategoriaId = categoriaCartaoId,
            MembroFamiliaId = _membroId
        };
        await _transacaoRepo.AddAsync(faturaExistente);

        var abertas = await _cartaoService.ListarFaturasAbertasAsync(nubank.Id);
        Assert.Single(abertas);

        var arroz = await _cestaService.SalvarProdutoAsync(new ProdutoCestaFormModel { Nome = "Arroz", CategoriaId = _alimentacao.Id });
        var local = await _cestaService.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Boa Supermercado" });

        var modelo = new CompraCestaFormModel
        {
            LocalCompraId = local.Id,
            Data = new DateTime(2026, 10, 12),
            ValorDinheiro = 0m,
            ValorCartaoCredito = 50m,
            CartaoCreditoId = nubank.Id,
            FaturaTransacaoId = faturaExistente.Id,
            Itens = [new ItemCompraCestaFormModel { ProdutoCestaId = arroz.Id, Preco = 50m }]
        };
        var compra = await _cestaService.SalvarCompraAsync(modelo, _membroId);

        await using (var ctx = _factory.CreateDbContext())
        {
            var fatura = await ctx.Transacoes.Include(t => t.Itens).SingleAsync(t => t.Id == faturaExistente.Id);
            Assert.Equal(250m, fatura.Valor);
            Assert.Equal(nubank.Id, fatura.CartaoId);
            Assert.Equal(50m, Assert.Single(fatura.Itens).Valor);
        }

        // Editar compra: crédito sobe de 50 para 80 -> fatura vai para 280
        modelo.Id = compra.Id;
        modelo.ValorCartaoCredito = 80m;
        modelo.Itens[0].Preco = 80m;
        await _cestaService.SalvarCompraAsync(modelo, _membroId);

        await using (var ctx = _factory.CreateDbContext())
        {
            var fatura = await ctx.Transacoes.Include(t => t.Itens).SingleAsync(t => t.Id == faturaExistente.Id);
            Assert.Equal(280m, fatura.Valor);
            Assert.Equal(80m, Assert.Single(fatura.Itens).Valor);
        }

        // Excluir compra: remove o item e volta a fatura para 200 (sem apagar a fatura pois restam 200)
        await _cestaService.ExcluirCompraAsync(compra.Id);

        await using (var ctx = _factory.CreateDbContext())
        {
            var fatura = await ctx.Transacoes.Include(t => t.Itens).SingleAsync(t => t.Id == faturaExistente.Id);
            Assert.Equal(200m, fatura.Valor);
            Assert.Empty(fatura.Itens);
        }
    }

    public void Dispose() => _factory.Dispose();
}
