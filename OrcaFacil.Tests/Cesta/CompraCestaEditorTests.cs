using System.Globalization;
using OrcaFacil.Core.Cesta;
using OrcaFacil.Core.Dtos;

namespace OrcaFacil.Tests.Cesta;

public class CompraCestaEditorTests
{
    [Fact]
    public void Acrescentar_AtualizaPrecoQuandoOProdutoJaEstaNaCompra()
    {
        var itens = new List<ItemCompraCestaFormModel>
        {
            new() { ProdutoCestaId = 1, Preco = 10m },
            new() { ProdutoCestaId = 2, Preco = 4m }
        };

        CompraCestaEditor.AcrescentarOuAtualizar(itens, 1, 12.5m);

        Assert.Equal(2, itens.Count);
        Assert.Equal(12.5m, itens[0].Preco);
        Assert.Equal(4m, itens[1].Preco);
    }

    [Fact]
    public void Acrescentar_IncluiLinhaNova()
    {
        var itens = new List<ItemCompraCestaFormModel>();

        CompraCestaEditor.AcrescentarOuAtualizar(itens, 3, 7m);

        Assert.Single(itens);
        Assert.Equal(3, itens[0].ProdutoCestaId);
        Assert.Equal(7m, itens[0].Preco);
    }

    [Fact]
    public void Editar_ParaProdutoExistente_UneAsLinhas()
    {
        var atual = new ItemCompraCestaFormModel { ProdutoCestaId = 2, Preco = 4m };
        var itens = new List<ItemCompraCestaFormModel>
        {
            new() { ProdutoCestaId = 1, Preco = 10m },
            atual
        };

        CompraCestaEditor.AplicarEdicao(itens, atual, 1, 15m);

        Assert.Single(itens);
        Assert.Equal(1, itens[0].ProdutoCestaId);
        Assert.Equal(15m, itens[0].Preco);
        Assert.NotEqual(10m + 15m, itens[0].Preco);
    }

    [Fact]
    public void CompraManual_ProdutoRepetido_AtualizaOPrecoSemSomar()
    {
        var itens = new List<ItemCompraCestaFormModel>
        {
            new() { ProdutoCestaId = 4, Preco = 2.10m }
        };

        CompraCestaEditor.AcrescentarOuAtualizar(itens, 4, 3.25m);

        var item = Assert.Single(itens);
        Assert.Equal(4, item.ProdutoCestaId);
        Assert.Equal(3.25m, item.Preco);
        Assert.Null(item.ProdutoNovoChave);
        Assert.NotEqual(2.10m + 3.25m, item.Preco);
    }

    [Fact]
    public void EditarPendente_PrecoOuNome_PreservaChaveENome()
    {
        var atual = new ItemCompraCestaFormModel
        {
            ProdutoNovoChave = "acucar",
            NomePendente = "Açúcar",
            CategoriaPendente = "Alimentos",
            Preco = 2m
        };
        var itens = new List<ItemCompraCestaFormModel> { atual };

        CompraCestaEditor.AplicarEdicao(itens, atual, 0, 4.5m);

        Assert.Equal("acucar", atual.ProdutoNovoChave);
        Assert.Equal("Açúcar", atual.NomePendente);
        Assert.Equal("Alimentos", atual.CategoriaPendente);
        Assert.Equal(4.5m, atual.Preco);
        Assert.Equal(0, atual.ProdutoCestaId);

        CompraCestaEditor.AplicarEdicao(itens, atual, 0, 4.5m, "Açúcar cristal");

        Assert.Equal("acucar", atual.ProdutoNovoChave);
        Assert.Equal("Açúcar cristal", atual.NomePendente);
        Assert.Single(itens);
    }

    [Theory]
    [InlineData(0, 10, "Selecione o produto.")]
    [InlineData(1, 0, "Informe o preço do item.")]
    [InlineData(1, 5, null)]
    public void ValidarItem_RepeteAsMensagensDaTela(int produtoId, decimal preco, string? esperado)
    {
        Assert.Equal(esperado, CompraCestaEditor.ValidarItem(produtoId, preco));
    }

