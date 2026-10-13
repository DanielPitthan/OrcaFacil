using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Core.Services;

namespace OrcaFacil.Tests.Cesta;

public class CestaCadastroServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly CestaRepository _cestas;
    private readonly CestaCadastroService _service;
    private readonly int _membroId;
    private readonly Categoria _alimentacao;
    private readonly Categoria _limpeza;

    public CestaCadastroServiceTests()
    {
        _cestas = new CestaRepository(_factory);
        _service = new CestaCadastroService(_cestas);

        var membros = new MembroFamiliaRepository(_factory);
        var membro = new MembroFamilia { Nome = "Usuário" };
        membros.AddAsync(membro).GetAwaiter().GetResult();
        _membroId = membro.Id;

        using var contexto = _factory.CreateDbContext();
        _alimentacao = new Categoria { Nome = "Alimentação", Icone = "🛒", CorHex = "#B2CFE6" };
        _limpeza = new Categoria { Nome = "Limpeza", Icone = "🧽", CorHex = "#659ECD" };
        contexto.Categorias.AddRange(_alimentacao, _limpeza);
        contexto.SaveChanges();
    }

    [Fact]
    public async Task SalvarCompra_GeraDespesaClassificadaPeloProduto()
    {
        var arroz = await Produto("Arroz", _alimentacao.Id);
        var detergente = await Produto("Detergente", _limpeza.Id);
        var local = await Local("Fort Atacadista");

        var compra = await _service.SalvarCompraAsync(Modelo(local.Id, 0, null, (arroz.Id, 60m), (detergente.Id, 40m)), _membroId);

        await using var contexto = _factory.CreateDbContext();
        var despesa = await contexto.Transacoes.Include(t => t.Itens).SingleAsync();
        var categoriaCesta = await contexto.Categorias.SingleAsync(c => c.Nome == "Cesta Básica");

        Assert.Equal(despesa.Id, compra.TransacaoId);
        Assert.Equal(100m, despesa.Valor);
        Assert.True(despesa.Pago);
        Assert.Equal(TipoTransacao.Despesa, despesa.Tipo);
        Assert.Equal(categoriaCesta.Id, despesa.CategoriaId);
        Assert.Equal(2, despesa.Itens.Count);
        Assert.Equal(60m, despesa.Itens.Single(i => i.Descricao == "Arroz").Valor);
        Assert.Equal(_alimentacao.Id, despesa.Itens.Single(i => i.Descricao == "Arroz").CategoriaId);
        Assert.Equal(_limpeza.Id, despesa.Itens.Single(i => i.Descricao == "Detergente").CategoriaId);
        Assert.All(despesa.Itens, item => Assert.Equal(NaturezaItemLancamento.Avulsa, item.Natureza));

        var somas = await new TransacaoRepository(_factory).SomaPorCategoriaAsync(compra.Data, compra.Data, TipoTransacao.Despesa);
        Assert.Equal(60m, somas[_alimentacao.Id]);
        Assert.Equal(40m, somas[_limpeza.Id]);
        Assert.DoesNotContain(categoriaCesta.Id, somas.Keys);
    }

    [Fact]
    public async Task SalvarCompra_BeneficioEntraComoDesconto()
    {
        var arroz = await Produto("Arroz", _alimentacao.Id);
        var detergente = await Produto("Detergente", _limpeza.Id);
        var local = await Local("Boa Supermercado");

        await _service.SalvarCompraAsync(
            Modelo(local.Id, 20m, TipoCartaoBeneficio.ValeAlimentacao, (arroz.Id, 60m), (detergente.Id, 40m)),
            _membroId);

        await using var contexto = _factory.CreateDbContext();
        var despesa = await contexto.Transacoes.Include(t => t.Itens).SingleAsync();

        Assert.Equal(80m, despesa.Valor);
        Assert.Equal(80m, despesa.Itens.Sum(i => i.Valor));
        Assert.Equal(48m, despesa.Itens.Single(i => i.Descricao == "Arroz").Valor);
        Assert.Equal(32m, despesa.Itens.Single(i => i.Descricao == "Detergente").Valor);
        Assert.Contains("desconto", despesa.Observacao);
        Assert.Contains("Vale Alimentação", despesa.Observacao);

        var somas = await new TransacaoRepository(_factory).SomaPorCategoriaAsync(despesa.Data, despesa.Data, TipoTransacao.Despesa);
        Assert.Equal(48m, somas[_alimentacao.Id]);
        Assert.Equal(32m, somas[_limpeza.Id]);
    }

    [Fact]
    public async Task SalvarCompra_DivideSaldoEntreDinheiroECartao()
    {
        var arroz = await Produto("Arroz", _alimentacao.Id);
        var local = await Local("Boa Supermercado");
        var modelo = Modelo(local.Id, 20m, TipoCartaoBeneficio.ValeAlimentacao, (arroz.Id, 100m));
        modelo.ValorDinheiro = 30m;
        modelo.ValorCartaoCredito = 50m;

        var compra = await _service.SalvarCompraAsync(modelo, _membroId);

        Assert.Equal(30m, compra.ValorDinheiro);
        Assert.Equal(50m, compra.ValorCartaoCredito);
        await using var contexto = _factory.CreateDbContext();
        var despesa = await contexto.Transacoes.SingleAsync();
        Assert.Equal(80m, despesa.Valor);
        Assert.Contains("Dinheiro", despesa.Observacao);
        Assert.Contains("Cartão de crédito", despesa.Observacao);
    }

    [Fact]
    public async Task SalvarCompra_PagamentoQueNaoFechaOSaldo_NaoGrava()
    {
        var arroz = await Produto("Arroz", _alimentacao.Id);
        var local = await Local("Fort Atacadista");
        var modelo = Modelo(local.Id, 0, null, (arroz.Id, 40m));
        modelo.ValorDinheiro = 10m;
        modelo.ValorCartaoCredito = 10m;

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SalvarCompraAsync(modelo, _membroId));

        await using var contexto = _factory.CreateDbContext();
        Assert.Empty(contexto.ComprasCesta);
    }

    [Fact]
    public async Task SalvarCompra_BeneficioCobreTudo_NaoGeraDespesa()
    {
        var arroz = await Produto("Arroz", _alimentacao.Id);
        var local = await Local("Amazon");

        var compra = await _service.SalvarCompraAsync(
            Modelo(local.Id, 15m, TipoCartaoBeneficio.ValeRefeicao, (arroz.Id, 15m)),
            _membroId);

        Assert.Null(compra.TransacaoId);
        await using var contexto = _factory.CreateDbContext();
        Assert.Empty(contexto.Transacoes);
    }

    [Fact]
    public async Task SalvarCompra_AtualizarPreco_AtualizaADespesa()
    {
        var arroz = await Produto("Arroz", _alimentacao.Id);
        var local = await Local("Fort Atacadista");
        var modelo = Modelo(local.Id, 10m, TipoCartaoBeneficio.Outro, (arroz.Id, 40m));
        var compra = await _service.SalvarCompraAsync(modelo, _membroId);

        modelo.Id = compra.Id;
        modelo.Itens[0].Preco = 50m;
        await _service.SalvarCompraAsync(modelo, _membroId);

        await using var contexto = _factory.CreateDbContext();
        var despesa = await contexto.Transacoes.Include(t => t.Itens).SingleAsync();
        Assert.Equal(40m, despesa.Valor);
        Assert.Equal(50m, Assert.Single(contexto.ItensCompraCesta).Preco);
    }

    [Fact]
    public async Task ExcluirCompra_RemoveADespesa()
    {
        var arroz = await Produto("Arroz", _alimentacao.Id);
        var local = await Local("Fort Atacadista");
        var compra = await _service.SalvarCompraAsync(Modelo(local.Id, 0, null, (arroz.Id, 10m)), _membroId);

        await _service.ExcluirCompraAsync(compra.Id);

        await using var contexto = _factory.CreateDbContext();
        Assert.Empty(contexto.ComprasCesta);
        Assert.Empty(contexto.Transacoes);
    }

    [Fact]
    public async Task SalvarCompra_BeneficioMaiorQueOTotal_NaoGrava()
    {
        var arroz = await Produto("Arroz", _alimentacao.Id);
        var local = await Local("Fort Atacadista");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SalvarCompraAsync(Modelo(local.Id, 50m, TipoCartaoBeneficio.ValeAlimentacao, (arroz.Id, 10m)), _membroId));

        await using var contexto = _factory.CreateDbContext();
        Assert.Empty(contexto.ComprasCesta);
    }

    [Fact]
    public async Task SalvarProduto_NomeRepetido_Recusa()
    {
        await _service.SalvarProdutoAsync(new ProdutoCestaFormModel { Nome = "Arroz", CategoriaId = _alimentacao.Id });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SalvarProdutoAsync(new ProdutoCestaFormModel { Nome = "arroz", CategoriaId = _alimentacao.Id }));

        Assert.Contains("Já existe", ex.Message);
    }

    public void Dispose() => _factory.Dispose();

    private Task<ProdutoCesta> Produto(string nome, int categoriaId)
        => _service.SalvarProdutoAsync(new ProdutoCestaFormModel { Nome = nome, CategoriaId = categoriaId });

    private Task<LocalCompra> Local(string nome)
        => _service.SalvarLocalAsync(new LocalCompraFormModel { Nome = nome });

    private static CompraCestaFormModel Modelo(
        int localId,
        decimal beneficio,
        TipoCartaoBeneficio? tipo,
        params (int ProdutoId, decimal Preco)[] itens)
        => new()
        {
            LocalCompraId = localId,
            Data = new DateTime(2026, 10, 5),
            ValorBeneficio = beneficio,
            TipoCartaoBeneficio = tipo,
            Itens = itens.Select(item => new ItemCompraCestaFormModel
            {
                ProdutoCestaId = item.ProdutoId,
                Preco = item.Preco
            }).ToList()
        };
}
