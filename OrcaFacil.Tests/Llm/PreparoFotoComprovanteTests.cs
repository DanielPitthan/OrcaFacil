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
    public void Avatar_MantemOLado512_ENaoUsaOTetoDoComprovante()
    {
        var fonte = File.ReadAllText(CaminhoFotoPerfil());

        Assert.Contains("private const float LadoMaximo = 512f;", fonte, StringComparison.Ordinal);
        Assert.Contains("ReduzirJpegAsync(origem, LadoMaximo, CancellationToken.None)", fonte, StringComparison.Ordinal);
        Assert.Contains("Reduzir(imagem, LadoMaximo)", fonte, StringComparison.Ordinal);
        Assert.Contains("FileSystem.AppDataDirectory, \"perfil\"", fonte, StringComparison.Ordinal);
        Assert.Contains("foto-{DateTime.Now:yyyyMMddHHmmss}.jpg", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("LadoMaximoComprovante", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("2576", fonte, StringComparison.Ordinal);
        Assert.Equal(512, PreparoFotoComprovante.CalcularTamanho(1800, 1200).LadoLongo);
    }

    private static string CaminhoFotoPerfil()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var caminho = Path.Combine(dir.FullName, "OrcaFacil", "Services", "FotoPerfilService.cs");
            if (File.Exists(caminho))
                return caminho;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar FotoPerfilService.cs.");
    }
}
