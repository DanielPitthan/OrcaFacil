using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Repositories;

public interface IAtivoFinanceiroRepository : IGenericRepository<AtivoFinanceiro>
{
    Task<List<AtivoFinanceiro>> ListarAsync(bool somenteAtivos);
    Task<bool> ExisteTickerAsync(string ticker, int? ignorarId = null);
}
