using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Repositories;

public interface ICartaoRepository
{
    Task<List<Cartao>> ListarAsync(bool somenteAtivos = false, TipoCartao? tipo = null);
    Task<Cartao?> ObterAsync(int id);
    Task<bool> ExisteNomeAsync(string nome, int? ignorarId = null);
    Task<bool> PossuiVinculosAsync(int cartaoId);
    Task AdicionarAsync(Cartao cartao);
    Task AtualizarAsync(Cartao cartao);
    Task ExcluirAsync(Cartao cartao);

    Task<List<RecargaCartao>> ListarRecargasAsync(int cartaoId);
    Task<RecargaCartao?> ObterRecargaAsync(int recargaId);
    Task AdicionarRecargaAsync(RecargaCartao recarga);
    Task ExcluirRecargaAsync(RecargaCartao recarga);

    Task<decimal> ObterTotalRecargasAsync(int cartaoId);
    Task<decimal> ObterTotalUsadoBeneficioAsync(int cartaoId, int? ignorarCompraId = null);

    Task<List<Transacao>> ListarFaturasAbertasAsync(int cartaoId);
}
