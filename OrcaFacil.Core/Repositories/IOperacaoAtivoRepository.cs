using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Repositories;

public interface IOperacaoAtivoRepository : IGenericRepository<OperacaoAtivo>
{
    Task<List<OperacaoAtivo>> ListarPorAtivoAsync(int ativoId, int membroFamiliaId);
    Task<List<OperacaoAtivo>> ListarPorMembroAsync(int membroFamiliaId);
    Task<bool> ExistePorAtivoAsync(int ativoId);
}
