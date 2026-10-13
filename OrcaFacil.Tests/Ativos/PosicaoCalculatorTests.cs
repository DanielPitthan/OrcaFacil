using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.Enums;
using Xunit;

namespace OrcaFacil.Tests.Ativos;

public class PosicaoCalculatorTests
{
    private static MovimentoPosicao Compra(int id, DateTime data, decimal quantidade, decimal preco, decimal custos = 0)
        => new() { Id = id, Data = data, Tipo = TipoOperacao.Compra, Quantidade = quantidade, PrecoUnitario = preco, Custos = custos };

    private static MovimentoPosicao Venda(int id, DateTime data, decimal quantidade, decimal preco)
        => new() { Id = id, Data = data, Tipo = TipoOperacao.Venda, Quantidade = quantidade, PrecoUnitario = preco };

    [Fact]
    public void PrimeiraCompra_PrecoMedioIncluiCustos()
    {
        var posicao = PosicaoCalculator.Calcular(new[]
        {
            Compra(1, new DateTime(2026, 1, 10), 100, 10m, custos: 10m)
        });

        Assert.Equal(100m, posicao.Quantidade);
        Assert.Equal(10.10m, posicao.PrecoMedio);
        Assert.Equal(1010m, posicao.CustoTotal);
    }

    [Fact]
    public void ComprasSeguidas_CalculaMediaPonderadaComCustos()
    {
        var posicao = PosicaoCalculator.Calcular(new[]
        {
            Compra(1, new DateTime(2026, 1, 10), 100, 10m, custos: 10m),
            Compra(2, new DateTime(2026, 2, 10), 100, 12m)
        });

        Assert.Equal(200m, posicao.Quantidade);
        Assert.Equal(11.05m, posicao.PrecoMedio);
        Assert.Equal(2210m, posicao.CustoTotal);
    }

    [Fact]
    public void VendaParcial_NaoAlteraPrecoMedio()
    {
        var antes = PosicaoCalculator.Calcular(new[]
        {
            Compra(1, new DateTime(2026, 1, 10), 100, 10m, custos: 10m),
            Compra(2, new DateTime(2026, 2, 10), 100, 12m)
        });

        var depois = PosicaoCalculator.Calcular(new[]
        {
            Compra(1, new DateTime(2026, 1, 10), 100, 10m, custos: 10m),
            Compra(2, new DateTime(2026, 2, 10), 100, 12m),
            Venda(3, new DateTime(2026, 3, 10), 50, 15m)
        });

        Assert.Equal(antes.PrecoMedio, depois.PrecoMedio);
        Assert.Equal(150m, depois.Quantidade);
        Assert.Equal(1657.50m, depois.CustoTotal);
    }

    [Fact]
    public void VendaTotalSeguidaDeNovaCompra_RecomecaPrecoMedio()
    {
        var posicao = PosicaoCalculator.Calcular(new[]
        {
            Compra(1, new DateTime(2026, 1, 10), 100, 10m, custos: 10m),
            Venda(2, new DateTime(2026, 2, 10), 100, 12m),
            Compra(3, new DateTime(2026, 3, 10), 10, 20m)
        });

        Assert.Equal(10m, posicao.Quantidade);
        Assert.Equal(20m, posicao.PrecoMedio);
        Assert.Equal(200m, posicao.CustoTotal);
    }

    [Fact]
    public void VendaAcimaDaPosicaoNaData_DeveFalhar()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => PosicaoCalculator.Calcular(new[]
        {
            Compra(1, new DateTime(2026, 1, 10), 100, 10m),
            Venda(2, new DateTime(2026, 2, 10), 101, 11m)
        }));

        Assert.Contains("excede a quantidade", ex.Message);
    }

    [Fact]
    public void CorrigirCompraAntiga_DeixaVendaPosteriorInvalida()
    {
        Assert.Throws<InvalidOperationException>(() => PosicaoCalculator.Calcular(new[]
        {
            Compra(1, new DateTime(2026, 1, 10), 50, 10m),
            Venda(2, new DateTime(2026, 2, 10), 80, 12m)
        }));
    }

    [Theory]
    [InlineData("PETR4", true)]
    [InlineData("petr4", true)]
    [InlineData("MXRF11", true)]
    [InlineData("PETR4F", true)]
    [InlineData("PETR", false)]
    [InlineData("PETRO4", false)]
    [InlineData("PETR4FF", false)]
    [InlineData("12345", false)]
    public void Ticker_SeguePadraoB3(string ticker, bool valido)
    {
        Assert.Equal(valido, TickerB3.EhValido(ticker));
    }
}
