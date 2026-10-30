using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Repositories;

public interface ICategoriaRepository : IGenericRepository<Categoria>
{
    Task<List<Categoria>> GetAtivasAsync();
    Task<Categoria?> GetByNomeAsync(string nome);
    Task<bool> ExisteNomeAsync(string nome, int? ignorarId = null);
    Task<bool> PossuiTransacoesAsync(int categoriaId);
}
