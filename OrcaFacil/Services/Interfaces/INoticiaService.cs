using OrcaFacil.Core.Noticias;

namespace OrcaFacil.Services.Interfaces;

public enum EscopoNoticia
{
    Brasil,
    Mundo
}

public sealed record ResultadoNoticias(IReadOnlyList<Noticia> Itens, string? Aviso);

public interface INoticiaService
{
    Task<ResultadoNoticias> ListarAsync(
        EscopoNoticia escopo,
        bool forcarAtualizacao = false,
        CancellationToken cancellationToken = default);
}
