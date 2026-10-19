using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;
using GraphicsImage = global::Microsoft.Maui.Graphics.IImage;
using OrcaFacil.Core.Armazenamento;
using OrcaFacil.Core.Llm;
using OrcaFacil.Services.Interfaces;
#if ANDROID
using OrcaFacil.Platforms.Android.Fotos;
#endif

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
        FileResult? arquivo = null;
        try
        {
            arquivo = await CapturarFotoAsync("Tirar selfie");
            return arquivo is null ? null : await SalvarAsync(arquivo);
        }
        finally
        {
            ApagarTemporarioDoFluxo(TentarCaminho(arquivo));
        }
    }

    public async Task<string?> EscolherDaGaleriaAsync()
    {
        FileResult? arquivo = null;
        try
        {
            arquivo = await EscolherFotoAsync("Escolher foto");
            return arquivo is null ? null : await SalvarAsync(arquivo);
        }
        finally
        {
            ApagarTemporarioDoFluxo(TentarCaminho(arquivo));
        }
    }

    public async Task<FotoComprovantePronta> PrepararComprovanteAsync(
        bool daCamera,
        Func<Task>? aoReceberFoto = null,
        CancellationToken cancellationToken = default)
    {
        FileResult? arquivo = null;
        try
        {
            arquivo = daCamera
                ? await CapturarFotoAsync("Ler comprovante")
                : await EscolherFotoAsync("Escolher comprovante");
            if (arquivo is null || cancellationToken.IsCancellationRequested)
                return FotoComprovantePronta.SemFoto();

            if (aoReceberFoto is not null)
                await aoReceberFoto();

            if (cancellationToken.IsCancellationRequested)
                return FotoComprovantePronta.SemFoto();

            await using var origem = await arquivo.OpenReadAsync();
            var bytes = await CopiarAsync(origem, cancellationToken);
            if (bytes.Length == 0)
                return FotoComprovantePronta.Ilegivel();

            return await MontarComprovanteAsync(bytes, cancellationToken);
        }
        finally
        {
            ApagarTemporarioDoFluxo(TentarCaminho(arquivo));
        }
    }

    public string? ObterDataUrl(string? caminho)
    {
        var fisico = ResolverArquivo(caminho);
        if (fisico is null)
            return null;

        var bytes = File.ReadAllBytes(fisico);
        return $"data:image/jpeg;base64,{Convert.ToBase64String(bytes)}";
    }

    public async Task<string> GirarHorarioAsync(string caminho)
    {
        var fisico = ResolverArquivo(caminho);
        if (fisico is null)
            throw new InvalidOperationException("Foto não encontrada.");

        GraphicsImage imagem;
        await using (var entrada = File.OpenRead(fisico))
        {
            imagem = PlatformImage.FromStream(entrada)
                ?? throw new InvalidOperationException("Não foi possível ler a foto.");
        }

        var descarte = new Descarte();
        var giro = GirarPassos(imagem, 1, descarte);
        if (giro.Contexto is not null)
            descarte.Manter(giro.Contexto);

        try
        {
            return await PersistirAsync(giro.Imagem, descarte);
        }
        finally
        {
            descarte.Agora(giro.Imagem);
            descarte.Resto();
        }
    }

    public void Remover()
    {
        if (Directory.Exists(PastaFotos))
            Directory.Delete(PastaFotos, recursive: true);
    }

    private async Task<FileResult?> CapturarFotoAsync(string titulo)
    {
        if (!MediaPicker.Default.IsCaptureSupported)
            throw new InvalidOperationException("Este dispositivo não suporta captura de fotos.");

        if (await Permissions.RequestAsync<Permissions.Camera>() != PermissionStatus.Granted)
            throw new InvalidOperationException("Permissão de câmera negada.");

        return await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions { Title = titulo });
    }

    private Task<FileResult?> EscolherFotoAsync(string titulo) =>
        MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions { Title = titulo });

    private readonly record struct PlanoCodificacao(byte[]? Enviar, int Lado, float Qualidade, bool Repetir);

    private async Task<FotoComprovantePronta> MontarComprovanteAsync(byte[] origem, CancellationToken cancellationToken)
    {
        var plano = await PrimeiraCodificacaoAsync(origem, cancellationToken);
        if (plano.Enviar is not null)
            return FotoComprovantePronta.ComJpeg(plano.Enviar);
        if (!plano.Repetir)
            return FotoComprovantePronta.PedindoOutra();

        var segunda = await CodificarAsync(origem, plano.Lado, plano.Qualidade, avatar: false, cancellationToken);
        var deNovo = PreparoFotoComprovante.DecidirEnvio(segunda.Bytes.Length, segunda.LadoLongo);
        if (!deNovo.Enviar || segunda.Bytes.Length > ClienteMensagens.TamanhoMaximoImagem)
            return FotoComprovantePronta.PedindoOutra();

            return FotoComprovantePronta.ComJpeg(segunda.Bytes);
    }

    /// <summary>
    /// Na repetição o JPEG da primeira codificação não volta: só o lado e a qualidade novos.
    /// </summary>
    private async Task<PlanoCodificacao> PrimeiraCodificacaoAsync(byte[] origem, CancellationToken cancellationToken)
    {
        var primeira = await CodificarAsync(
            origem,
            PreparoFotoComprovante.LadoMaximoComprovante,
            PreparoFotoComprovante.QualidadeJpeg,
            avatar: false,
            cancellationToken);
        var decisao = PreparoFotoComprovante.DecidirEnvio(primeira.Bytes.Length, primeira.LadoLongo);
        if (decisao.Enviar && primeira.Bytes.Length <= ClienteMensagens.TamanhoMaximoImagem)
            return new PlanoCodificacao(primeira.Bytes, 0, 0f, false);

        if (!decisao.PedirOutraFoto && !decisao.Enviar)
            return new PlanoCodificacao(null, decisao.LadoMaximo, decisao.Qualidade, true);

        return new PlanoCodificacao(null, 0, 0f, false);
    }

    private async Task<string> SalvarAsync(FileResult arquivo)
    {
        var temporario = TentarCaminho(arquivo);
        try
        {
            Directory.CreateDirectory(PastaFotos);

            // Nome único evita cache da data URL antiga e permite descartar a foto anterior.
            var destino = Path.Combine(PastaFotos, $"foto-{DateTime.Now:yyyyMMddHHmmss}.jpg");

            await using (var origem = await arquivo.OpenReadAsync())
            {
                try
                {
                    var bytes = await CopiarAsync(origem, CancellationToken.None);
                    var jpeg = await CodificarAsync(
                        bytes,
                        (int)LadoMaximo,
                        PreparoFotoComprovante.QualidadeJpeg,
                        avatar: true,
                        CancellationToken.None);
                    await using var saida = File.Create(destino);
                    await saida.WriteAsync(jpeg.Bytes);
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
            return Path.GetFileName(destino);
        }
        finally
        {
            ApagarTemporarioDoFluxo(temporario);
        }
    }

    private async Task<string> PersistirAsync(GraphicsImage imagem, Descarte descarte)
    {
        Directory.CreateDirectory(PastaFotos);
        var destino = Path.Combine(PastaFotos, $"foto-{DateTime.Now:yyyyMMddHHmmss}.jpg");
        var reduzida = imagem.Width > LadoMaximo || imagem.Height > LadoMaximo
            ? imagem.Downsize(LadoMaximo, disposeOriginal: true)
            : imagem;
        if (!ReferenceEquals(reduzida, imagem))
            descarte.Marcar(imagem);

        try
        {
            await using var saida = File.Create(destino);
            await reduzida.SaveAsync(saida, ImageFormat.Jpeg, PreparoFotoComprovante.QualidadeJpeg);
        }
        finally
        {
            descarte.Agora(reduzida);
        }

        ApagarAnteriores(destino);
        return Path.GetFileName(destino);
    }

    /// <summary>
    /// Só abre arquivo cujo alvo final está em <see cref="PastaFotos"/>.
    /// Nome sem pasta e caminho absoluto antigo caem nessa pasta. Link para fora é recusado.
    /// </summary>
    private string? ResolverArquivo(string? valor)
        => FotoPath.CaminhoDentroDaPasta(PastaFotos, valor);

    private void ApagarAnteriores(string destino)
    {
        foreach (var antiga in Directory.GetFiles(PastaFotos).Where(f => f != destino))
            File.Delete(antiga);
    }

    private async Task<(byte[] Bytes, int LadoLongo)> CodificarAsync(
        byte[] origem,
        int ladoMaximo,
        float qualidade,
        bool avatar,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
#if ANDROID
        var nativo = RedutorFotoAndroid.TentarJpeg(origem, ladoMaximo, qualidade);
        if (nativo is not null)
            return (nativo.Value.Bytes, nativo.Value.LadoLongo);
#endif
        return await CodificarGraphicsAsync(origem, ladoMaximo, qualidade, avatar, cancellationToken);
    }

    private async Task<(byte[] Bytes, int LadoLongo)> CodificarGraphicsAsync(
        byte[] origem,
        int ladoMaximo,
        float qualidade,
        bool avatar,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var memoria = new MemoryStream(origem, writable: false);
        var imagem = PlatformImage.FromStream(memoria)
            ?? throw new InvalidOperationException("Não foi possível ler a foto.");

        var descarte = new Descarte();
        GraphicsImage? reduzida = null;
        try
        {
            var quadro = Orientar(imagem, LerOrientacao(origem), descarte);
            if (quadro.Contexto is not null)
                descarte.Manter(quadro.Contexto);

            var orientada = quadro.Imagem;
            if (avatar)
            {
                reduzida = orientada.Width > LadoMaximo || orientada.Height > LadoMaximo
                    ? orientada.Downsize(LadoMaximo, disposeOriginal: true)
                    : orientada;
            }
            else
            {
                reduzida = Reduzir(orientada, ladoMaximo);
            }

            if (!ReferenceEquals(reduzida, orientada))
                descarte.Marcar(orientada);

            var lado = Math.Max(Medida(reduzida.Width), Medida(reduzida.Height));
            await using var saida = new MemoryStream();
            await reduzida.SaveAsync(saida, ImageFormat.Jpeg, qualidade);
            cancellationToken.ThrowIfCancellationRequested();
            return (saida.ToArray(), lado);
        }
        finally
        {
            descarte.Agora(reduzida);
            descarte.Agora(imagem);
            descarte.Resto();
        }
    }

    private static GraphicsImage Reduzir(GraphicsImage imagem, int ladoMaximo)
    {
        var largura = Medida(imagem.Width);
        var altura = Medida(imagem.Height);
        var alvo = PreparoFotoComprovante.CalcularTamanho(largura, altura, ladoMaximo);
        if (alvo.Largura == largura && alvo.Altura == altura)
            return imagem;

        return imagem.Downsize(alvo.Largura, alvo.Altura, disposeOriginal: true);
    }

    private static int LerOrientacao(byte[] bytes)
    {
#if ANDROID
        return RedutorFotoAndroid.LerOrientacao(bytes);
#else
        _ = bytes;
        return OrientacaoExif.Normal;
#endif
    }

    private readonly record struct Quadro(GraphicsImage Imagem, BitmapExportContext? Contexto);

    private static Quadro Orientar(GraphicsImage imagem, int orientacao, Descarte descarte)
    {
        if (OrientacaoExif.DecoderAplicaExif || !OrientacaoExif.ExigeTransformacao(orientacao))
            return new Quadro(imagem, null);

        var largura = Medida(imagem.Width);
        var altura = Medida(imagem.Height);
        if (OrientacaoExif.DecoderJaTrocouLados(orientacao, largura, altura, largura, altura))
            return new Quadro(imagem, null);

        return orientacao switch
        {
            OrientacaoExif.FlipHorizontal => Trocar(imagem, Espelhar(imagem, horizontal: true), descarte),
            OrientacaoExif.Rotate180 => GirarPassos(imagem, 2, descarte),
            OrientacaoExif.FlipVertical => Trocar(imagem, Espelhar(imagem, horizontal: false), descarte),
            OrientacaoExif.Transpose => GirarEEspelhar(imagem, 1, descarte),
            OrientacaoExif.Rotate90 => GirarPassos(imagem, 1, descarte),
            OrientacaoExif.Transverse => GirarEEspelhar(imagem, 3, descarte),
            OrientacaoExif.Rotate270 => GirarPassos(imagem, 3, descarte),
            _ => new Quadro(imagem, null)
        };
    }

    private static Quadro Trocar(GraphicsImage anterior, Quadro nova, Descarte descarte)
    {
        descarte.Agora(anterior);
        return nova;
    }

    private static Quadro GirarEEspelhar(GraphicsImage imagem, int passos, Descarte descarte)
    {
        var girada = GirarPassos(imagem, passos, descarte);
        var espelho = Espelhar(girada.Imagem, horizontal: true);
        descarte.Agora(girada.Contexto);
        descarte.Agora(girada.Imagem);
        return espelho;
    }

    /// <summary>
    /// Cada passo descarta o bitmap anterior. 180 e 270 não ficam com as cópias intermediárias vivas.
    /// </summary>
    private static Quadro GirarPassos(GraphicsImage imagem, int passos, Descarte descarte)
    {
        var atual = imagem;
        BitmapExportContext? contexto = null;
        for (var i = 0; i < passos; i++)
        {
            var giro = GirarUma(atual);
            if (contexto is not null)
                descarte.Agora(contexto);
            else
                descarte.Agora(atual);

            contexto = giro.Contexto;
            atual = giro.Imagem;
        }

        return new Quadro(atual, contexto);
    }

    private static Quadro GirarUma(GraphicsImage imagem)
    {
        var largura = Medida(imagem.Width);
        var altura = Medida(imagem.Height);
        var contexto = CriarExportacao(altura, largura);
        var canvas = contexto.Canvas;
        canvas.Translate(altura, 0);
        canvas.Rotate(90);
        imagem.Draw(canvas, new RectF(0, 0, largura, altura));
        return new Quadro(contexto.Image, contexto);
    }

    private static Quadro Espelhar(GraphicsImage imagem, bool horizontal)
    {
        var largura = Medida(imagem.Width);
        var altura = Medida(imagem.Height);
        var contexto = CriarExportacao(largura, altura);
        var canvas = contexto.Canvas;
        if (horizontal)
        {
            canvas.Translate(largura, 0);
            canvas.Scale(-1, 1);
        }
        else
        {
            canvas.Translate(0, altura);
            canvas.Scale(1, -1);
        }

        imagem.Draw(canvas, new RectF(0, 0, largura, altura));
        return new Quadro(contexto.Image, contexto);
    }

    private static int Medida(float valor)
    {
        var inteiro = (int)Math.Round(valor, MidpointRounding.AwayFromZero);
        if (inteiro < 1)
            throw new InvalidOperationException("Não foi possível ler a foto.");

        return inteiro;
    }

    private static BitmapExportContext CriarExportacao(int largura, int altura)
    {
#if WINDOWS
        return new Microsoft.Maui.Graphics.Win2D.W2DBitmapExportService().CreateContext(largura, altura, 1);
#else
        return new PlatformBitmapExportContext(largura, altura, 1);
#endif
    }

    private static async Task<byte[]> CopiarAsync(Stream origem, CancellationToken cancellationToken)
    {
        using var memoria = new MemoryStream();
        await origem.CopyToAsync(memoria, cancellationToken);
        return memoria.ToArray();
    }

    private static string? TentarCaminho(FileResult? arquivo)
    {
        if (arquivo is null)
            return null;

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
    /// Apaga o caminho guardado do <see cref="FileResult"/> e as pastas vazias ancestrais dele.
    /// Não enumera <see cref="FileSystem.CacheDirectory"/> nem <see cref="Path.GetTempPath"/>.
    /// </summary>
    private void ApagarTemporarioDoFluxo(string? caminho)
    {
        foreach (var item in LimpezaFotoTemporaria.ListarRemocao(
                     caminho,
                     PastaFotos,
                     FileSystem.CacheDirectory,
                     Path.GetTempPath()))
        {
            try
            {
                if (File.Exists(item))
                    File.Delete(item);
                else if (Directory.Exists(item) && DiretorioVazio(item))
                    Directory.Delete(item);
            }
            catch (Exception)
            {
                // O cache do aparelho é temporário. O caminho não entra no log.
            }
        }
    }

    private static bool DiretorioVazio(string caminho)
    {
        try
        {
            return !Directory.EnumerateFileSystemEntries(caminho).Any();
        }
        catch (Exception)
        {
            return false;
        }
    }

    private sealed class Descarte
    {
        private readonly HashSet<object> _vistos = new(ReferenceEqualityComparer.Instance);
        private readonly List<IDisposable> _manter = new();

        public void Manter(IDisposable item) => _manter.Add(item);

        public void Marcar(object item) => _vistos.Add(item);

        public void Agora(IDisposable? item)
        {
            if (item is null || !_vistos.Add(item))
                return;

            _manter.Remove(item);
            try
            {
                item.Dispose();
            }
            catch (Exception)
            {
                // O quadro nativo já pode ter sido solto pelo Downsize.
            }
        }

        public void Resto()
        {
            foreach (var item in _manter.ToArray())
                Agora(item);
        }
    }
}
