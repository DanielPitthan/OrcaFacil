using OrcaFacil.Core.Llm;

namespace OrcaFacil.Tests.Llm;

public class OrientacaoExifTests
{
    [Theory]
    [InlineData(OrientacaoExif.Normal, 1, 0, 4, 3, 1, 0, 4, 3)]
    [InlineData(OrientacaoExif.FlipHorizontal, 1, 0, 4, 3, 2, 0, 4, 3)]
    [InlineData(OrientacaoExif.Rotate180, 1, 0, 4, 3, 2, 2, 4, 3)]
    [InlineData(OrientacaoExif.FlipVertical, 1, 0, 4, 3, 1, 2, 4, 3)]
    [InlineData(OrientacaoExif.Transpose, 1, 0, 4, 3, 0, 1, 3, 4)]
    [InlineData(OrientacaoExif.Rotate90, 1, 0, 4, 3, 2, 1, 3, 4)]
    [InlineData(OrientacaoExif.Transverse, 1, 0, 4, 3, 2, 2, 3, 4)]
    [InlineData(OrientacaoExif.Rotate270, 1, 0, 4, 3, 0, 2, 3, 4)]
    public void CadaOrientacao_MoveOCantoSemTrocarDeNovo(
        int orientacao,
        int x,
        int y,
        int largura,
        int altura,
        int xEsperado,
        int yEsperado,
        int larguraSaida,
        int alturaSaida)
    {
        var pixel = OrientacaoExif.Aplicar(orientacao, x, y, largura, altura);
        var tamanho = OrientacaoExif.TamanhoSaida(orientacao, largura, altura);

        Assert.Equal((xEsperado, yEsperado), pixel);
        Assert.Equal((larguraSaida, alturaSaida), tamanho);
    }

    [Theory]
    [InlineData(OrientacaoExif.Normal)]
    [InlineData(OrientacaoExif.FlipHorizontal)]
    [InlineData(OrientacaoExif.Rotate180)]
    [InlineData(OrientacaoExif.FlipVertical)]
    [InlineData(OrientacaoExif.Transpose)]
    [InlineData(OrientacaoExif.Rotate90)]
    [InlineData(OrientacaoExif.Transverse)]
    [InlineData(OrientacaoExif.Rotate270)]
    public void Aplicar_EUmaPermutacaoDosPixels(int orientacao)
    {
        const int largura = 5;
        const int altura = 4;
        var (saidaLargura, saidaAltura) = OrientacaoExif.TamanhoSaida(orientacao, largura, altura);
        var vistos = new HashSet<(int X, int Y)>();

        for (var y = 0; y < altura; y++)
        {
            for (var x = 0; x < largura; x++)
            {
                var pixel = OrientacaoExif.Aplicar(orientacao, x, y, largura, altura);
                Assert.InRange(pixel.X, 0, saidaLargura - 1);
                Assert.InRange(pixel.Y, 0, saidaAltura - 1);
                Assert.True(vistos.Add(pixel));
            }
        }

        Assert.Equal(largura * altura, vistos.Count);
        Assert.False(OrientacaoExif.DecoderAplicaExif);
    }

    [Theory]
    [InlineData(OrientacaoExif.Rotate90, 4000, 3000, 3000, 4000, true)]
    [InlineData(OrientacaoExif.Rotate270, 4000, 3000, 3000, 4000, true)]
    [InlineData(OrientacaoExif.Transpose, 4000, 3000, 1500, 2000, true)]
    [InlineData(OrientacaoExif.Rotate90, 4000, 3000, 2000, 1500, false)]
    [InlineData(OrientacaoExif.Rotate90, 1000, 1000, 500, 800, false)]
    [InlineData(OrientacaoExif.FlipHorizontal, 4000, 3000, 3000, 4000, false)]
    [InlineData(OrientacaoExif.FlipVertical, 4000, 3000, 3000, 4000, false)]
    [InlineData(OrientacaoExif.Rotate180, 4000, 3000, 3000, 4000, false)]
    [InlineData(OrientacaoExif.Normal, 4000, 3000, 3000, 4000, false)]
    public void DecoderJaTrocouLados_SoQuandoOGiroJaApareceNaMedida(
        int orientacao,
        int larguraBruta,
        int alturaBruta,
        int larguraDecodificada,
        int alturaDecodificada,
        bool esperado)
    {
        Assert.Equal(
            esperado,
            OrientacaoExif.DecoderJaTrocouLados(
                orientacao,
                larguraBruta,
                alturaBruta,
                larguraDecodificada,
                alturaDecodificada));
    }

    [Fact]
    public void Tabela_CobreOsOitoCodigosNaOrdem()
    {
        Assert.Equal(Enumerable.Range(1, 8), OrientacaoExif.Tabela.Select(regra => regra.Codigo));
    }

