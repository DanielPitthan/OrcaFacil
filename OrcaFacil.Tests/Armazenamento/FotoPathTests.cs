using OrcaFacil.Core.Armazenamento;

namespace OrcaFacil.Tests.Armazenamento;

public class FotoPathTests
{
    [Theory]
    [InlineData("foto-20261017120000.jpg")]
    [InlineData("foto.jpg")]
    [InlineData("foto..jpg")]
    [InlineData("a")]
    public void NomeDeArquivo_EAceito(string nome)
    {
        Assert.True(FotoPath.EhSomenteNomeArquivo(nome));
        Assert.True(FotoPath.AceitaValorPersistido(nome));
        Assert.Equal(nome, FotoPath.ExigirNomeArquivo(nome));
        Assert.Equal(nome, FotoPath.ExtrairNomeArquivo(nome));
    }

    [Fact]
    public void Null_ESemFoto()
    {
        Assert.True(FotoPath.AceitaValorPersistido(null));
        Assert.False(FotoPath.EhSomenteNomeArquivo(null));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("foto.jpg ")]
    [InlineData(" foto.jpg")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("perfil/foto.jpg")]
    [InlineData(@"perfil\foto.jpg")]
    [InlineData("/data/user/0/com.orcafacil/files/perfil/foto.jpg")]
    [InlineData(@"C:\Users\Daniel\AppData\perfil\foto.jpg")]
    [InlineData("../foto.jpg")]
    [InlineData("foto/../segredo.jpg")]
    [InlineData("foto:1.jpg")]
    [InlineData("foto*.jpg")]
    [InlineData("foto?.jpg")]
    [InlineData("foto\"a.jpg")]
    [InlineData("foto<1>.jpg")]
    [InlineData("foto|1.jpg")]
    public void CaminhoOuNomeInvalido_ERecusado(string valor)
    {
        Assert.False(FotoPath.EhSomenteNomeArquivo(valor));
        Assert.False(FotoPath.AceitaValorPersistido(valor));
        Assert.Throws<ArgumentException>(() => FotoPath.ExigirNomeArquivo(valor));
    }

    [Fact]
    public void NomeLongoDemais_ERecusado()
    {
        var nome = new string('a', FotoPath.TamanhoMaximo + 1) + ".jpg";
        Assert.False(FotoPath.EhSomenteNomeArquivo(nome));
        Assert.Throws<ArgumentException>(() => FotoPath.ExigirNomeArquivo(nome));
    }

    [Fact]
    public void NomeNoLimite_EAceito()
    {
        var nome = new string('a', FotoPath.TamanhoMaximo);
        Assert.Equal(nome, FotoPath.ExigirNomeArquivo(nome));
    }

    [Fact]
    public void CaractereDeControle_ERecusado()
    {
        Assert.False(FotoPath.EhSomenteNomeArquivo("foto\n.jpg"));
        Assert.Throws<ArgumentException>(() => FotoPath.ExigirNomeArquivo("foto\n.jpg"));
    }

    [Theory]
    [InlineData("/data/user/0/com.orcafacil/files/perfil/foto-20261017.jpg", "foto-20261017.jpg")]
    [InlineData(@"C:\Users\Daniel\AppData\Local\perfil\foto-1.jpg", "foto-1.jpg")]
    [InlineData(@"C:/fotos\perfil/foto-2.jpg", "foto-2.jpg")]
    [InlineData("  /pasta/foto-3.jpg  ", "foto-3.jpg")]
    [InlineData("foto-4.jpg", "foto-4.jpg")]
    public void ExtrairNome_DeCaminhoLegado(string legado, string nome)
    {
        Assert.Equal(nome, FotoPath.ExtrairNomeArquivo(legado));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/pasta/")]
    [InlineData(@"C:\pasta\")]
    [InlineData("..")]
    [InlineData("pasta/..")]
    [InlineData("/")]
    public void ExtrairNome_SemArquivoNoFim_Lanca(string valor)
    {
        Assert.Throws<ArgumentException>(() => FotoPath.ExtrairNomeArquivo(valor));
    }
}
