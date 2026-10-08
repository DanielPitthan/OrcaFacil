using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Repositories;

public interface IOrcamentoRepository : IGenericRepository<Orcamento>
{
    Task<List<Orcamento>> GetByMesAnoAsync(int mes, int ano);
    Task<Orcamento?> GetByCategoriaMesAnoAsync(int categoriaId, int mes, int ano);
}
