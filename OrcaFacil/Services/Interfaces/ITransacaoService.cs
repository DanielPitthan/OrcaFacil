using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Services.Interfaces;

public interface ITransacaoService
{
    Task<List<Transacao>> ListarAsync(FiltroLancamentoDto filtro);
    Task<Transacao?> ObterAsync(int id);
    Task<Transacao> CriarAsync(TransacaoFormModel dto);
    Task AtualizarAsync(int id, TransacaoFormModel dto);
    Task ExcluirAsync(int id);
    Task MarcarComoPagoAsync(int id, bool pago);
    Task<int> TrazerDespesasDoMesAnteriorAsync(int mes, int ano);
    Task<bool> ExistemDespesasParaTrazerAsync(int mes, int ano);
    Task<ResumoMensalDto> ObterResumoMensalAsync(int mes, int ano);
    Task<EvolucaoMensalDto> ObterEvolucaoDiariaAsync(int mes, int ano);
    Task<List<GastoPorCategoriaDto>> ObterGastosPorCategoriaAsync(int mes, int ano);
    Task<List<TendenciaMensalDto>> ObterTendenciaMensalAsync(int meses);
    Task<List<Transacao>> ObterDespesasComAvisoAsync();
}
