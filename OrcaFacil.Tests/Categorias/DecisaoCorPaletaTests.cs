using OrcaFacil.Core.Categorias;

namespace OrcaFacil.Tests.Categorias;

public class DecisaoCorPaletaTests
{
    private const string Original = "#A2B94F";
    private const string Arrastada = "#112233";

    [Fact]
    public void OkGravaACorArrastada()
    {
        var cor = DecisaoCorPaleta.Resolver(Original, Arrastada, confirmou: true);

        Assert.Equal(Arrastada, cor);
    }

    [Theory]
    [InlineData(false, Arrastada)]
    [InlineData(false, null)]
    [InlineData(false, "")]
    [InlineData(false, "   ")]
    [InlineData(true, null)]
    [InlineData(true, "")]
    [InlineData(true, "   ")]
    public void DispensaMantemAOriginal(bool confirmou, string? escolhida)
    {
        var cor = DecisaoCorPaleta.Resolver(Original, escolhida, confirmou);

        Assert.Equal(Original, cor);
    }
}
