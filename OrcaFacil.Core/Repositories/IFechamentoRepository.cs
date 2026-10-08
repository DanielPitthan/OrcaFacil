using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Repositories;

public interface IFechamentoRepository : IGenericRepository<FechamentoPeriodo>
{
    Task<bool> EstaFechadoAsync(int mes, int ano);

    /// <summary>Inverte o estado do período (cria o registro se ainda não existir) e retorna o novo estado.</summary>
    Task<bool> AlternarAsync(int mes, int ano);
}
