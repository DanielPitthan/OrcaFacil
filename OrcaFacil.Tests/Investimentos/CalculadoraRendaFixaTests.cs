using OrcaFacil.Core.Enums;
using OrcaFacil.Core.RendaFixa;

namespace OrcaFacil.Tests.Investimentos;

public class CalculadoraRendaFixaTests
{
    [Fact]
    public void ResgateTotal_UsaValorDaOperacao_NaoQuantidadeVezesPreco()
    {
        var posicao = CalculadoraRendaFixa.Calcular(
        [
            Movimento(1, new DateTime(2023, 1, 20), TipoOperacaoRendaFixa.Aplicacao, 0.06m, 61.50m),
            Movimento(2, new DateTime(2023, 5, 26), TipoOperacaoRendaFixa.Resgate, 0.06m, 70.07m)
        ]);

        Assert.Equal(0m, posicao.Quantidade);
        Assert.Equal(0m, posicao.CustoTotal);
        var resultado = Assert.Single(posicao.Resgates);
        Assert.Equal(8.57m, resultado.Resultado);
    }

    [Fact]
    public void CustosDaAplicacao_NaoEntramNoCustoMedio()
    {
        var posicao = CalculadoraRendaFixa.Calcular(
        [
            Movimento(1, new DateTime(2026, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 10m, 100m, custos: 5m),
            Movimento(2, new DateTime(2026, 2, 10), TipoOperacaoRendaFixa.Resgate, 10m, 130m)
        ]);

        Assert.Equal(30m, Assert.Single(posicao.Resgates).Resultado);
    }

    [Fact]
    public void ResgateParcial_BaixaCustoProporcional_EMantemPrecoMedio()
    {
        var posicao = CalculadoraRendaFixa.Calcular(
        [
            Movimento(1, new DateTime(2026, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 10m, 100m),
            Movimento(2, new DateTime(2026, 2, 10), TipoOperacaoRendaFixa.Resgate, 4m, 50m)
        ]);

        Assert.Equal(10m, Assert.Single(posicao.Resgates).Resultado);
        Assert.Equal(6m, posicao.Quantidade);
        Assert.Equal(60m, posicao.CustoTotal);
        Assert.Equal(10m, posicao.CustoMedio);
    }

    [Fact]
    public void ResgateQueFechaAPosicao_AbsorveOCustoRestante()
    {
        var posicao = CalculadoraRendaFixa.Calcular(
        [
            Movimento(1, new DateTime(2026, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 3m, 10m),
            Movimento(2, new DateTime(2026, 2, 10), TipoOperacaoRendaFixa.Resgate, 1m, 4m),
            Movimento(3, new DateTime(2026, 3, 10), TipoOperacaoRendaFixa.Resgate, 2m, 8m)
        ]);

        Assert.Equal(0.67m, posicao.Resgates[0].Resultado);
        Assert.Equal(1.33m, posicao.Resgates[1].Resultado);
        Assert.Equal(2.00m, posicao.Resgates.Sum(r => r.Resultado));
        Assert.Equal(0m, posicao.Quantidade);
        Assert.Equal(0m, posicao.CustoTotal);
    }

    [Fact]
    public void IrRetidoECustos_ReduzemOResultado()
    {
        var posicao = CalculadoraRendaFixa.Calcular(
        [
            Movimento(1, new DateTime(2026, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 1m, 100m),
            Movimento(2, new DateTime(2026, 2, 10), TipoOperacaoRendaFixa.Resgate, 1m, 120m, custos: 2m, ir: 5m)
        ]);

        Assert.Equal(13m, Assert.Single(posicao.Resgates).Resultado);
    }

    [Fact]
    public void MesmoDia_AplicacaoVemAntesDoResgate_MesmoComIdMaior()
    {
        var posicao = CalculadoraRendaFixa.Calcular(
        [
            Movimento(1, new DateTime(2026, 4, 23), TipoOperacaoRendaFixa.Resgate, 10m, 12m),
            Movimento(2, new DateTime(2026, 4, 23), TipoOperacaoRendaFixa.Aplicacao, 10m, 10m)
        ]);

        Assert.Equal(2m, Assert.Single(posicao.Resgates).Resultado);
        Assert.Equal(0m, posicao.Quantidade);
    }

    [Fact]
    public void Arredondar_MeioExato_AfastaDeZero()
    {
        Assert.Equal(0.01m, CalculadoraRendaFixa.Arredondar(0.005m));
        Assert.Equal(-0.01m, CalculadoraRendaFixa.Arredondar(-0.005m));
        Assert.Equal(0m, CalculadoraRendaFixa.Arredondar(0.004m));
    }

    [Fact]
    public void ResgateSemPosicao_ERecusado()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => CalculadoraRendaFixa.Calcular(
        [
            Movimento(1, new DateTime(2026, 3, 11), TipoOperacaoRendaFixa.Resgate, 264024m, 2718.50m, codigo: "CDBC25AQGPU")
        ]));

        Assert.Contains("CDBC25AQGPU", ex.Message, StringComparison.Ordinal);
        Assert.Contains("excede a posição", ex.Message, StringComparison.Ordinal);
    }

    private static MovimentoRendaFixa Movimento(
        int id,
        DateTime data,
        TipoOperacaoRendaFixa tipo,
        decimal quantidade,
        decimal valor,
        decimal custos = 0m,
        decimal ir = 0m,
        string codigo = "CDB")
        => new()
        {
            Id = id,
            Data = data,
            Tipo = tipo,
            Quantidade = quantidade,
            Valor = valor,
            Custos = custos,
            IrRetido = ir,
            Codigo = codigo
        };
}
