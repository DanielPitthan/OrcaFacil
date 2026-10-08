using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Repositories;

public interface IMetaRepository : IGenericRepository<Meta>
{
    Task<List<Meta>> GetByMembroAsync(int membroFamiliaId);
    Task<List<Meta>> GetAtivasAsync();
}
