using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.Dirpf;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Tests.Dirpf;

public class CalculadoraPosicaoDirpfTests
{
    private static readonly DateTime Corte = new(2024, 12, 31);

    [Fact]
    public void ComprasEVendaParcial_UsamValorDaOperacao_EMantemOCustoMedio()
    {
        var posicao = CalculadoraPosicaoDirpf.Ate(
        [
            Movimento(1, new DateTime(2023, 11, 1), TipoOperacao.Compra, 100, valor: 1500m, custos: 0m, preco: 10m),
            Movimento(2, new DateTime(2024, 2, 10), TipoOperacao.Compra, 100, valor: 2200m, custos: 50m, preco: 20m),
            Movimento(3, new DateTime(2024, 6, 1), TipoOperacao.Venda, 50, valor: 2000m, custos: 8m, preco: 40m),
            Movimento(4, new DateTime(2025, 1, 15), TipoOperacao.Compra, 10, valor: 9999m, custos: 0m, preco: 1m)
        ], Corte);

        Assert.Equal(150m, posicao.Quantidade);
        Assert.Equal(2812.50m, posicao.CustoTotal);
        Assert.Equal(18.75m, posicao.PrecoMedio);
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
