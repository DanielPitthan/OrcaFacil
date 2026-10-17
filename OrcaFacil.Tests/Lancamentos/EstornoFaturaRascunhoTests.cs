using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Lancamentos;

namespace OrcaFacil.Tests.Lancamentos;

public class EstornoFaturaRascunhoTests
{
    [Fact]
    public void Lancar_AbateOTotalEEntraNaSequencia()
    {
        var fatura = Fatura(500m);
        ItensFaturaInclusao.Acrescentar(fatura.Itens, 2, "Alimentação", 500m, NaturezaItemLancamento.Avulsa, null, null, "Mercado");

        var estorno = EstornoFaturaRascunho.Lancar(
            fatura, new PedidoEstorno(100m, 3, "  Devolução loja X "), "Lazer", mesFechado: false);

        Assert.Equal(400m, fatura.Valor);
        Assert.Equal(100m, estorno.Valor);
        Assert.True(estorno.EhEstorno);
        Assert.Equal("Devolução loja X", estorno.Descritivo);
        Assert.Equal("Lazer", estorno.Descricao);
        Assert.Equal(NaturezaItemLancamento.Avulsa, estorno.Natureza);
        Assert.Equal(["Mercado", "Devolução loja X"], FaturaCartaoEditor.Ordenar(fatura.Itens).Select(i => i.Descritivo));
        Assert.Equal(400m, FaturaCartaoEditor.Soma(fatura.Itens));
    }

    [Fact]
    public void Lancar_TotalAbaixoDoMinimo_NaoMexeNoRascunho()
    {
        var fatura = Fatura(30m);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            EstornoFaturaRascunho.Lancar(fatura, new PedidoEstorno(30m, 3, "Devolução"), "Lazer", mesFechado: false));

