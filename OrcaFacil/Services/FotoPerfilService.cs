using Microsoft.Extensions.Logging;
using Microsoft.Maui.Graphics.Platform;
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
        if (!MediaPicker.Default.IsCaptureSupported)
            throw new InvalidOperationException("Este dispositivo não suporta captura de fotos.");

        if (await Permissions.RequestAsync<Permissions.Camera>() != PermissionStatus.Granted)
            throw new InvalidOperationException("Permissão de câmera negada.");

        var arquivo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions { Title = "Tirar selfie" });
        return arquivo is null ? null : await SalvarAsync(arquivo);
    }

    public async Task<string?> EscolherDaGaleriaAsync()
    {
        var arquivo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions { Title = "Escolher foto" });
        return arquivo is null ? null : await SalvarAsync(arquivo);
    }

    public string? ObterDataUrl(string? caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho) || !File.Exists(caminho))
            return null;

        var bytes = File.ReadAllBytes(caminho);
        return $"data:image/jpeg;base64,{Convert.ToBase64String(bytes)}";
    }

    public void Remover()
    {
        if (Directory.Exists(PastaFotos))
            Directory.Delete(PastaFotos, recursive: true);
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
                var imagem = PlatformImage.FromStream(origem);
                var reduzida = imagem.Width > LadoMaximo || imagem.Height > LadoMaximo
                    ? imagem.Downsize(LadoMaximo, disposeOriginal: true)
                    : imagem;

                await using var saida = File.Create(destino);
                await reduzida.SaveAsync(saida, ImageFormat.Jpeg, 0.85f);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao redimensionar a foto; salvando o original.");
                await using var origemNovamente = await arquivo.OpenReadAsync();
                await using var saida = File.Create(destino);
                await origemNovamente.CopyToAsync(saida);
            }
        }

        foreach (var antiga in Directory.GetFiles(PastaFotos).Where(f => f != destino))
            File.Delete(antiga);

        return destino;
    }
}