    [Theory]
    [InlineData(OrientacaoExif.Normal, false, false)]
    [InlineData(OrientacaoExif.FlipHorizontal, false, true)]
    [InlineData(OrientacaoExif.Rotate180, false, true)]
    [InlineData(OrientacaoExif.FlipVertical, false, true)]
    [InlineData(OrientacaoExif.Transpose, true, true)]
    [InlineData(OrientacaoExif.Rotate90, true, true)]
    [InlineData(OrientacaoExif.Transverse, true, true)]
    [InlineData(OrientacaoExif.Rotate270, true, true)]
    public void Tabela_DefineTrocaDeLadosESeAplica(int codigo, bool trocaLados, bool aplica)
    {
        var regra = Assert.Single(OrientacaoExif.Tabela, item => item.Codigo == codigo);

        Assert.Equal(trocaLados, regra.TrocaLados);
        Assert.Equal(aplica, regra.Aplica);
        Assert.Equal(trocaLados, OrientacaoExif.TrocaLados(codigo));
        Assert.Equal(aplica, OrientacaoExif.ExigeTransformacao(codigo));
        Assert.Equal(aplica, OrientacaoExif.DeveAplicar(codigo, decoderJaAplicou: false));
        Assert.False(OrientacaoExif.DeveAplicar(codigo, decoderJaAplicou: true));
        Assert.Equal(aplica ? codigo : OrientacaoExif.Normal, OrientacaoExif.OrientacaoAAplicar(codigo, decoderJaAplicou: false));
        Assert.Equal(OrientacaoExif.Normal, OrientacaoExif.OrientacaoAAplicar(codigo, decoderJaAplicou: true));
    }

    [Theory]
    [InlineData(OrientacaoExif.FlipHorizontal)]
    [InlineData(OrientacaoExif.Rotate180)]
    [InlineData(OrientacaoExif.FlipVertical)]
    [InlineData(OrientacaoExif.Rotate90)]
    public void DecoderDeclarado_NaoGiraDeNovo_MesmoSemTrocarOsLados(int codigo)
    {
        Assert.True(OrientacaoExif.DecoderJaAplicou(codigo, true, 4000, 3000, 4000, 3000));
        Assert.Equal(
            OrientacaoExif.Normal,
            OrientacaoExif.OrientacaoAAplicar(
                codigo,
                OrientacaoExif.DecoderJaAplicou(codigo, true, 4000, 3000, 4000, 3000)));
    }

    [Fact]
    public void MedidasIguais_NaoProvamQueODecoderGirou()
    {
        Assert.False(OrientacaoExif.DecoderJaAplicou(OrientacaoExif.Rotate90, false, 4000, 3000, 4000, 3000));
        Assert.False(OrientacaoExif.DecoderJaAplicou(OrientacaoExif.FlipHorizontal, false, 4000, 3000, 4000, 3000));
        Assert.Equal(
            OrientacaoExif.Rotate90,
            OrientacaoExif.OrientacaoAAplicar(OrientacaoExif.Rotate90, decoderJaAplicou: false));
        Assert.Equal(
            OrientacaoExif.FlipHorizontal,
            OrientacaoExif.OrientacaoAAplicar(OrientacaoExif.FlipHorizontal, decoderJaAplicou: false));
    }

    [Fact]
    public void LadosTrocados_SignificamQueODecoderJaAplicouOGiro()
    {
        Assert.True(OrientacaoExif.DecoderJaAplicou(OrientacaoExif.Rotate90, false, 4000, 3000, 3000, 4000));
        Assert.Equal(
            OrientacaoExif.Normal,
            OrientacaoExif.OrientacaoAAplicar(
                OrientacaoExif.Rotate90,
                OrientacaoExif.DecoderJaAplicou(OrientacaoExif.Rotate90, false, 4000, 3000, 3000, 4000)));
        Assert.False(OrientacaoExif.DecoderJaAplicou(OrientacaoExif.FlipHorizontal, false, 4000, 3000, 3000, 4000));
    }

    [Fact]
    public void Espelhamento_NaoTrocaLados_EGiroSim()
    {
        Assert.False(OrientacaoExif.TrocaLados(OrientacaoExif.FlipHorizontal));
        Assert.False(OrientacaoExif.TrocaLados(OrientacaoExif.FlipVertical));
        Assert.False(OrientacaoExif.TrocaLados(OrientacaoExif.Rotate180));
        Assert.True(OrientacaoExif.TrocaLados(OrientacaoExif.Rotate90));
        Assert.True(OrientacaoExif.TrocaLados(OrientacaoExif.Rotate270));
        Assert.True(OrientacaoExif.TrocaLados(OrientacaoExif.Transpose));
        Assert.True(OrientacaoExif.TrocaLados(OrientacaoExif.Transverse));
        Assert.False(OrientacaoExif.ExigeTransformacao(OrientacaoExif.Normal));
        Assert.True(OrientacaoExif.ExigeTransformacao(OrientacaoExif.Rotate270));
    }
}
