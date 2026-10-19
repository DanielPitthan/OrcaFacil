namespace OrcaFacil.Core.Llm;

/// <summary>
/// As oito orientações EXIF (tag 274). O valor é o código do arquivo, não um ângulo.
/// <see cref="DecoderAplicaExif"/> é falso: <c>BitmapFactory</c> e <c>PlatformImage.FromStream</c>
/// não giram sozinhos. Quem chama aplica esta transformação uma vez.
/// Se o bitmap decodificado já trocou a largura com a altura, <see cref="DecoderJaTrocouLados"/>
/// evita um segundo giro. Espelhamento não muda o tamanho, então o 2 e o 4 sempre se aplicam.
/// </summary>
public static class OrientacaoExif
{
    public const int Normal = 1;
    public const int FlipHorizontal = 2;
    public const int Rotate180 = 3;
    public const int FlipVertical = 4;
    public const int Transpose = 5;
    public const int Rotate90 = 6;
    public const int Transverse = 7;
    public const int Rotate270 = 8;

    public const bool DecoderAplicaExif = false;

    public static bool ExigeTransformacao(int orientacao) =>
        orientacao is >= FlipHorizontal and <= Rotate270;

    public static bool TrocaLados(int orientacao) =>
        orientacao is Transpose or Rotate90 or Transverse or Rotate270;

    public static (int Largura, int Altura) TamanhoSaida(int orientacao, int largura, int altura)
    {
        ExigirRetangulo(largura, altura);
        return TrocaLados(orientacao) ? (altura, largura) : (largura, altura);
    }

    /// <summary>
    /// Onde o pixel gravado <paramref name="x"/>, <paramref name="y"/> aparece depois da orientação.
    /// Origem no canto superior esquerdo. Não amplia e não corta.
    /// </summary>
    public static (int X, int Y) Aplicar(int orientacao, int x, int y, int largura, int altura)
    {
        ExigirRetangulo(largura, altura);
        if (x < 0 || y < 0 || x >= largura || y >= altura)
            throw new ArgumentOutOfRangeException(nameof(x));

        return orientacao switch
        {
            FlipHorizontal => (largura - 1 - x, y),
            Rotate180 => (largura - 1 - x, altura - 1 - y),
            FlipVertical => (x, altura - 1 - y),
            Transpose => (y, x),
            Rotate90 => (altura - 1 - y, x),
            Transverse => (altura - 1 - y, largura - 1 - x),
            Rotate270 => (y, largura - 1 - x),
            _ => (x, y)
        };
    }

    /// <summary>
    /// Verdadeiro só quando a orientação troca os lados, a foto não é quadrada
    /// e o bitmap decodificado já está de lado em relação ao arquivo cru.
    /// </summary>
    public static bool DecoderJaTrocouLados(
        int orientacao,
        int larguraBruta,
        int alturaBruta,
        int larguraDecodificada,
        int alturaDecodificada)
    {
        if (!TrocaLados(orientacao))
            return false;
        if (larguraBruta <= 0 || alturaBruta <= 0 || larguraDecodificada <= 0 || alturaDecodificada <= 0)
            return false;
        if (larguraBruta == alturaBruta)
            return false;

        var brutaLarga = larguraBruta > alturaBruta;
        var decodificadaLarga = larguraDecodificada > alturaDecodificada;
        return brutaLarga != decodificadaLarga;
    }

    private static void ExigirRetangulo(int largura, int altura)
    {
        if (largura <= 0)
            throw new ArgumentOutOfRangeException(nameof(largura));
        if (altura <= 0)
            throw new ArgumentOutOfRangeException(nameof(altura));
    }
}
