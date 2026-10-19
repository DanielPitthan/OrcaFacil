namespace OrcaFacil.Services.Interfaces;

/// <summary>Captura (câmera/galeria), redimensiona e persiste a foto do perfil no armazenamento local.</summary>
public interface IFotoPerfilService
{
    /// <summary>Retorna o caminho salvo ou null se o usuário cancelar.</summary>
    Task<string?> TirarSelfieAsync();

    /// <summary>Retorna o caminho salvo ou null se o usuário cancelar.</summary>
    Task<string?> EscolherDaGaleriaAsync();

    /// <summary>
    /// Abre a câmera (<c>Permissions.Camera</c>, <c>CapturePhotoAsync</c>).
    /// Null se a pessoa cancelar. Não grava em perfil/.
    /// </summary>
    Task<FileResult?> CapturarFotoAsync(string titulo);

    /// <summary>Abre a galeria (<c>PickPhotoAsync</c>). Null se a pessoa cancelar. Não grava em perfil/.</summary>
    Task<FileResult?> EscolherFotoAsync(string titulo);

    /// <summary>
    /// JPEG em memória, com a rotação EXIF do Android. O lado máximo padrão é o do avatar (512).
    /// Arquivo temporário de cache, se houver, é apagado no finally. Não grava em perfil/.
    /// </summary>
    Task<byte[]> ReduzirArquivoEmMemoriaAsync(
        FileResult arquivo,
        float ladoMaximo = 512f,
        CancellationToken cancellationToken = default);

    string? ObterDataUrl(string? caminho);

    /// <summary>Gira a foto salva 90° no sentido horário e devolve o novo caminho.</summary>
    Task<string> GirarHorarioAsync(string caminho);

    void Remover();

    string PastaFotos { get; }
}
