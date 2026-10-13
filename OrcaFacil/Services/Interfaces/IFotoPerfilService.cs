namespace OrcaFacil.Services.Interfaces;

/// <summary>Captura (câmera/galeria), redimensiona e persiste a foto do perfil no armazenamento local.</summary>
public interface IFotoPerfilService
{
    /// <summary>Retorna o caminho salvo ou null se o usuário cancelar.</summary>
    Task<string?> TirarSelfieAsync();

    /// <summary>Retorna o caminho salvo ou null se o usuário cancelar.</summary>
    Task<string?> EscolherDaGaleriaAsync();

    string? ObterDataUrl(string? caminho);

    /// <summary>Gira a foto salva 90° no sentido horário e devolve o novo caminho.</summary>
    Task<string> GirarHorarioAsync(string caminho);

    void Remover();

    string PastaFotos { get; }
}
