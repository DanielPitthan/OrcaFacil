using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Repositories;

public interface IMembroFamiliaRepository : IGenericRepository<MembroFamilia>
{
    Task<List<MembroFamilia>> GetAtivosAsync();
}
