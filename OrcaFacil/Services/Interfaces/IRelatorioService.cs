using OrcaFacil.Core.Dtos;

namespace OrcaFacil.Services.Interfaces;

public interface IRelatorioService
{
    Task<RelatorioResultDto> GerarAsync(FiltroRelatorioDto filtro);
}
