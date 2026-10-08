using OrcaFacil.Core.Entities;
using Xunit;

namespace OrcaFacil.Tests.Entities;

public class MetaTests
{
    [Theory]
    [InlineData(0, 1000, 0)]
    [InlineData(500, 1000, 50)]
    [InlineData(1000, 1000, 100)]
    [InlineData(1500, 1000, 100)] // nunca deve passar de 100%, mesmo com aporte acima do alvo
    public void PercentualConcluido_DeveCalcularCorretamenteELimitarA100(decimal atual, decimal alvo, decimal esperado)
    {
        var meta = new Meta { ValorAtual = atual, ValorAlvo = alvo };

        Assert.Equal(esperado, meta.PercentualConcluido);
    }

    [Fact]
    public void PercentualConcluido_ComAlvoZero_DeveRetornarZero()
    {
        var meta = new Meta { ValorAtual = 100m, ValorAlvo = 0m };

        Assert.Equal(0m, meta.PercentualConcluido);
    }
}
