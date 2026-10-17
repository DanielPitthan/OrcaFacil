using System.Globalization;
using OrcaFacil.Core.Lancamentos;

namespace OrcaFacil.Tests.Lancamentos;

public class FormatadorMoedaTests
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    [Fact]
    public void ItemDaFatura_EstornoLevaCreditoECompraFicaSemSinal()
    {
        Assert.Equal("+ R$ 100,00", FormatadorMoeda.FormatarItemFatura(100m, ehEstorno: true, PtBr));
        Assert.Equal("R$ 100,00", FormatadorMoeda.FormatarItemFatura(100m, ehEstorno: false, PtBr));
        Assert.Equal("− R$ 4.300,00", FormatadorMoeda.FormatarLancamento(4300m, receita: false, PtBr));
        Assert.Equal("+ R$ 50,00", FormatadorMoeda.FormatarLancamento(50m, receita: true, PtBr));
    }

    [Fact]
    public void SaldoELiquido_SeparamOSinalDoSimbolo()
    {
        Assert.Equal("− R$ 40,00", FormatadorMoeda.FormatarSaldo(-40m, PtBr));
        Assert.Equal("R$ 40,00", FormatadorMoeda.FormatarSaldo(40m, PtBr));
        Assert.Equal("R$ 0,00", FormatadorMoeda.FormatarSaldo(0m, PtBr));
        Assert.Equal("+ R$ 50,00", FormatadorMoeda.FormatarLiquido(50m, PtBr));
        Assert.Equal("− R$ 50,00", FormatadorMoeda.FormatarLiquido(-50m, PtBr));
        Assert.Equal("R$ 0,00", FormatadorMoeda.FormatarLiquido(0m, PtBr));
    }

    [Fact]
    public void MagnitudeNegativa_NaoColaHifenNemDuplicaOSinal()
    {
        var textos = new[]
        {
            FormatadorMoeda.Formatar(-100m, SinalMoeda.Credito, PtBr),
            FormatadorMoeda.Formatar(-100m, SinalMoeda.Saida, PtBr),
            FormatadorMoeda.Formatar(-100m, SinalMoeda.Neutro, PtBr),
            FormatadorMoeda.FormatarItemFatura(-100m, ehEstorno: true, PtBr),
            FormatadorMoeda.FormatarSaldo(-100m, PtBr)
        };

        Assert.Equal("+ R$ 100,00", textos[0]);
        Assert.Equal("− R$ 100,00", textos[1]);
        Assert.Equal("R$ 100,00", textos[2]);
        Assert.All(textos, texto =>
        {
            Assert.DoesNotContain("-R$", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("− -", texto, StringComparison.Ordinal);
        });
    }
}
