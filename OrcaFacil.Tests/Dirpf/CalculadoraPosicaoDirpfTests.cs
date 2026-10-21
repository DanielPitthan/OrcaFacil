using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.Dirpf;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Tests.Dirpf;

public class CalculadoraPosicaoDirpfTests
{
    private static readonly DateTime Corte = new(2024, 12, 31);

    [Fact]
    public void ComprasEVendaParcial_UsamQuantidadeVezesPreco_EMantemOCustoMedio()
    {
        var posicao = CalculadoraPosicaoDirpf.Ate(
        [
            Movimento(1, new DateTime(2023, 11, 1), TipoOperacao.Compra, 100, valor: 1500m, custos: 0m, preco: 10m),
            Movimento(2, new DateTime(2024, 2, 10), TipoOperacao.Compra, 100, valor: 2200m, custos: 50m, preco: 20m),
            Movimento(3, new DateTime(2024, 6, 1), TipoOperacao.Venda, 50, valor: 2000m, custos: 8m, preco: 40m),
            Movimento(4, new DateTime(2025, 1, 15), TipoOperacao.Compra, 10, valor: 9999m, custos: 0m, preco: 1m)
        ], Corte);

        Assert.Equal(150m, posicao.Quantidade);
        Assert.Equal(2287.50m, posicao.CustoTotal);
        Assert.Equal(15.25m, posicao.PrecoMedio);
    }

    [Fact]
    public void CustoEm31DeDezembro_FechaComOCustoBaixado_QuandoPrecoEValorDiferem()
    {
        MovimentoPosicao[] movimentos =
        [
            Movimento(1, new DateTime(2024, 3, 1), TipoOperacao.Compra, 10, valor: 1500m, custos: 5m, preco: 10m),
            Movimento(2, new DateTime(2024, 8, 1), TipoOperacao.Venda, 4, valor: 900m, custos: 2m, preco: 30m)
        ];

        var ficha = CalculadoraPosicaoDirpf.Ate(movimentos, Corte);
        var travado = CalculadoraResultadoRealizado.Calcular(movimentos);

        Assert.NotEqual(movimentos[0].Quantidade * movimentos[0].PrecoUnitario, movimentos[0].ValorOperacao);
        Assert.Equal(travado.Posicao.Quantidade, ficha.Quantidade);
        Assert.Equal(travado.Posicao.CustoTotal, ficha.CustoTotal);
        Assert.Equal(6m, ficha.Quantidade);
        Assert.Equal(63m, ficha.CustoTotal);
    }

    [Fact]
    public void VendaNoMesmoDia_ComIdAnteriorAoDaCompra_BaixaAntes()
    {
        var posicao = CalculadoraPosicaoDirpf.Ate(
        [
            Movimento(1, new DateTime(2024, 1, 2), TipoOperacao.Compra, 10, valor: 100m, custos: 0m, preco: 10m),
            Movimento(5, new DateTime(2024, 6, 1), TipoOperacao.Compra, 10, valor: 100m, custos: 0m, preco: 20m),
            Movimento(2, new DateTime(2024, 6, 1), TipoOperacao.Venda, 4, valor: 200m, custos: 0m, preco: 50m)
        ], Corte);

        Assert.Equal(16m, posicao.Quantidade);
        Assert.Equal(260m, posicao.CustoTotal);
        Assert.Equal(16.25m, posicao.PrecoMedio);
    }

    [Fact]
    public void QuantidadeFracionaria_BaixaPeloCustoMedio()
    {
        var posicao = CalculadoraPosicaoDirpf.Ate(
        [
            Movimento(1, new DateTime(2024, 4, 1), TipoOperacao.Compra, 2.5m, valor: 99m, custos: 1m, preco: 4m),
            Movimento(2, new DateTime(2024, 5, 1), TipoOperacao.Venda, 0.5m, valor: 10m, custos: 0.2m, preco: 8m)
        ], Corte);

        Assert.Equal(2m, posicao.Quantidade);
        Assert.Equal(8.8m, posicao.CustoTotal);
        Assert.Equal(4.4m, posicao.PrecoMedio);
    }

    [Fact]
    public void VendaTotal_ZeraAPosicao()
    {
        var posicao = CalculadoraPosicaoDirpf.Ate(
        [
            Movimento(1, new DateTime(2024, 1, 5), TipoOperacao.Compra, 10, 100m, 0m, 10m),
            Movimento(2, new DateTime(2024, 8, 1), TipoOperacao.Venda, 10, 90m, 1m, 9m)
        ], Corte);

        Assert.Equal(0m, posicao.Quantidade);
        Assert.Equal(0m, posicao.CustoTotal);
        Assert.Equal(0m, posicao.PrecoMedio);
    }

    private static MovimentoPosicao Movimento(
        int id,
        DateTime data,
        TipoOperacao tipo,
        decimal quantidade,
        decimal valor,
        decimal custos,
        decimal preco) => new()
    {
        Id = id,
        Data = data,
        Tipo = tipo,
        Quantidade = quantidade,
        PrecoUnitario = preco,
        ValorOperacao = valor,
        Custos = custos
    };
}
