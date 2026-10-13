using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Services.Interfaces;

public interface IAtivoService
{
    Task<List<AtivoFinanceiro>> ListarAsync(bool somenteAtivos = false);
    Task<AtivoFinanceiro> SalvarAsync(AtivoFormModel modelo);
    Task ExcluirAsync(int id);
    Task InativarAsync(int id);
}
