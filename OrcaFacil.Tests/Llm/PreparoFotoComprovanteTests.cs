using OrcaFacil.Core.Armazenamento;
using OrcaFacil.Core.Llm;

namespace OrcaFacil.Tests.Llm;

public class PreparoFotoComprovanteTests
{
    [Fact]
    public void TetoDoComprovante_EMaiorQueOAvatar_ENaoAmplia()
    {
        Assert.Equal(512, PreparoFotoComprovante.LadoMaximoAvatar);
        Assert.Equal(2576, PreparoFotoComprovante.LadoMaximoComprovante);
        Assert.True(PreparoFotoComprovante.LadoMaximoComprovante > PreparoFotoComprovante.LadoMaximoAvatar);

        var pequena = PreparoFotoComprovante.CalcularTamanho(800, 600, PreparoFotoComprovante.LadoMaximoComprovante);
        Assert.Equal(new TamanhoAlvo(800, 600), pequena);

        var miuda = PreparoFotoComprovante.CalcularTamanho(100, 50);
        Assert.Equal(new TamanhoAlvo(100, 50), miuda);
    }

    [Theory]
    [InlineData(4000, 3000, 2576, 2576, 1932)]
    [InlineData(3000, 4000, 2576, 1932, 2576)]
    [InlineData(2576, 1000, 2576, 2576, 1000)]
    [InlineData(2577, 1000, 2576, 2576, 1000)]
    [InlineData(2000, 1000, 512, 512, 256)]
    [InlineData(1000, 2000, 512, 256, 512)]
    [InlineData(3000, 3000, 2576, 2576, 2576)]
    [InlineData(10000, 1, 2576, 2576, 1)]
    public void CalcularTamanho_LimitaOLadoLongoSemPassarDoTeto(
        int largura,
        int altura,
        int teto,
        int larguraEsperada,
        int alturaEsperada)
    {
        var alvo = PreparoFotoComprovante.CalcularTamanho(largura, altura, teto);

        Assert.Equal(larguraEsperada, alvo.Largura);
        Assert.Equal(alturaEsperada, alvo.Altura);
        Assert.True(alvo.LadoLongo <= teto);
    }

    [Fact]
    public void CalcularTamanho_SemTetoExplicito_Usa512DoAvatar()
    {
        var alvo = PreparoFotoComprovante.CalcularTamanho(4000, 2000);

        Assert.Equal(512, alvo.LadoLongo);
        Assert.Equal(new TamanhoAlvo(512, 256), alvo);
        Assert.Equal("TamanhoAlvo", alvo.ToString());
    }

