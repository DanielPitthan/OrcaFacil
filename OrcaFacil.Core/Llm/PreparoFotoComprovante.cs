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

    /// <summary>
    /// Segunda tentativa quando o JPEG passa de <see cref="ClienteMensagens.TamanhoMaximoImagem"/>.
    /// Não amplia uma foto que já está neste lado ou abaixo.
    /// </summary>
    public const int LadoNovaTentativa = 2048;

    /// <summary>Qualidade JPEG da redução. No Android vira compressão 85.</summary>
    public const float QualidadeJpeg = 0.85f;

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
    /// Maior potência de dois em que o lado longo, depois da amostra, ainda fica no teto ou acima.
    /// O decode usa isso antes do redimensionamento exato. Foto menor que o teto fica em 1.
    /// </summary>
    public static int CalcularAmostra(int largura, int altura, int ladoMaximo)
    {
        if (largura <= 0)
            throw new ArgumentOutOfRangeException(nameof(largura));
        if (altura <= 0)
            throw new ArgumentOutOfRangeException(nameof(altura));
        if (ladoMaximo <= 0)
            throw new ArgumentOutOfRangeException(nameof(ladoMaximo));

        var longo = Math.Max(largura, altura);
        var amostra = 1;
        const int tetoAmostra = 64;
        while (amostra < tetoAmostra && longo / (amostra * 2) >= ladoMaximo)
            amostra *= 2;

        return amostra;
    }

    /// <summary>
    /// Até 5 MB, envia. Acima disso, e com lado longo maior que <see cref="LadoNovaTentativa"/>,
    /// pede outra codificação nesse lado e em <see cref="QualidadeJpeg"/>.
    /// Se já está nesse lado (ou menor) e ainda passa do limite, pede outra foto e não reenvia.
    /// <paramref name="ladoLongoProduzido"/> é o lado que a codificação gerou, não o teto pedido.
    /// </summary>
    public static DecisaoEnvioFoto DecidirEnvio(int bytes, int ladoLongoProduzido)
    {
        if (bytes < 0)
            throw new ArgumentOutOfRangeException(nameof(bytes));
        if (ladoLongoProduzido <= 0)
            throw new ArgumentOutOfRangeException(nameof(ladoLongoProduzido));

        if (bytes <= ClienteMensagens.TamanhoMaximoImagem)
            return new DecisaoEnvioFoto(ladoLongoProduzido, QualidadeJpeg, Enviar: true, PedirOutraFoto: false);

        if (ladoLongoProduzido > LadoNovaTentativa)
            return new DecisaoEnvioFoto(LadoNovaTentativa, QualidadeJpeg, Enviar: false, PedirOutraFoto: false);

        return new DecisaoEnvioFoto(LadoNovaTentativa, QualidadeJpeg, Enviar: false, PedirOutraFoto: true);
    }

    /// <summary>
    /// Escolhe o <c>media_type</c> pelos bytes. A extensão de <paramref name="nomeArquivo"/> não entra.
    /// JPEG, PNG, GIF e WEBP são os únicos tipos que a API aceita.
    /// Vazio, HEIC e o resto devolvem nulo: converta para JPEG ou não envie.
    /// </summary>
    public static string? EscolherMediaType(ReadOnlySpan<byte> bytes, string? nomeArquivo = null)
    {
        _ = nomeArquivo;
        if (EhJpeg(bytes))
            return MediaTypeJpeg;
        if (EhPng(bytes))
            return MediaTypePng;
        if (EhGif(bytes))
            return MediaTypeGif;
        if (EhWebp(bytes))
            return MediaTypeWebp;

        return null;
    }

    /// <summary>Caixa <c>ftyp</c> com marca HEIC/HEIF. Não é um tipo aceito pela API.</summary>
    public static bool EhHeic(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12 || !Marca(bytes, 4, "ftyp"u8))
            return false;

        if (MarcaHeic(bytes.Slice(8, 4)))
            return true;

        var fim = TamanhoCaixa(bytes);
        for (var i = 16; i + 4 <= fim; i += 4)
        {
            if (MarcaHeic(bytes.Slice(i, 4)))
                return true;
        }

        return false;
    }

    /// <summary>
    /// JPEG, PNG, GIF e WEBP seguem com o tipo dos bytes.
    /// HEIC e qualquer outro formato só seguem se <paramref name="converterParaJpeg"/> devolver JPEG.
    /// Sem JPEG válido, <see cref="PreparoMidia.Enviar"/> fica falso e a chamada não acontece.
    /// </summary>
    public static PreparoMidia PrepararEnvio(
        ReadOnlySpan<byte> bytes,
        string? nomeArquivo,
        Func<byte[], byte[]?> converterParaJpeg)
    {
        ArgumentNullException.ThrowIfNull(converterParaJpeg);

        var tipo = EscolherMediaType(bytes, nomeArquivo);
        if (tipo is not null)
            return new PreparoMidia(bytes.ToArray(), tipo);

        if (bytes.Length == 0)
            return new PreparoMidia(null, null);

        var convertido = converterParaJpeg(bytes.ToArray());
        if (convertido is null || EscolherMediaType(convertido) != MediaTypeJpeg)
            return new PreparoMidia(null, null);

        return new PreparoMidia(convertido, MediaTypeJpeg);
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

    private static bool EhJpeg(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;

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

    private static bool MarcaHeic(ReadOnlySpan<byte> marca) =>
        Igual(marca, "heic"u8) ||
        Igual(marca, "heix"u8) ||
        Igual(marca, "hevc"u8) ||
        Igual(marca, "hevx"u8) ||
        Igual(marca, "heim"u8) ||
        Igual(marca, "heis"u8) ||
        Igual(marca, "mif1"u8) ||
        Igual(marca, "msf1"u8);

    private static int TamanhoCaixa(ReadOnlySpan<byte> bytes)
    {
        var tamanho = ((int)bytes[0] << 24) | ((int)bytes[1] << 16) | ((int)bytes[2] << 8) | bytes[3];
        if (tamanho < 16)
            return 12;

        return Math.Min(bytes.Length, tamanho);
    }

    private static bool Marca(ReadOnlySpan<byte> bytes, int inicio, ReadOnlySpan<byte> texto) =>
        bytes.Length >= inicio + texto.Length && Igual(bytes.Slice(inicio, texto.Length), texto);

    private static bool Igual(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> texto)
    {
        if (bytes.Length != texto.Length)
            return false;

        for (var i = 0; i < texto.Length; i++)
        {
            var atual = bytes[i];
            if (atual is >= (byte)'A' and <= (byte)'Z')
                atual = (byte)(atual + 32);

            if (atual != texto[i])
                return false;
        }

        return true;
    }
}

/// <summary>
/// Bytes e <c>media_type</c> prontos para o provedor. Sem tipo, a chamada não sai.
/// <see cref="ToString"/> não leva os bytes.
/// </summary>
public readonly record struct PreparoMidia(byte[]? Bytes, string? MediaType)
{
    public bool Enviar => MediaType is not null && Bytes is { Length: > 0 };

    public override string ToString() => Enviar ? "Enviar" : "Recusar";
}

/// <summary>Largura e altura depois da redução. Não amplia.</summary>
public readonly record struct TamanhoAlvo(int Largura, int Altura)
{
    public int LadoLongo => Math.Max(Largura, Altura);

    public override string ToString() => "TamanhoAlvo";
}

/// <summary>
/// O que fazer com o JPEG já codificado. <see cref="ToString"/> não leva a quantidade de bytes.
/// </summary>
public readonly record struct DecisaoEnvioFoto(int LadoMaximo, float Qualidade, bool Enviar, bool PedirOutraFoto)
{
    public override string ToString() =>
        Enviar ? "Enviar" : PedirOutraFoto ? "PedirOutraFoto" : "ReduzirDeNovo";
}
