using System.Globalization;
using OrcaFacil.Core.Cesta;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Tests.Cesta;

public class CestaComparacaoRegrasTests
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    [Fact]
    public void Montar_ComDoisLocais_ApontaOMaisBaratoEAEconomia()
    {
        var compras = new List<CompraComparacao>
        {
            Compra(1, 1, "Boa Supermercado", new DateTime(2026, 10, 5), 0, null, ("Arroz", 20m), ("Feijão", 10m)),
            Compra(2, 2, "Fort Atacadista", new DateTime(2026, 10, 6), 0, null, ("Arroz", 16m), ("Feijão", 8m))
        };

        var resultado = CestaComparacaoRegras.Montar(compras, 10, 2026);

        Assert.True(resultado.Disponivel);
        Assert.Equal(
            $"O Fort Atacadista está 20% mais barato que o Boa Supermercado (economia de {Moeda(6m)} na cesta comparável).",
            resultado.Mensagem);
        Assert.Equal(24m, resultado.Locais.Single(l => l.Nome == "Fort Atacadista").TotalComparavel);
        Assert.True(resultado.Locais.Single(l => l.Nome == "Fort Atacadista").MaisBarato);
        var arroz = resultado.Produtos.Single(p => p.Nome == "Arroz");
        Assert.Equal(4m, arroz.EconomiaReais);
        Assert.True(arroz.Precos.Single(p => p.LocalNome == "Fort Atacadista").MaisBarato);
    }

    [Fact]
    public void Montar_ComUmaCompra_PedeASegunda()
    {
        var compras = new List<CompraComparacao>
        {
            Compra(1, 1, "Boa Supermercado", new DateTime(2026, 10, 5), 10m, TipoCartaoBeneficio.ValeAlimentacao, ("Arroz", 20m))
        };

        var resultado = CestaComparacaoRegras.Montar(compras, 10, 2026);

        Assert.False(resultado.Disponivel);
        Assert.Contains("segunda compra", resultado.Mensagem);
        Assert.Equal(50m, Assert.Single(resultado.Beneficios).AproveitamentoPercentual);
    }

    [Fact]
    public void Montar_Beneficio_MostraOndeRendeMais()
    {
        var compras = new List<CompraComparacao>
        {
            Compra(1, 1, "Boa Supermercado", new DateTime(2026, 10, 5), 10m, TipoCartaoBeneficio.ValeAlimentacao, ("Arroz", 30m)),
            Compra(2, 2, "Fort Atacadista", new DateTime(2026, 10, 6), 12m, TipoCartaoBeneficio.ValeRefeicao, ("Arroz", 24m))
        };

        var resultado = CestaComparacaoRegras.Montar(compras, 10, 2026);
        var boa = resultado.Beneficios.Single(b => b.LocalNome == "Boa Supermercado");
        var fort = resultado.Beneficios.Single(b => b.LocalNome == "Fort Atacadista");

        Assert.Equal(3m, boa.Rendimento);
        Assert.Equal(2m, fort.Rendimento);
        Assert.True(boa.RendeMais);
        Assert.False(fort.RendeMais);
        Assert.Equal(20m, boa.CustoDeBolso);
        Assert.Equal(12m, fort.CustoDeBolso);
    }

    [Fact]
    public void Montar_EvolucaoMensal_CalculaEconomia()
    {
        var compras = new List<CompraComparacao>
        {
            Compra(1, 1, "Boa Supermercado", new DateTime(2026, 9, 5), 0, null, ("Arroz", 30m)),
            Compra(2, 1, "Boa Supermercado", new DateTime(2026, 10, 5), 0, null, ("Arroz", 24m))
        };

        var resultado = CestaComparacaoRegras.Montar(compras, 10, 2026);

        Assert.Equal(-6m, resultado.Evolucao.DiferencaReais);
        Assert.Equal(20m, resultado.Evolucao.DiferencaPercentual);
        Assert.Contains("a menos", resultado.Evolucao.Mensagem);
        Assert.Contains("mais barata", resultado.Mensagem);
    }

    [Fact]
    public void Rascunhos_TrazPrecosDoMesAnteriorEIgnoraLocalJaComprado()
    {
        var compras = new List<CompraComparacao>
        {
            Compra(1, 1, "Boa Supermercado", new DateTime(2026, 9, 30), 15m, TipoCartaoBeneficio.ValeAlimentacao, ("Arroz", 20m), ("Feijão", 8m)),
            Compra(2, 2, "Fort Atacadista", new DateTime(2026, 9, 10), 0, null, ("Arroz", 18m)),
            Compra(3, 2, "Fort Atacadista", new DateTime(2026, 10, 2), 0, null, ("Arroz", 17m))
        };

        var rascunhos = CestaComparacaoRegras.Rascunhos(compras, 10, 2026);

        var rascunho = Assert.Single(rascunhos);
        Assert.Equal(1, rascunho.LocalId);
        Assert.Equal(new DateTime(2026, 10, 30), rascunho.Data);
        Assert.Equal(15m, rascunho.ValorBeneficio);
        Assert.Equal(new[] { 20m, 8m }, rascunho.Itens.Select(i => i.Preco));
    }

    [Fact]
    public void Rascunhos_AjustaDiaQuandoOMesAlvoEMaisCurto()
    {
        var compras = new List<CompraComparacao>
        {
            Compra(1, 1, "Boa Supermercado", new DateTime(2026, 1, 31), 0, null, ("Arroz", 10m))
        };

        var rascunho = Assert.Single(CestaComparacaoRegras.Rascunhos(compras, 2, 2026));

        Assert.Equal(new DateTime(2026, 2, 28), rascunho.Data);
    }

    [Fact]
    public void Ratear_DistribuiODescontoSemPerderCentavos()
    {
        var parcelas = CestaDespesaRegras.Ratear(
        [
            new ItemPrecoCategoria { Nome = "A", CategoriaId = 1, Preco = 10m },
            new ItemPrecoCategoria { Nome = "B", CategoriaId = 2, Preco = 10m },
            new ItemPrecoCategoria { Nome = "C", CategoriaId = 3, Preco = 10m }
        ], 1m);

        Assert.Equal(29m, parcelas.Sum(p => p.Valor));
        Assert.All(parcelas, p => Assert.True(p.Valor >= 0.01m));
    }

    [Fact]
    public void NormalizarPagamento_DivideOSaldoEntreDinheiroECartao()
    {
        var pagamento = CestaDespesaRegras.NormalizarPagamento(80m, 30m, 50m);

        Assert.Equal(30m, pagamento.Dinheiro);
        Assert.Equal(50m, pagamento.Cartao);
    }

    [Fact]
    public void NormalizarPagamento_SomaDiferenteDoSaldo_Recusa()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CestaDespesaRegras.NormalizarPagamento(80m, 30m, 40m));

        Assert.Contains("precisam somar", ex.Message);
    }

    [Fact]
    public void ValidarBeneficio_AcimaDoTotal_Recusa()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CestaDespesaRegras.ValidarBeneficio(10m, 11m, TipoCartaoBeneficio.ValeAlimentacao));

        Assert.Contains("não pode ser maior", ex.Message);
    }

    private static CompraComparacao Compra(
        int id,
        int localId,
        string local,
        DateTime data,
        decimal beneficio,
        TipoCartaoBeneficio? tipo,
        params (string Nome, decimal Preco)[] itens)
        => new(id, localId, local, data, beneficio, tipo, itens.Select((item, indice) => new ItemComparacao(indice + 1, item.Nome, item.Preco)).ToList());

    private static string Moeda(decimal valor) => valor.ToString("C", Cultura);
}