    [Theory]
    [InlineData(0, 10, 512)]
    [InlineData(10, 0, 512)]
    [InlineData(10, 10, 0)]
    [InlineData(-1, 10, 512)]
    public void CalcularTamanho_RecusaMedidaInvalida(int largura, int altura, int teto)
    {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
            PreparoFotoComprovante.CalcularTamanho(largura, altura, teto));
    }

    [Fact]
    public void EscolherMediaType_JpegPngGifWebp_EJpegQuandoNaoReconhece()
    {
        Assert.Equal(PreparoFotoComprovante.MediaTypeJpeg, PreparoFotoComprovante.EscolherMediaType([0xFF, 0xD8, 0xFF, 0xD9]));
        Assert.Equal(PreparoFotoComprovante.MediaTypeJpeg, PreparoFotoComprovante.EscolherMediaType([]));
        Assert.Equal(PreparoFotoComprovante.MediaTypeJpeg, PreparoFotoComprovante.EscolherMediaType([1, 2, 3, 4]));
        Assert.Equal(
            PreparoFotoComprovante.MediaTypePng,
            PreparoFotoComprovante.EscolherMediaType([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]));
        Assert.Equal(
            PreparoFotoComprovante.MediaTypeGif,
            PreparoFotoComprovante.EscolherMediaType("GIF89a"u8.ToArray()));
        var webp = new byte[12];
        "RIFF"u8.CopyTo(webp);
        "WEBP"u8.CopyTo(webp.AsSpan(8));
        Assert.Equal(PreparoFotoComprovante.MediaTypeWebp, PreparoFotoComprovante.EscolherMediaType(webp));
    }

    [Fact]
    public void Avatar_ForaDePerfilERecusado_ENomeSalvoAindaAbre()
    {
        var raiz = Directory.CreateTempSubdirectory("orca-foto-");
        try
        {
            var perfil = Path.Combine(raiz.FullName, "perfil");
            Directory.CreateDirectory(perfil);
            var foto = Path.Combine(perfil, "foto-20261017.jpg");
            File.WriteAllBytes(foto, [0xFF, 0xD8]);
            var fora = Path.Combine(raiz.FullName, "segredo.bin");
            File.WriteAllBytes(fora, [1]);

            Assert.Null(FotoPath.CaminhoDentroDaPasta(perfil, fora));
            Assert.Null(FotoPath.CaminhoDentroDaPasta(perfil, Path.Combine(raiz.FullName, "perfil2", "foto.jpg")));
            Assert.Equal(Path.GetFullPath(foto), FotoPath.CaminhoDentroDaPasta(perfil, "foto-20261017.jpg"));
            Assert.Equal(512, PreparoFotoComprovante.CalcularTamanho(1800, 1200).LadoLongo);
        }
        finally
        {
            raiz.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData(8000, 6000, 2576, 2)]
    [InlineData(5152, 1000, 2576, 2)]
    [InlineData(5151, 1000, 2576, 1)]
    [InlineData(400, 300, 2576, 1)]
    [InlineData(100, 100, 512, 1)]
    [InlineData(200000, 100, 2576, 64)]
    public void CalcularAmostra_UsaPotenciaDeDoisSemCairAbaixoDoTeto(
        int largura,
        int altura,
        int teto,
        int amostra)
    {
        var valor = PreparoFotoComprovante.CalcularAmostra(largura, altura, teto);

        Assert.Equal(amostra, valor);
        Assert.True(valor >= 1);
        Assert.Equal(0, valor & (valor - 1));
        if (Math.Max(largura, altura) > teto && valor < 64)
            Assert.True(Math.Max(largura, altura) / (valor * 2) < teto);
    }

    [Theory]
    [InlineData(0, 10, 512)]
    [InlineData(10, 0, 512)]
    [InlineData(10, 10, 0)]
    public void CalcularAmostra_RecusaMedidaInvalida(int largura, int altura, int teto)
    {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
            PreparoFotoComprovante.CalcularAmostra(largura, altura, teto));
    }

    [Fact]
    public void DecidirEnvio_Ate5MbEnvia_AcimaReduzPara2048_ENaoReenvia()
    {
        var limite = ClienteMensagens.TamanhoMaximoImagem;
        Assert.Equal(5 * 1024 * 1024, limite);
        Assert.Equal(2048, PreparoFotoComprovante.LadoNovaTentativa);
        Assert.Equal(0.85f, PreparoFotoComprovante.QualidadeJpeg);

        var cabe = PreparoFotoComprovante.DecidirEnvio(limite, 1800);
        Assert.True(cabe.Enviar);
        Assert.False(cabe.PedirOutraFoto);
        Assert.Equal(1800, cabe.LadoMaximo);
        Assert.Equal(0.85f, cabe.Qualidade);
        Assert.Equal("Enviar", cabe.ToString());

        var grande = limite + 1;
        var reduzir = PreparoFotoComprovante.DecidirEnvio(grande, 4000);
        Assert.False(reduzir.Enviar);
        Assert.False(reduzir.PedirOutraFoto);
        Assert.Equal(2048, reduzir.LadoMaximo);
        Assert.Equal(0.85f, reduzir.Qualidade);
        Assert.Equal(85, (int)Math.Round(reduzir.Qualidade * 100f, MidpointRounding.AwayFromZero));
        Assert.Equal("ReduzirDeNovo", reduzir.ToString());
        Assert.DoesNotContain(grande.ToString(System.Globalization.CultureInfo.InvariantCulture), reduzir.ToString(), StringComparison.Ordinal);

        var ainda = PreparoFotoComprovante.DecidirEnvio(grande, reduzir.LadoMaximo);
        Assert.False(ainda.Enviar);
        Assert.True(ainda.PedirOutraFoto);
        Assert.Equal("PedirOutraFoto", ainda.ToString());

        var noLimiteDoLado = PreparoFotoComprovante.DecidirEnvio(grande, 2048);
        Assert.True(noLimiteDoLado.PedirOutraFoto);
        Assert.False(noLimiteDoLado.Enviar);

        var umPixelAcima = PreparoFotoComprovante.DecidirEnvio(grande, 2049);
        Assert.False(umPixelAcima.PedirOutraFoto);
        Assert.Equal(2048, umPixelAcima.LadoMaximo);

        var jaPequena = PreparoFotoComprovante.DecidirEnvio(grande, 800);
        Assert.True(jaPequena.PedirOutraFoto);
        Assert.False(jaPequena.Enviar);
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(10, 0)]
    public void DecidirEnvio_RecusaMedidaInvalida(int bytes, int lado)
    {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => PreparoFotoComprovante.DecidirEnvio(bytes, lado));
    }
}
