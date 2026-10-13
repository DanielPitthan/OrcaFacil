using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Lancamentos;

namespace OrcaFacil.Tests.Lancamentos;

public class ItensFaturaRegrasTests
{
    [Fact]
    public void Validar_SomaAcimaDoTotal_Recusa()
    {
        var itens = new List<ItemLancamento>
        {
            Item("Almoço", 600m, 2)
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ItensFaturaRegras.Validar(500m, categoriaFaturaId: 1, permiteItens: true, TipoTransacao.Despesa, itens));

        Assert.Contains("não pode passar", ex.Message);
    }

    [Fact]
    public void Validar_ParcelaInvalida_Recusa()
    {
        var item = Item("Mesa", 200m, 2);
        item.Natureza = NaturezaItemLancamento.Parcelada;
        item.ParcelaAtual = 13;
        item.TotalParcelas = 12;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ItensFaturaRegras.Validar(200m, categoriaFaturaId: 1, permiteItens: true, TipoTransacao.Despesa, [item]));

        Assert.Contains("parcela", ex.Message);
    }

    [Fact]
    public void Validar_SomaParcial_Aceita()
    {
        var itens = new List<ItemLancamento> { Item("Netflix", 70m, 2) };

        ItensFaturaRegras.Validar(5000m, categoriaFaturaId: 1, permiteItens: true, TipoTransacao.Despesa, itens);
    }

    [Fact]
    public void SelecionarParaCopia_AvancaParcelaEDescartaAvulsaEQuitada()
    {
        var itens = new List<ItemLancamento>
        {
            new() { Id = 1, Descricao = "Netflix", Valor = 70m, Natureza = NaturezaItemLancamento.Assinatura, CategoriaId = 2, Ordem = 0 },
            new() { Id = 2, Descricao = "Mesa", Valor = 2000m, Natureza = NaturezaItemLancamento.Parcelada, ParcelaAtual = 2, TotalParcelas = 12, CategoriaId = 3, Ordem = 1 },
            new() { Id = 3, Descricao = "Almoço", Valor = 500m, Natureza = NaturezaItemLancamento.Avulsa, CategoriaId = 4, Ordem = 2 },
            new() { Id = 4, Descricao = "Consultoria", Valor = 100m, Natureza = NaturezaItemLancamento.Parcelada, ParcelaAtual = 12, TotalParcelas = 12, CategoriaId = 5, Ordem = 3 }
        };

        var copias = ItensFaturaRegras.SelecionarParaCopia(itens);

        Assert.Equal(2, copias.Count);
        Assert.Equal("Netflix", copias[0].Descricao);
        Assert.Equal(NaturezaItemLancamento.Assinatura, copias[0].Natureza);
        Assert.Equal("Mesa", copias[1].Descricao);
        Assert.Equal(3, copias[1].ParcelaAtual);
        Assert.Equal(12, copias[1].TotalParcelas);
        Assert.Equal(2070m, copias.Sum(i => i.Valor));
    }

    private static ItemLancamento Item(string descricao, decimal valor, int categoriaId) => new()
    {
        Descricao = descricao,
        Valor = valor,
        CategoriaId = categoriaId,
        Natureza = NaturezaItemLancamento.Avulsa
    };
}
