namespace OrcaFacil.Core.Llm;

/// <summary>
/// Tamanho de saída e <c>media_type</c> da foto que vai para o provedor.
/// O avatar continua em <see cref="LadoMaximoAvatar"/>. O comprovante usa um teto maior.
/// </summary>
public static class PreparoFotoComprovante
{
    /// <summary>Lado longo da foto de perfil. O padrão de hoje.</summary>
    public const int LadoMaximoAvatar = 512;

    /// <summary>
    /// Teto do lado longo do comprovante. Maior que o avatar para o texto impresso continuar legível.
    /// </summary>
    public const int LadoMaximoComprovante = 2576;

    public const string MediaTypeJpeg = "image/jpeg";
    public const string MediaTypePng = "image/png";
    public const string MediaTypeGif = "image/gif";
    public const string MediaTypeWebp = "image/webp";

    /// <summary>
    /// Cabe no retângulo sem ampliar. O lado longo do resultado não passa de <paramref name="ladoMaximo"/>.
    /// Sem argumento, o teto é o do avatar.
    /// </summary>
    public static TamanhoAlvo CalcularTamanho(int largura, int altura, int ladoMaximo = LadoMaximoAvatar)
    {
        if (largura <= 0)
            throw new ArgumentOutOfRangeException(nameof(largura));
        if (altura <= 0)
            throw new ArgumentOutOfRangeException(nameof(altura));
        if (ladoMaximo <= 0)
            throw new ArgumentOutOfRangeException(nameof(ladoMaximo));

        var longo = Math.Max(largura, altura);
        if (longo <= ladoMaximo)
            return new TamanhoAlvo(largura, altura);

        if (largura >= altura)
            return new TamanhoAlvo(ladoMaximo, LadoMenor(altura, largura, ladoMaximo));

        return new TamanhoAlvo(LadoMenor(largura, altura, ladoMaximo), ladoMaximo);
    }

    /// <summary>
    /// Escolhe o <c>media_type</c> pelos bytes. JPEG, arquivo vazio e formato fora da lista
    /// viram <see cref="MediaTypeJpeg"/>, que é o que a redução grava.
    /// </summary>
    public static string EscolherMediaType(ReadOnlySpan<byte> bytes)
    {
        if (EhPng(bytes))
            return MediaTypePng;
        if (EhGif(bytes))
            return MediaTypeGif;
        if (EhWebp(bytes))
            return MediaTypeWebp;

        return MediaTypeJpeg;
    }

    private static int LadoMenor(int menor, int maior, int teto)
    {
        var valor = (int)Math.Round(menor * (double)teto / maior, MidpointRounding.AwayFromZero);
        if (valor < 1)
            return 1;
        if (valor > teto)
            return teto;

        return valor;
    }

    private static bool EhPng(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 8 &&
        bytes[0] == 0x89 &&
        bytes[1] == 0x50 &&
        bytes[2] == 0x4E &&
        bytes[3] == 0x47 &&
        bytes[4] == 0x0D &&
        bytes[5] == 0x0A &&
        bytes[6] == 0x1A &&
        bytes[7] == 0x0A;

    private static bool EhGif(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 6 &&
        bytes[0] == (byte)'G' &&
        bytes[1] == (byte)'I' &&
        bytes[2] == (byte)'F' &&
        bytes[3] == (byte)'8' &&
        (bytes[4] == (byte)'7' || bytes[4] == (byte)'9') &&
        bytes[5] == (byte)'a';

    private static bool EhWebp(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 12 &&
        bytes[0] == (byte)'R' &&
        bytes[1] == (byte)'I' &&
        bytes[2] == (byte)'F' &&
        bytes[3] == (byte)'F' &&
        bytes[8] == (byte)'W' &&
        bytes[9] == (byte)'E' &&
        bytes[10] == (byte)'B' &&
        bytes[11] == (byte)'P';
}

/// <summary>Largura e altura depois da redução. Não amplia.</summary>
public readonly record struct TamanhoAlvo(int Largura, int Altura)
{
    public int LadoLongo => Math.Max(Largura, Altura);

    public override string ToString() => "TamanhoAlvo";
}
