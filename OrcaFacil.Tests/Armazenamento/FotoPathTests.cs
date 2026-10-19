using System.ComponentModel.DataAnnotations;
using System.Reflection;
using OrcaFacil.Core.Armazenamento;
using OrcaFacil.Core.Entities;

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
    public void TamanhoMaximo_AcompanhaOMaxLengthDaColuna()
    {
        var maximo = typeof(MembroFamilia).GetProperty(nameof(MembroFamilia.FotoPath))!
            .GetCustomAttribute<MaxLengthAttribute>()!.Length;
        Assert.Equal(260, maximo);
        Assert.Equal(FotoPath.TamanhoMaximo, maximo);
    }

    [Fact]
    public void CaminhoAbsolutoForaDaPasta_NaoAbre()
    {
        using var area = new AreaFoto();
        var segredo = Path.Combine(area.Raiz, "segredo.bin");
        File.WriteAllBytes(segredo, [1, 2, 3]);

        Assert.True(File.Exists(segredo));
        Assert.Null(FotoPath.CaminhoDentroDaPasta(area.Pasta, segredo));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(segredo));
    }

    [Fact]
    public void TravessiaComPontoPonto_NaoAbre()
    {
        using var area = new AreaFoto();
        var segredo = Path.Combine(area.Raiz, "segredo.bin");
        File.WriteAllBytes(segredo, [4, 5]);
        var travessia = Path.Combine(area.Pasta, "..", "segredo.bin");

        Assert.True(File.Exists(travessia));
        Assert.Null(FotoPath.CaminhoDentroDaPasta(area.Pasta, travessia));
        Assert.Null(FotoPath.CaminhoDentroDaPasta(area.Pasta, "perfil/../segredo.bin"));
    }

    [Fact]
    public void PastaIrmaComOMesmoPrefixo_NaoAbre()
    {
        using var area = new AreaFoto();
        var irma = Path.Combine(area.Raiz, "perfil-secreto");
        Directory.CreateDirectory(irma);
        var arquivo = Path.Combine(irma, "a.jpg");
        File.WriteAllBytes(arquivo, [7]);

        Assert.True(File.Exists(arquivo));
        Assert.Null(FotoPath.CaminhoDentroDaPasta(area.Pasta, arquivo));
    }

    [Fact]
    public void NomeSemDiretorio_UsaAPastaPerfil_ENaoAPastaCorrente()
    {
        using var area = new AreaFoto();
        var nome = "orcafacil-armadilha-" + Guid.NewGuid().ToString("N") + ".jpg";
        var armadilha = Path.Combine(Directory.GetCurrentDirectory(), nome);
        var verdadeira = Path.Combine(area.Pasta, nome);
        File.WriteAllBytes(armadilha, [1]);
        File.WriteAllBytes(verdadeira, [2]);
        try
        {
            var achou = FotoPath.CaminhoDentroDaPasta(area.Pasta, nome);
            Assert.Equal(Path.GetFullPath(verdadeira), achou);
            Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(achou!));
        }
        finally
        {
            File.Delete(armadilha);
        }
    }

    [Fact]
    public void CaminhoLegadoDentroDaPasta_AindaAbre()
    {
        using var area = new AreaFoto();
        var foto = Path.Combine(area.Pasta, "foto-20261017.jpg");
        File.WriteAllBytes(foto, [9]);

        Assert.Equal(Path.GetFullPath(foto), FotoPath.CaminhoDentroDaPasta(area.Pasta, foto));
        Assert.Equal(Path.GetFullPath(foto), FotoPath.CaminhoDentroDaPasta(area.Pasta, "foto-20261017.jpg"));
    }

    private sealed class AreaFoto : IDisposable
    {
        public AreaFoto()
        {
            Raiz = Directory.CreateTempSubdirectory("orcafacil-foto-").FullName;
            Pasta = Path.Combine(Raiz, "perfil");
            Directory.CreateDirectory(Pasta);
        }

        public string Raiz { get; }

        public string Pasta { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Raiz, recursive: true);
            }
            catch (IOException)
            {
            }
        }
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
