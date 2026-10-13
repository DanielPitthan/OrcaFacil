using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Services.Interfaces;

public interface IOperacaoAtivoService
{
    Task<List<PosicaoAtivoDto>> ListarPosicaoAsync();
    Task<List<OperacaoAtivo>> ListarPorAtivoAsync(int ativoId);
    Task SalvarAsync(OperacaoAtivoFormModel modelo);
    Task ExcluirAsync(int id);
}
