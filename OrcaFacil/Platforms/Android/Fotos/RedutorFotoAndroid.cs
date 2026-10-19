using OrcaFacil.Core.Llm;

namespace OrcaFacil.Platforms.Android.Fotos;

/// <summary>
/// Reduz a foto no Android com amostra antes do teto, uma transformação EXIF e descarte dos bitmaps.
/// O decoder não aplica EXIF (<see cref="OrientacaoExif.DecoderAplicaExif"/>). Falha devolve null
/// para o caminho de reserva. Não grava log.
/// </summary>
public static class RedutorFotoAndroid
{
    public readonly record struct JpegCodificado(byte[] Bytes, int LadoLongo)
    {
        public override string ToString() => "JpegCodificado";
    }

    public static int LerOrientacao(byte[] bytes)
    {
        try
        {
            using var fluxo = new MemoryStream(bytes, writable: false);
            using var exif = new global::Android.Media.ExifInterface(fluxo);
            return exif.GetAttributeInt(
                global::Android.Media.ExifInterface.TagOrientation,
                OrientacaoExif.Normal);
        }
        catch (Exception)
        {
            return OrientacaoExif.Normal;
        }
    }

    public static JpegCodificado? TentarJpeg(byte[] bytes, int ladoMaximo, float qualidade)
    {
        global::Android.Graphics.Bitmap? bruto = null;
        global::Android.Graphics.Bitmap? orientado = null;
        global::Android.Graphics.Bitmap? escalado = null;
        global::Android.Graphics.Matrix? matriz = null;
        try
        {
            using var limites = new global::Android.Graphics.BitmapFactory.Options { InJustDecodeBounds = true };
            using (var leituraLimites = new MemoryStream(bytes, writable: false))
                global::Android.Graphics.BitmapFactory.DecodeStream(leituraLimites, null, limites);

            if (limites.OutWidth <= 0 || limites.OutHeight <= 0)
                return null;

            var amostra = PreparoFotoComprovante.CalcularAmostra(limites.OutWidth, limites.OutHeight, ladoMaximo);
            using var pixels = new global::Android.Graphics.BitmapFactory.Options
            {
                InSampleSize = amostra,
                InPreferredConfig = global::Android.Graphics.Bitmap.Config.Argb8888
            };
            using (var leitura = new MemoryStream(bytes, writable: false))
                bruto = global::Android.Graphics.BitmapFactory.DecodeStream(leitura, null, pixels);

            if (bruto is null)
                return null;

            var orientacao = LerOrientacao(bytes);
            (orientado, matriz) = Aplicar(bruto, orientacao, limites.OutWidth, limites.OutHeight);
            if (!ReferenceEquals(orientado, bruto))
            {
                Reciclar(bruto);
                bruto = null;
            }

            var alvo = PreparoFotoComprovante.CalcularTamanho(orientado.Width, orientado.Height, ladoMaximo);
            if (alvo.Largura != orientado.Width || alvo.Altura != orientado.Height)
            {
                escalado = global::Android.Graphics.Bitmap.CreateScaledBitmap(
                    orientado,
                    alvo.Largura,
                    alvo.Altura,
                    true);
                if (!ReferenceEquals(escalado, orientado))
                {
                    Reciclar(orientado);
                    orientado = null;
                }
            }
            else
            {
                escalado = orientado;
            }

            if (escalado is null)
                return null;

            using var saida = new MemoryStream();
            var nota = (int)Math.Round(qualidade * 100f, MidpointRounding.AwayFromZero);
            if (nota < 1)
                nota = 1;
            if (nota > 100)
                nota = 100;

            if (!escalado.Compress(global::Android.Graphics.Bitmap.CompressFormat.Jpeg, nota, saida))
                return null;

            var gravado = saida.ToArray();
            if (gravado.Length == 0)
                return null;

            return new JpegCodificado(gravado, Math.Max(alvo.Largura, alvo.Altura));
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            matriz?.Dispose();
            ReciclarDistintos(escalado, orientado, bruto);
        }
    }

    private static (global::Android.Graphics.Bitmap Bitmap, global::Android.Graphics.Matrix? Matriz) Aplicar(
        global::Android.Graphics.Bitmap origem,
        int orientacao,
        int larguraBruta,
        int alturaBruta)
    {
        if (OrientacaoExif.DecoderAplicaExif || !OrientacaoExif.ExigeTransformacao(orientacao))
            return (origem, null);

        if (OrientacaoExif.DecoderJaTrocouLados(orientacao, larguraBruta, alturaBruta, origem.Width, origem.Height))
            return (origem, null);

        var matriz = new global::Android.Graphics.Matrix();
        switch (orientacao)
        {
            case OrientacaoExif.FlipHorizontal:
                matriz.SetScale(-1, 1);
                break;
            case OrientacaoExif.Rotate180:
                matriz.SetRotate(180);
                break;
            case OrientacaoExif.FlipVertical:
                matriz.SetRotate(180);
                matriz.PostScale(-1, 1);
                break;
            case OrientacaoExif.Transpose:
                matriz.SetRotate(90);
                matriz.PostScale(-1, 1);
                break;
            case OrientacaoExif.Rotate90:
                matriz.SetRotate(90);
                break;
            case OrientacaoExif.Transverse:
                matriz.SetRotate(-90);
                matriz.PostScale(-1, 1);
                break;
            case OrientacaoExif.Rotate270:
                matriz.SetRotate(-90);
                break;
            default:
                matriz.Dispose();
                return (origem, null);
        }

        var novo = global::Android.Graphics.Bitmap.CreateBitmap(
            origem,
            0,
            0,
            origem.Width,
            origem.Height,
            matriz,
            true);
        return (novo ?? origem, matriz);
    }

    private static void ReciclarDistintos(
        global::Android.Graphics.Bitmap? escalado,
        global::Android.Graphics.Bitmap? orientado,
        global::Android.Graphics.Bitmap? bruto)
    {
        Reciclar(escalado);
        if (!ReferenceEquals(orientado, escalado))
            Reciclar(orientado);
        if (!ReferenceEquals(bruto, orientado) && !ReferenceEquals(bruto, escalado))
            Reciclar(bruto);
    }

    private static void Reciclar(global::Android.Graphics.Bitmap? bitmap)
    {
        if (bitmap is null)
            return;

        try
        {
            if (!bitmap.IsRecycled)
                bitmap.Recycle();
        }
        catch (Exception)
        {
            // O nativo já pode ter sido solto por outro descarte do mesmo quadro.
        }

        try
        {
            bitmap.Dispose();
        }
        catch (Exception)
        {
        }
    }
}
