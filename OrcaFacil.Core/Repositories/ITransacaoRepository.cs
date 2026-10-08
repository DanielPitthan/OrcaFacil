using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Repositories;

public interface ITransacaoRepository : IGenericRepository<Transacao>
{
    Task<List<Transacao>> GetByPeriodoAsync(DateTime inicio, DateTime fim, int? categoriaId = null, TipoTransacao? tipo = null, int? membroFamiliaId = null);
    Task<List<Transacao>> GetRecorrentesAtivasAsync();
    Task<List<Transacao>> GetDespesasRecorrentesPorPeriodoAsync(DateTime inicio, DateTime fim);
    Task<List<Transacao>> GetDespesasPendentesParaAvisoAsync(DateTime ateData);
    Task<HashSet<int>> GetOrigemIdsComCopiaNoPeriodoAsync(DateTime inicio, DateTime fim);
    Task<decimal> SomaPorTipoAsync(TipoTransacao tipo, DateTime inicio, DateTime fim, int? membroFamiliaId = null, bool? pago = null);
    Task<Dictionary<int, decimal>> SomaPorCategoriaAsync(DateTime inicio, DateTime fim, TipoTransacao tipo, int? membroFamiliaId = null);
}
