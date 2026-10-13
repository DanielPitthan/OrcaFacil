using OrcaFacil.Core.Numerico;

namespace OrcaFacil.Tests.Numerico;

public class MascaraDireitaEsquerdaTests
{
    [Fact]
    public void DigitosEntramDaDireitaParaAEsquerda_ComCentavosFixos()
    {
        var digitos = "";
        foreach (var tecla in "12345")
        {
            digitos = MascaraDireitaEsquerda.AplicarEntrada(digitos, tecla.ToString(), casasDecimais: 2, maximo: null, substituir: false);
        }

        Assert.Equal("12345", digitos);
        Assert.Equal(123.45m, MascaraDireitaEsquerda.ValorDe(digitos, 2));
        Assert.Equal("123,45", MascaraDireitaEsquerda.Formatar(123.45m, 2));
    }

    [Theory]
    [InlineData("", "1", "0,01")]
    [InlineData("1", "2", "0,12")]
    [InlineData("12", "3", "1,23")]
    [InlineData("123", "4", "12,34")]
    [InlineData("1234", "5", "123,45")]
    [InlineData("12345", "6", "1.234,56")]
    public void CadaTeclaDeslocaOsCentavos(string antes, string tecla, string textoEsperado)
    {
        var digitos = MascaraDireitaEsquerda.AplicarEntrada(antes, tecla, 2, null, substituir: false);
        var texto = MascaraDireitaEsquerda.Formatar(MascaraDireitaEsquerda.ValorDe(digitos, 2), 2);
        Assert.Equal(textoEsperado, texto);
    }

    [Fact]
    public void BackspaceRemoveOUltimoDigito()
    {
        var digitos = "123456";
        digitos = MascaraDireitaEsquerda.Apagar(digitos);

        Assert.Equal(123.45m, MascaraDireitaEsquerda.ValorDe(digitos, 2));
        Assert.Equal("123,45", MascaraDireitaEsquerda.Formatar(MascaraDireitaEsquerda.ValorDe(digitos, 2), 2));
    }

    [Theory]
    [InlineData("R$ 1.234,56", "123456")]
    [InlineData("1234.56", "123456")]
    [InlineData("10,00", "1000")]
    [InlineData("abc", "")]
    [InlineData("0005", "5")]
    public void TextoColadoViraDigitosSemSeparadores(string texto, string digitos)
    {
        Assert.Equal(digitos, MascaraDireitaEsquerda.SomenteDigitos(texto));
        Assert.Equal(digitos, MascaraDireitaEsquerda.AplicarEntrada("999", texto, 2, null, substituir: true));
    }

    [Fact]
    public void NaoUltrapassaOValorMaximo()
    {
        var digitos = MascaraDireitaEsquerda.AplicarEntrada("100", "1", casasDecimais: 0, maximo: 100m, substituir: false);
        Assert.Equal("100", digitos);
        Assert.Equal("100", MascaraDireitaEsquerda.Formatar(100m, 0));
    }

    [Fact]
    public void ZeroADireitaDeslocaOsCentavos_ZeroAEsquerdaNaoAltera()
    {
        var digitos = MascaraDireitaEsquerda.AplicarEntrada("", "0", 2, null, false);
        Assert.Equal("", digitos);
        Assert.Equal("0,00", MascaraDireitaEsquerda.Formatar(0m, 2));

        digitos = MascaraDireitaEsquerda.AplicarEntrada("123", "0", 2, null, false);
        Assert.Equal("12,30", MascaraDireitaEsquerda.Formatar(MascaraDireitaEsquerda.ValorDe(digitos, 2), 2));
    }

    [Fact]
    public void InteiroUsaSeparadorDeMilhar()
    {
        var digitos = "";
        foreach (var tecla in "1500")
            digitos = MascaraDireitaEsquerda.AplicarEntrada(digitos, tecla.ToString(), 0, null, false);

        Assert.Equal(1500m, MascaraDireitaEsquerda.ValorDe(digitos, 0));
        Assert.Equal("1.500", MascaraDireitaEsquerda.Formatar(1500m, 0));
    }

    [Fact]
    public void ValorExistenteReabreNaMesmaEscala()
    {
        Assert.Equal("123456", MascaraDireitaEsquerda.DigitosDe(1234.56m, 2));
        Assert.Equal("100", MascaraDireitaEsquerda.DigitosDe(100m, 0));
        Assert.Equal("", MascaraDireitaEsquerda.DigitosDe(0m, 2));
        Assert.Equal("0,00", MascaraDireitaEsquerda.Formatar(0m, 2));
    }

    [Fact]
    public void ConverteParaDecimalEDecimalAnulavel()
    {
        Assert.Equal(0m, MascaraDireitaEsquerda.Para<decimal>(null));
        Assert.Equal(12.34m, MascaraDireitaEsquerda.Para<decimal>(12.34m));
        Assert.Null(MascaraDireitaEsquerda.Para<decimal?>(null));
        Assert.Equal(12.34m, MascaraDireitaEsquerda.Para<decimal?>(12.34m));
    }
}
