using OrcaFacil.Core;
using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Tests;

public class OrdemPorDataEIdTests
{
    [Fact]
    public void DataVemAntes_ENoMesmoDiaOIdDesempata_IdZeroPorUltimo()
    {
        var itens = new[]
        {
            (Data: new DateTime(2026, 1, 10, 18, 0, 0), Id: 0),
            (Data: new DateTime(2026, 1, 11, 8, 0, 0), Id: 1),
            (Data: new DateTime(2026, 1, 10, 8, 0, 0), Id: 4),
            (Data: new DateTime(2026, 1, 10, 23, 0, 0), Id: 2)
        };

        var ordem = OrdemPorDataEId.Ordenar(itens, item => item.Data, item => item.Id).Select(item => item.Id).ToList();

        Assert.Equal([2, 4, 0, 1], ordem);
        Assert.True(OrdemPorDataEId.Comparar(itens[2].Data, 4, itens[0].Data, 0) < 0);
        Assert.Equal(int.MaxValue, OrdemPorDataEId.Chave(0));
    }

    [Fact]
    public void Calculadora_IgnoraAHora_ESegueOId()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => PosicaoCalculator.Calcular(new[]
        {
            Movimento(2, new DateTime(2026, 1, 10, 8, 0, 0), TipoOperacao.Compra),
            Movimento(1, new DateTime(2026, 1, 10, 18, 0, 0), TipoOperacao.Venda)
        }));

        Assert.Contains("excede a quantidade", ex.Message);

        var posicao = PosicaoCalculator.Calcular(new[]
        {
            Movimento(1, new DateTime(2026, 1, 10, 18, 0, 0), TipoOperacao.Compra),
            Movimento(2, new DateTime(2026, 1, 10, 8, 0, 0), TipoOperacao.Venda)
        });
        Assert.Equal(0m, posicao.Quantidade);
    }

    private static MovimentoPosicao Movimento(int id, DateTime data, TipoOperacao tipo)
        => new()
        {
            Id = id,
            Data = data,
            Tipo = tipo,
            Quantidade = 1m,
            PrecoUnitario = 10m
        };
}