    [Fact]
    public void Pagamento_DivideSaldoComoOFormulario()
    {
        Assert.Equal(70m, CompraCestaEditor.Saldo(100m, 30m));

        var dinheiro = CompraCestaEditor.Pagamento(ModoPagamentoCesta.Dinheiro, 70m, 0m);
        Assert.Equal((70m, 0m), dinheiro);

        var cartao = CompraCestaEditor.Pagamento(ModoPagamentoCesta.Cartao, 70m, 40m);
        Assert.Equal((0m, 70m), cartao);

        var dividido = CompraCestaEditor.Pagamento(ModoPagamentoCesta.Dividido, 70m, 20m);
        Assert.Equal((20m, 50m), dividido);

        var estoura = CompraCestaEditor.Pagamento(ModoPagamentoCesta.Dividido, 70m, 80m);
        Assert.Equal((80m, -10m), estoura);
    }

    [Theory]
    [InlineData(10, 5, ModoPagamentoCesta.Dividido)]
    [InlineData(0, 5, ModoPagamentoCesta.Cartao)]
    [InlineData(0, 0, ModoPagamentoCesta.Dinheiro)]
    [InlineData(8, 0, ModoPagamentoCesta.Dinheiro)]
    public void DefinirModo_LeOPagamentoSalvo(decimal dinheiro, decimal cartao, ModoPagamentoCesta esperado)
    {
        Assert.Equal(esperado, CompraCestaEditor.DefinirModo(dinheiro, cartao));
    }

    [Fact]
    public void Situacao_CobreQuandoOBeneficioQuitaACompra()
    {
        Assert.Null(CompraCestaEditor.Situacao(0, 0, 0));
        Assert.Equal(SituacaoCompraCesta.Coberta, CompraCestaEditor.Situacao(100m, 100m, 0m));
        Assert.Equal(SituacaoCompraCesta.APagar, CompraCestaEditor.Situacao(100m, 40m, 60m));
        Assert.Equal("Coberta", CompraCestaEditor.RotuloSituacao(SituacaoCompraCesta.Coberta));
        Assert.Equal(100d, CompraCestaEditor.PercentualBeneficio(80m, 80m));
        Assert.Equal(25m, CompraCestaEditor.Aproveitamento(40m, 10m));
    }

    [Fact]
    public void Validar_SaldoInsuficienteUsaOValorDisponivel()
    {
        var cultura = CultureInfo.CurrentCulture;
        var ui = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
        try
        {
            var itens = new List<ItemCompraCestaFormModel> { new() { ProdutoCestaId = 1, Preco = 50m } };
            var mensagem = CompraCestaEditor.Validar(
                localId: 1,
                data: new DateTime(2026, 10, 16),
                itens,
                usarBeneficio: true,
                cartaoBeneficioId: 2,
                beneficio: 40m,
                saldoBeneficio: 15m,
                dinheiroEfetivo: 10m,
                cartaoEfetivo: 0m,
                saldo: 10m,
                total: 50m,
                cartaoCreditoId: 0);

            Assert.Equal("Saldo insuficiente no cartão de benefício (disponível: R$ 15,00). Recarregue antes de usar.", mensagem);
        }
        finally
        {
            CultureInfo.CurrentCulture = cultura;
            CultureInfo.CurrentUICulture = ui;
        }
    }

    [Fact]
    public void Validar_ExigeLocalDataItemECartao()
    {
        var vazio = new List<ItemCompraCestaFormModel>();
        Assert.Equal("Selecione o local da compra.", CompraCestaEditor.Validar(0, DateTime.Today, vazio, false, 0, 0, 0, 0, 0, 0, 0, 0));
        Assert.Equal("Informe a data da compra.", CompraCestaEditor.Validar(1, null, vazio, false, 0, 0, 0, 0, 0, 0, 0, 0));
        Assert.Equal("Inclua ao menos um produto.", CompraCestaEditor.Validar(1, DateTime.Today, vazio, false, 0, 0, 0, 0, 0, 0, 0, 0));

        var itens = new List<ItemCompraCestaFormModel> { new() { ProdutoCestaId = 1, Preco = 20m } };
        Assert.Equal(
            "Selecione o cartão de crédito.",
            CompraCestaEditor.Validar(1, DateTime.Today, itens, false, 0, 0, 0, 0, 20m, 20m, 20m, 0));
    }

    [Fact]
    public void EmDestaque_MantemOSelecionadoForaDosPrimeiros()
    {
        var produtos = Enumerable.Range(1, 8).Select(i => i.ToString()).ToList();

        var destaque = CompraCestaEditor.EmDestaque(produtos, p => p == "8");

        Assert.Equal(7, destaque.Count);
        Assert.Contains("8", destaque);
    }
}
