using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Lancamentos;

namespace OrcaFacil.Tests.Lancamentos;

public class ItensFaturaInclusaoTests
{
    [Fact]
    public void Acrescentar_MesmaCategoriaEMesmoValor_MantemLinhasSeparadasComDescritivo()
    {
        var itens = new List<ItemLancamentoFormModel>();

        ItensFaturaInclusao.Acrescentar(itens, 2, "Alimentação", 40m, NaturezaItemLancamento.Avulsa, null, null, "McDonald's");
        ItensFaturaInclusao.Acrescentar(itens, 2, "Alimentação", 40m, NaturezaItemLancamento.Avulsa, null, null, " Big Steak ");
        ItensFaturaInclusao.Acrescentar(itens, 2, "Alimentação", 40m, NaturezaItemLancamento.Avulsa, null, null, "Jantar no restaurante XPTO");
        ItensFaturaInclusao.Acrescentar(itens, 4, "Compras", 900m, NaturezaItemLancamento.Avulsa, null, null, "Celular da loja A");
        ItensFaturaInclusao.Acrescentar(itens, 4, "Compras", 900m, NaturezaItemLancamento.Avulsa, null, null, "Celular da loja B");
        ItensFaturaInclusao.Acrescentar(itens, 3, "Lazer", 55.90m, NaturezaItemLancamento.Assinatura, null, null, "Netflix");
        ItensFaturaInclusao.Acrescentar(itens, 3, "Lazer", 14.90m, NaturezaItemLancamento.Assinatura, null, null, "Amazon Prime");
        ItensFaturaInclusao.Acrescentar(itens, 3, "Lazer", 19.90m, NaturezaItemLancamento.Assinatura, null, null, "   ");

        Assert.Equal(8, itens.Count);
        Assert.Equal(3, itens.Count(i => i.CategoriaId == 2 && i.Valor == 40m));
        Assert.Equal(2, itens.Count(i => i.CategoriaId == 4 && i.Valor == 900m));
        Assert.Equal(["McDonald's", "Big Steak", "Jantar no restaurante XPTO"], itens.Take(3).Select(i => i.Descritivo));
        Assert.Null(itens.Last().Descritivo);
        Assert.Equal("Lazer", ItensFaturaInclusao.Titulo(itens.Last().Descritivo, itens.Last().Descricao));
        Assert.Equal("Netflix", ItensFaturaInclusao.Titulo(itens[5].Descritivo, itens[5].Descricao));
        Assert.All(itens.Where(i => i.CategoriaId == 2), i => Assert.Equal("Alimentação", i.Descricao));
    }

    [Fact]
    public void Acrescentar_DescritivoLongo_RecusaSemIncluirALinha()
    {
        var itens = new List<ItemLancamentoFormModel>();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ItensFaturaInclusao.Acrescentar(
                itens, 2, "Alimentação", 10m, NaturezaItemLancamento.Avulsa, null, null, new string('a', 121)));

        Assert.Contains("120", ex.Message);
        Assert.Empty(itens);
    }

    [Fact]
    public void Titulo_UsaDescritivoQuandoPreenchido()
    {
        Assert.Equal("Netflix", ItensFaturaInclusao.Titulo(" Netflix ", "Lazer"));
        Assert.Equal("Lazer", ItensFaturaInclusao.Titulo("  ", "Lazer"));
        Assert.Equal("Lazer", ItensFaturaInclusao.Titulo(null, "Lazer"));
        Assert.False(ItensFaturaInclusao.TemDescritivo(null));
        Assert.True(ItensFaturaInclusao.TemDescritivo("Streaming 1"));
    }
}
