using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Services.Interfaces;

public interface IMetaService
{
    Task<List<Meta>> ListarAsync();
    Task<Meta> CriarAsync(MetaFormModel dto, int membroFamiliaId);
    Task AtualizarAsync(int id, MetaFormModel dto);
    Task ExcluirAsync(int id);
    Task AdicionarContribuicaoAsync(int metaId, ContribuicaoMetaModel dto);
}
