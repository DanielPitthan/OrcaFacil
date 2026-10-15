using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Services.Interfaces;

public interface ICartaoService
{
    Task<List<Cartao>> ListarAsync(bool somenteAtivos = false, TipoCartao? tipo = null);
    Task<Cartao?> ObterAsync(int id);
    Task<Cartao> SalvarAsync(CartaoFormModel modelo);
    Task ExcluirAsync(int id);
    Task InativarAsync(int id);

    Task<List<RecargaCartao>> ListarRecargasAsync(int cartaoId);
    Task<RecargaCartao> RecarregarAsync(RecargaCartaoFormModel modelo);
    Task ExcluirRecargaAsync(int recargaId);
    Task<decimal> ObterSaldoBeneficioAsync(int cartaoId, int? ignorarCompraId = null);
    Task<List<Transacao>> ListarFaturasAbertasAsync(int cartaoId);
}
