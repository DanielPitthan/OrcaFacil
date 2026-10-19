using Microsoft.Extensions.Logging;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;
using GraphicsImage = global::Microsoft.Maui.Graphics.IImage;
using OrcaFacil.Core.Llm;
using OrcaFacil.Services.Interfaces;

namespace OrcaFacil.Services;

public class FotoPerfilService : IFotoPerfilService
{
    private const float LadoMaximo = 512f;

    private readonly ILogger<FotoPerfilService> _logger;

    public FotoPerfilService(ILogger<FotoPerfilService> logger)
    {
        _logger = logger;
    }

    public string PastaFotos => Path.Combine(FileSystem.AppDataDirectory, "perfil");

    public async Task<string?> TirarSelfieAsync()
    {
        var arquivo = await CapturarFotoAsync("Tirar selfie");
        return arquivo is null ? null : await SalvarAsync(arquivo);
    }

    public async Task<string?> EscolherDaGaleriaAsync()
    {
        var arquivo = await EscolherFotoAsync("Escolher foto");
        return arquivo is null ? null : await SalvarAsync(arquivo);
    }

    public async Task<FileResult?> CapturarFotoAsync(string titulo)
    {
        if (!MediaPicker.Default.IsCaptureSupported)
            throw new InvalidOperationException("Este dispositivo não suporta captura de fotos.");

        if (await Permissions.RequestAsync<Permissions.Camera>() != PermissionStatus.Granted)
            throw new InvalidOperationException("Permissão de câmera negada.");

        return await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions { Title = titulo });
    }

    public Task<FileResult?> EscolherFotoAsync(string titulo) =>
        MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions { Title = titulo });

    public Task<byte[]> ReduzirArquivoEmMemoriaAsync(
        FileResult arquivo,
        float ladoMaximo = LadoMaximo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        return ReduzirArquivoEmMemoriaCoreAsync(arquivo, ladoMaximo, cancellationToken);
    }

    public string? ObterDataUrl(string? caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho) || !File.Exists(caminho))
            return null;

        var bytes = File.ReadAllBytes(caminho);
        return $"data:image/jpeg;base64,{Convert.ToBase64String(bytes)}";
    }

    public async Task<string> GirarHorarioAsync(string caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho) || !File.Exists(caminho))
            throw new InvalidOperationException("Foto não encontrada.");

        await using var entrada = File.OpenRead(caminho);
        var imagem = PlatformImage.FromStream(entrada)
            ?? throw new InvalidOperationException("Não foi possível ler a foto.");

        return await PersistirAsync(GirarHorario(imagem));
    }

    public void Remover()
    {
        if (Directory.Exists(PastaFotos))
            Directory.Delete(PastaFotos, recursive: true);
    }

    private async Task<byte[]> ReduzirArquivoEmMemoriaCoreAsync(
        FileResult arquivo,
        float ladoMaximo,
        CancellationToken cancellationToken)
    {
        string? caminho = null;
        try
        {
            caminho = TentarCaminho(arquivo);
            await using var origem = await arquivo.OpenReadAsync();
            return await ReduzirJpegAsync(origem, ladoMaximo, cancellationToken);
        }
        finally
        {
            ApagarTemporario(caminho);
        }
    }

    private async Task<string> SalvarAsync(FileResult arquivo)
    {
        Directory.CreateDirectory(PastaFotos);

        // Nome único evita cache da data URL antiga e permite descartar a foto anterior.
        var destino = Path.Combine(PastaFotos, $"foto-{DateTime.Now:yyyyMMddHHmmss}.jpg");

        await using (var origem = await arquivo.OpenReadAsync())
        {
            try
            {
                var jpeg = await ReduzirJpegAsync(origem, LadoMaximo, CancellationToken.None);
                await using var saida = File.Create(destino);
                await saida.WriteAsync(jpeg);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao redimensionar a foto; salvando o original.");
                await using var origemNovamente = await arquivo.OpenReadAsync();
                await using var saida = File.Create(destino);
                await origemNovamente.CopyToAsync(saida);
            }
        }

        ApagarAnteriores(destino);
        return destino;
    }

    private async Task<string> PersistirAsync(GraphicsImage imagem)
    {
        Directory.CreateDirectory(PastaFotos);
        var destino = Path.Combine(PastaFotos, $"foto-{DateTime.Now:yyyyMMddHHmmss}.jpg");
        var reduzida = Reduzir(imagem, LadoMaximo);

        await using var saida = File.Create(destino);
        await reduzida.SaveAsync(saida, ImageFormat.Jpeg, 0.85f);
        ApagarAnteriores(destino);
        return destino;
    }

    private async Task<byte[]> ReduzirJpegAsync(Stream origem, float ladoMaximo, CancellationToken cancellationToken)
    {
        using var memoria = new MemoryStream();
        await origem.CopyToAsync(memoria, cancellationToken);
        var graus = LerRotacaoExif(memoria);
        memoria.Position = 0;

        var imagem = PlatformImage.FromStream(memoria)
            ?? throw new InvalidOperationException("Não foi possível ler a foto.");
        var orientada = AplicarRotacao(imagem, graus);
        var reduzida = Reduzir(orientada, ladoMaximo);

        await using var saida = new MemoryStream();
        await reduzida.SaveAsync(saida, ImageFormat.Jpeg, 0.85f);
        return saida.ToArray();
    }

    private static GraphicsImage Reduzir(GraphicsImage imagem, float ladoMaximo)
    {
        var largura = (int)Math.Round(imagem.Width, MidpointRounding.AwayFromZero);
        var altura = (int)Math.Round(imagem.Height, MidpointRounding.AwayFromZero);
        if (largura < 1 || altura < 1)
            throw new InvalidOperationException("Não foi possível ler a foto.");

        var teto = (int)Math.Round(ladoMaximo, MidpointRounding.AwayFromZero);
        var alvo = PreparoFotoComprovante.CalcularTamanho(largura, altura, teto);
        if (alvo.Largura == largura && alvo.Altura == altura)
            return imagem;

        return imagem.Downsize(alvo.Largura, alvo.Altura, disposeOriginal: true);
    }

    private void ApagarAnteriores(string destino)
    {
        foreach (var antiga in Directory.GetFiles(PastaFotos).Where(f => f != destino))
            File.Delete(antiga);
    }

    private static string? TentarCaminho(FileResult arquivo)
    {
        try
        {
            return arquivo.FullPath;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Apaga só cache ou pasta temporária. Nunca a pasta perfil/ nem um arquivo fora desses dois lugares.
    /// </summary>
    private static void ApagarTemporario(string? caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho))
            return;

        try
        {
            if (!File.Exists(caminho))
                return;

            var cheio = Path.GetFullPath(caminho);
            var perfil = Path.GetFullPath(Path.Combine(FileSystem.AppDataDirectory, "perfil"));
            if (EstaDentro(cheio, perfil))
                return;

            var cache = Path.GetFullPath(FileSystem.CacheDirectory);
            var temporario = Path.GetFullPath(Path.GetTempPath());
            if (!EstaDentro(cheio, cache) && !EstaDentro(cheio, temporario))
                return;

            File.Delete(cheio);
        }
        catch (Exception)
        {
            // O cache do aparelho é temporário. O caminho não entra no log.
        }
    }

    private static bool EstaDentro(string caminho, string pasta)
    {
        var prefixo = pasta.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return string.Equals(caminho, pasta, StringComparison.OrdinalIgnoreCase)
            || caminho.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase);
    }

    private static int LerRotacaoExif(Stream stream)
    {
#if ANDROID
        stream.Position = 0;
        var exif = new global::Android.Media.ExifInterface(stream);
        var orientacao = exif.GetAttributeInt(
            global::Android.Media.ExifInterface.TagOrientation,
            (int)global::Android.Media.Orientation.Normal);
        stream.Position = 0;

        return orientacao switch
        {
            (int)global::Android.Media.Orientation.Rotate90 => 90,
            (int)global::Android.Media.Orientation.Rotate180 => 180,
            (int)global::Android.Media.Orientation.Rotate270 => 270,
            _ => 0
        };
#else
        _ = stream;
        return 0;
#endif
    }

    private static GraphicsImage AplicarRotacao(GraphicsImage imagem, int graus)
    {
        var atual = imagem;
        var passos = ((graus % 360) + 360) % 360 / 90;
        for (var i = 0; i < passos; i++)
            atual = GirarHorario(atual);

        return atual;
    }

    private static GraphicsImage GirarHorario(GraphicsImage imagem)
    {
        var largura = (int)Math.Round(imagem.Width);
        var altura = (int)Math.Round(imagem.Height);
        var exportacao = CriarExportacao(altura, largura);
        var canvas = exportacao.Canvas;
        canvas.Translate(altura, 0);
        canvas.Rotate(90);
        imagem.Draw(canvas, new RectF(0, 0, largura, altura));
        return exportacao.Image;
    }

    private static BitmapExportContext CriarExportacao(int largura, int altura)
    {
#if WINDOWS
        return new Microsoft.Maui.Graphics.Win2D.W2DBitmapExportService().CreateContext(largura, altura, 1);
#else
        return new PlatformBitmapExportContext(largura, altura, 1);
#endif
    }
}
