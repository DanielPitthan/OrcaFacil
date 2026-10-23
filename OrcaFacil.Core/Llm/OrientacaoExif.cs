namespace OrcaFacil.Core.Llm;

/// <summary>
/// Uma linha da tag 274. <see cref="Aplica"/> vale quando o decoder ainda não girou a foto.
/// </summary>
public readonly record struct RegraOrientacaoExif(int Codigo, bool TrocaLados, bool Aplica);

/// <summary>
/// As oito orientações EXIF (tag 274). O valor é o código do arquivo, não um ângulo.
/// <see cref="DecoderAplicaExif"/> é falso: <c>BitmapFactory</c> e <c>PlatformImage.FromStream</c>
/// não giram sozinhos. Quem chama lê o código uma vez e usa <see cref="OrientacaoAAplicar"/>.
/// Se o decoder já aplicou, o resultado é <see cref="Normal"/> — também nos códigos 2, 3 e 4,
/// que não trocam os lados e por isso não aparecem em <see cref="DecoderJaTrocouLados"/>.
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

    private static readonly RegraOrientacaoExif[] Linhas =
    [
        new(Normal, false, false),
        new(FlipHorizontal, false, true),
        new(Rotate180, false, true),
        new(FlipVertical, false, true),
        new(Transpose, true, true),
        new(Rotate90, true, true),
        new(Transverse, true, true),
        new(Rotate270, true, true)
    ];

    public static IReadOnlyList<RegraOrientacaoExif> Tabela => Linhas;

    public static bool ExigeTransformacao(int orientacao) =>
        Regra(orientacao)?.Aplica == true;

    public static bool TrocaLados(int orientacao) =>
        Regra(orientacao)?.TrocaLados == true;

    /// <summary>
    /// Falso quando o decoder já aplicou a orientação lida, inclusive nos códigos que não trocam os lados.
    /// </summary>
    public static bool DeveAplicar(int orientacao, bool decoderJaAplicou) =>
        !decoderJaAplicou && ExigeTransformacao(orientacao);

    /// <summary>
    /// Código que ainda falta aplicar. A orientação entra uma vez: se o decoder já girou, devolve <see cref="Normal"/>.
    /// </summary>
    public static int OrientacaoAAplicar(int orientacaoLida, bool decoderJaAplicou) =>
        DeveAplicar(orientacaoLida, decoderJaAplicou) ? orientacaoLida : Normal;

    /// <summary>
    /// O decoder declarou que aplica o EXIF, ou as medidas mostram que os lados já trocaram.
    /// Medidas iguais não são sinal: espelhamento e 180° não mudam o tamanho.
    /// </summary>
    public static bool DecoderJaAplicou(
        int orientacao,
        bool decoderDeclarado,
        int larguraBruta,
        int alturaBruta,
        int larguraDecodificada,
        int alturaDecodificada)
    {
        if (decoderDeclarado)
            return true;

        return DecoderJaTrocouLados(
            orientacao,
            larguraBruta,
            alturaBruta,
            larguraDecodificada,
            alturaDecodificada);
    }

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

    private static RegraOrientacaoExif? Regra(int codigo)
    {
        foreach (var regra in Linhas)
        {
            if (regra.Codigo == codigo)
                return regra;
        }

        return null;
    }

    private static void ExigirRetangulo(int largura, int altura)
    {
        if (largura <= 0)
            throw new ArgumentOutOfRangeException(nameof(largura));
        if (altura <= 0)
            throw new ArgumentOutOfRangeException(nameof(altura));
    }
}