        Assert.Contains("0,01", ex.Message);
        Assert.Equal(30m, fatura.Valor);
        Assert.Empty(fatura.Itens);
    }

    [Fact]
    public void Lancar_MesFechado_RecusaComMensagemPadrao()
    {
        var fatura = Fatura(200m);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            EstornoFaturaRascunho.Lancar(fatura, new PedidoEstorno(10m, 3, "Devolução"), "Lazer", mesFechado: true));

        Assert.Equal(ItensFaturaRegras.MensagemMesFechado(fatura.Data), ex.Message);
        Assert.Equal(200m, fatura.Valor);
        Assert.Empty(fatura.Itens);
    }

    [Fact]
    public void Lancar_ValorNaoPositivo_Recusa()
    {
        var fatura = Fatura(200m);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            EstornoFaturaRascunho.Lancar(fatura, new PedidoEstorno(0m, 3, "Devolução"), "Lazer", mesFechado: false));

        Assert.Equal("Informe o valor do item.", ex.Message);
        Assert.Equal(200m, fatura.Valor);
    }

    [Fact]
    public void Excluir_DevolveAMagnitudeEMantemACompra()
    {
        var fatura = Fatura(500m);
        var compra = ItensFaturaInclusao.Acrescentar(
            fatura.Itens, 2, "Alimentação", 500m, NaturezaItemLancamento.Avulsa, null, null, "Mercado");
        var estorno = EstornoFaturaRascunho.Lancar(
            fatura, new PedidoEstorno(100m, 3, "Devolução"), "Lazer", mesFechado: false);
        Assert.Equal(400m, fatura.Valor);

        EstornoFaturaRascunho.Excluir(fatura, estorno, mesFechado: false);

        Assert.Equal(500m, fatura.Valor);
        Assert.Equal([compra], fatura.Itens);
        Assert.Equal(500m, FaturaCartaoEditor.Soma(fatura.Itens));
    }

    [Fact]
    public void Excluir_MesFechado_NaoRestaura()
    {
        var fatura = Fatura(400m);
        var estorno = EstornoNoRascunho(fatura, 100m);
        fatura.Valor = 300m;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            EstornoFaturaRascunho.Excluir(fatura, estorno, mesFechado: true));

        Assert.Contains("está fechado", ex.Message);
        Assert.Equal(300m, fatura.Valor);
        Assert.Contains(estorno, fatura.Itens);
    }

    [Fact]
    public void Excluir_Compra_NaoPassaPorEsteCaminho()
    {
        var fatura = Fatura(500m);
        var compra = ItensFaturaInclusao.Acrescentar(
            fatura.Itens, 2, "Alimentação", 200m, NaturezaItemLancamento.Avulsa, null, null, null);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            EstornoFaturaRascunho.Excluir(fatura, compra, mesFechado: false));

        Assert.Contains("estorno", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(500m, fatura.Valor);
        Assert.Contains(compra, fatura.Itens);
    }

    [Fact]
    public void Editar_AjustaOTotalPelaDiferenca()
    {
        var fatura = Fatura(400m);
        var estorno = EstornoNoRascunho(fatura, 100m);

        EstornoFaturaRascunho.Editar(
            fatura, estorno, new PedidoEstorno(40m, 4, " Loja Y "), "Transporte", mesFechado: false);

        Assert.Equal(460m, fatura.Valor);
        Assert.Equal(40m, estorno.Valor);
        Assert.Equal("Loja Y", estorno.Descritivo);
        Assert.Equal("Transporte", estorno.Descricao);
        Assert.Equal(4, estorno.CategoriaId);
        Assert.Equal(NaturezaItemLancamento.Avulsa, estorno.Natureza);
    }

    [Fact]
    public void Editar_LiquidoAbaixoDoMinimo_DesfazALinha()
    {
        var fatura = Fatura(100m);
        var estorno = EstornoNoRascunho(fatura, 10m);
        fatura.Valor = 90m;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            EstornoFaturaRascunho.Editar(
                fatura, estorno, new PedidoEstorno(100m, 3, "Devolução"), "Lazer", mesFechado: false));

        Assert.Contains("0,01", ex.Message);
        Assert.Equal(90m, fatura.Valor);
        Assert.Equal(10m, estorno.Valor);
        Assert.Equal("Lazer", estorno.Descricao);
    }

    [Fact]
    public void EspelharLancamento_AbateUmaVezEVaiParaOFimDaLista()
    {
        var fatura = Fatura(500m);
        ItensFaturaInclusao.Acrescentar(fatura.Itens, 2, "Alimentação", 200m, NaturezaItemLancamento.Avulsa, null, null, "Mercado");

        EstornoFaturaRascunho.EspelharLancamento(fatura, new ItemLancamento
        {
            Id = 9,
            Descricao = "Lazer",
            Descritivo = "Devolução loja X",
            Valor = 100m,
            EhEstorno = true,
            CategoriaId = 3,
            Ordem = 0
        });

        Assert.Equal(400m, fatura.Valor);
        var estorno = Assert.Single(fatura.Itens, i => i.EhEstorno);
        Assert.Equal(9, estorno.Id);
        Assert.Equal(1, estorno.Ordem);
        Assert.Equal("Devolução loja X", estorno.Descritivo);
        Assert.Equal(["Mercado", "Devolução loja X"], FaturaCartaoEditor.Ordenar(fatura.Itens).Select(i => i.Descritivo));
    }

    [Fact]
    public void EspelharExclusao_DevolveAMagnitude()
    {
        var fatura = Fatura(400m);
        var estorno = EstornoNoRascunho(fatura, 100m);

        EstornoFaturaRascunho.EspelharExclusao(fatura, estorno);

        Assert.Equal(500m, fatura.Valor);
        Assert.Empty(fatura.Itens);
    }

    [Fact]
    public void TextoParaEdicao_PrefereDescritivoEDepoisDescricaoAntiga()
    {
        var comDescritivo = new ItemLancamentoFormModel
        {
            Descricao = "Alimentação",
            Descritivo = " Devolução loja X ",
            EhEstorno = true
        };
        var legado = new ItemLancamentoFormModel { Descricao = "Estorno mercado", EhEstorno = true };
        var soCategoria = new ItemLancamentoFormModel { Descricao = "Alimentação", EhEstorno = true };

        Assert.Equal("Devolução loja X", EstornoFaturaRascunho.TextoParaEdicao(comDescritivo, "Alimentação"));
        Assert.Equal("Estorno mercado", EstornoFaturaRascunho.TextoParaEdicao(legado, "Alimentação"));
        Assert.Null(EstornoFaturaRascunho.TextoParaEdicao(soCategoria, "Alimentação"));
    }

    private static TransacaoFormModel Fatura(decimal valor) => new()
    {
        Descricao = "Fatura Nubank",
        Valor = valor,
        Data = new DateTime(2026, 10, 18),
        Tipo = TipoTransacao.Despesa,
        CategoriaId = 1
    };

    private static ItemLancamentoFormModel EstornoNoRascunho(TransacaoFormModel fatura, decimal valor)
    {
        var item = new ItemLancamentoFormModel
        {
            Descricao = "Lazer",
            Descritivo = "Devolução",
            Valor = valor,
            EhEstorno = true,
            CategoriaId = 3,
            Ordem = ItensFaturaInclusao.ProximaOrdem(fatura.Itens)
        };
        fatura.Itens.Add(item);
        return item;
    }
}
