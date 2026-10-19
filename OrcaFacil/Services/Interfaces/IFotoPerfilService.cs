using OrcaFacil.Core.Llm;

namespace OrcaFacil.Services.Interfaces;

/// <summary>Captura (câmera/galeria), redimensiona e persiste a foto do perfil no armazenamento local.</summary>
public interface IFotoPerfilService
{
    /// <summary>Retorna o nome do arquivo salvo, ou null se o usuário cancelar.</summary>
    Task<string?> TirarSelfieAsync();

    /// <summary>Retorna o nome do arquivo salvo, ou null se o usuário cancelar.</summary>
    Task<string?> EscolherDaGaleriaAsync();

    /// <summary>
    /// Abre a câmera ou a galeria, reduz o comprovante em memória e apaga o temporário no finally,
    /// inclusive quando a pessoa cancela, a leitura é interrompida ou a redução falha.
    /// Não grava em perfil/.
    /// </summary>
    Task<FotoComprovantePronta> PrepararComprovanteAsync(
        bool daCamera,
        Func<Task>? aoReceberFoto = null,
        CancellationToken cancellationToken = default);

    string? ObterDataUrl(string? caminho);

    /// <summary>Gira a foto salva 90° no sentido horário e devolve o novo nome de arquivo.</summary>
    Task<string> GirarHorarioAsync(string caminho);

    void Remover();

    string PastaFotos { get; }
}
