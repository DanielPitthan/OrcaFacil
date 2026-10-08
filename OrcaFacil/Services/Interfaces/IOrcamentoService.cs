using OrcaFacil.Core.Dtos;

namespace OrcaFacil.Services.Interfaces;

public interface IOrcamentoService
{
    Task<List<OrcamentoItemDto>> ListarPorMesAsync(int mes, int ano);
    Task DefinirLimiteAsync(OrcamentoFormModel dto);
    Task<int> TrazerDoMesAnteriorAsync(int mes, int ano);
    Task<bool> ExistemOrcamentosParaTrazerAsync(int mes, int ano);
    Task<List<AlertaOrcamentoDto>> ObterAlertasAsync(int mes, int ano, decimal percentualMinimo = 90m);
}
