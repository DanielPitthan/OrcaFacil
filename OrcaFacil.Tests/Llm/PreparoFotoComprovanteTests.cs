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
    public void MediaType_JpegChamadoPng_Png_Webp_Gif_HeicEDesconhecido()
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var gif = "GIF89a"u8.ToArray();
        var webp = new byte[12];
        "RIFF"u8.CopyTo(webp);
        "WEBP"u8.CopyTo(webp.AsSpan(8));
        var heic = CaixaHeic("heic");
        var desconhecido = new byte[] { 0x42, 0x4D, 0x00, 0x00 };

        Assert.Equal(PreparoFotoComprovante.MediaTypeJpeg, PreparoFotoComprovante.EscolherMediaType(jpeg, "cupom.png"));
        Assert.Equal(PreparoFotoComprovante.MediaTypePng, PreparoFotoComprovante.EscolherMediaType(png, "cupom.jpeg"));
        Assert.Equal(PreparoFotoComprovante.MediaTypeWebp, PreparoFotoComprovante.EscolherMediaType(webp, "cupom.png"));
        Assert.Equal(PreparoFotoComprovante.MediaTypeGif, PreparoFotoComprovante.EscolherMediaType(gif, "cupom.jpg"));
        Assert.True(PreparoFotoComprovante.EhHeic(heic));
        Assert.Null(PreparoFotoComprovante.EscolherMediaType(heic, "cupom.jpg"));
        Assert.False(PreparoFotoComprovante.EhHeic(desconhecido));
        Assert.Null(PreparoFotoComprovante.EscolherMediaType(desconhecido, "cupom.png"));
        Assert.Null(PreparoFotoComprovante.EscolherMediaType([]));

        var jpegPronto = PreparoFotoComprovante.PrepararEnvio(jpeg, "cupom.png", _ => throw new InvalidOperationException("JPEG não se converte."));
        Assert.Equal(PreparoFotoComprovante.MediaTypeJpeg, jpegPronto.MediaType);
        Assert.Equal(jpeg, jpegPronto.Bytes);
        Assert.True(jpegPronto.Enviar);
        Assert.Equal("Enviar", jpegPronto.ToString());

        Assert.Equal(PreparoFotoComprovante.MediaTypePng, PreparoFotoComprovante.PrepararEnvio(png, "nota.png", _ => null).MediaType);
        Assert.Equal(PreparoFotoComprovante.MediaTypeWebp, PreparoFotoComprovante.PrepararEnvio(webp, "nota.webp", _ => null).MediaType);
        Assert.Equal(PreparoFotoComprovante.MediaTypeGif, PreparoFotoComprovante.PrepararEnvio(gif, "nota.gif", _ => null).MediaType);

        var convertido = PreparoFotoComprovante.PrepararEnvio(heic, "foto.heic", _ => jpeg);
        Assert.Equal(PreparoFotoComprovante.MediaTypeJpeg, convertido.MediaType);
        Assert.Equal(jpeg, convertido.Bytes);
        Assert.True(convertido.Enviar);

        var heicSemConversao = PreparoFotoComprovante.PrepararEnvio(heic, "foto.heic", _ => null);
        Assert.False(heicSemConversao.Enviar);
        Assert.Null(heicSemConversao.MediaType);
        Assert.Equal("Recusar", heicSemConversao.ToString());

        var heicQueNaoVirouJpeg = PreparoFotoComprovante.PrepararEnvio(heic, "foto.heic", _ => png);
        Assert.False(heicQueNaoVirouJpeg.Enviar);

        var estranho = PreparoFotoComprovante.PrepararEnvio(desconhecido, "cupom.png", _ => null);
        Assert.False(estranho.Enviar);
        Assert.Null(estranho.MediaType);
        Assert.Null(estranho.Bytes);
    }

    [Fact]
    public void TetoAntesDoDecode_EVinteMega_ECabeSoAteEle()
    {
        Assert.Equal(20 * 1024 * 1024, PreparoFotoComprovante.TetoBytesAntesDoDecode);
        Assert.True(PreparoFotoComprovante.CabeAntesDoDecode(PreparoFotoComprovante.TetoBytesAntesDoDecode));
        Assert.False(PreparoFotoComprovante.CabeAntesDoDecode(PreparoFotoComprovante.TetoBytesAntesDoDecode + 1));
        Assert.False(PreparoFotoComprovante.CabeAntesDoDecode(-1));
        Assert.Contains("20 MB", MensagensLeituraComprovante.TextoFotoAcimaDoTeto, StringComparison.Ordinal);
        Assert.Contains("não foi enviada", MensagensLeituraComprovante.TextoFotoAcimaDoTeto, StringComparison.Ordinal);
        Assert.Equal("AcimaDoTeto", FotoComprovantePronta.AcimaDoTetoBruto().ToString());
    }

    [Fact]
    public void MediaType_Gif87aRiffAviEBufferCurto()
    {
        Assert.Equal(
            PreparoFotoComprovante.MediaTypeGif,
            PreparoFotoComprovante.EscolherMediaType("GIF87a"u8.ToArray(), "anim.png"));

        var avi = new byte[12];
        "RIFF"u8.CopyTo(avi);
        "AVI "u8.CopyTo(avi.AsSpan(8));
        Assert.Null(PreparoFotoComprovante.EscolherMediaType(avi, "filme.webp"));
        Assert.False(PreparoFotoComprovante.EhHeic(avi));
        Assert.Null(PreparoFotoComprovante.EscolherMediaType([0xFF, 0xD8], "curto.jpg"));
        Assert.Null(PreparoFotoComprovante.EscolherMediaType([0x89, 0x50], "curto.png"));
        Assert.Null(PreparoFotoComprovante.EscolherMediaType([], "vazio.png"));
    }

    private static byte[] CaixaHeic(string marca)
    {
        var bytes = new byte[12];
        bytes[3] = 16;
        "ftyp"u8.CopyTo(bytes.AsSpan(4));
        System.Text.Encoding.ASCII.GetBytes(marca).CopyTo(bytes.AsSpan(8));
        return bytes;
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

    [Fact]
    public void Amostras_SaoPotenciasDeDoisCrescentes()
    {
        Assert.Equal([1, 2, 4, 8, 16, 32, 64], PreparoFotoComprovante.Amostras);
    }

    [Theory]
    [InlineData(4000, 3000, 2576, 2)]
    [InlineData(3000, 4000, 2576, 2)]
    [InlineData(8000, 6000, 2576, 4)]
    [InlineData(4000, 3000, 2048, 2)]
    [InlineData(8000, 6000, 2048, 4)]
    [InlineData(2048, 1000, 2048, 1)]
    [InlineData(2049, 1000, 2048, 2)]
    [InlineData(5151, 1000, 2576, 2)]
    [InlineData(5152, 1000, 2576, 2)]
    [InlineData(5153, 1000, 2576, 4)]
    [InlineData(400, 300, 2576, 1)]
    [InlineData(100, 100, 512, 1)]
    [InlineData(200000, 100, 2576, 64)]
    public void CalcularAmostra_UsaAMenorPotenciaQueMantemOLadoDentroDoAlvo(
        int largura,
        int altura,
        int teto,
        int amostra)
    {
        var valor = PreparoFotoComprovante.CalcularAmostra(largura, altura, teto);
        var longo = Math.Max(largura, altura);
        var anterior = amostra / 2;

        Assert.Equal(amostra, valor);
        Assert.Contains(valor, PreparoFotoComprovante.Amostras);
        Assert.Equal(0, valor & (valor - 1));
        if (valor < PreparoFotoComprovante.Amostras[^1])
            Assert.True((long)longo <= (long)teto * valor);
        if (anterior >= 1)
            Assert.True((long)longo > (long)teto * anterior);
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
