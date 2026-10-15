using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Core.Services;
using OrcaFacil.Services.Interfaces;

namespace OrcaFacil.Services;

public class CartaoService : ICartaoService
{
    private readonly CartaoCadastroService _cadastro;

    public CartaoService(ICartaoRepository cartoes)
    {
        _cadastro = new CartaoCadastroService(cartoes);
    }

    public Task<List<Cartao>> ListarAsync(bool somenteAtivos = false, TipoCartao? tipo = null)
        => _cadastro.ListarAsync(somenteAtivos, tipo);

    public Task<Cartao?> ObterAsync(int id) => _cadastro.ObterAsync(id);

    public Task<Cartao> SalvarAsync(CartaoFormModel modelo) => _cadastro.SalvarAsync(modelo);

    public Task ExcluirAsync(int id) => _cadastro.ExcluirAsync(id);

    public Task InativarAsync(int id) => _cadastro.InativarAsync(id);

    public Task<List<RecargaCartao>> ListarRecargasAsync(int cartaoId)
        => _cadastro.ListarRecargasAsync(cartaoId);

    public Task<RecargaCartao> RecarregarAsync(RecargaCartaoFormModel modelo)
        => _cadastro.RecarregarAsync(modelo);

    public Task ExcluirRecargaAsync(int recargaId)
        => _cadastro.ExcluirRecargaAsync(recargaId);

    public Task<decimal> ObterSaldoBeneficioAsync(int cartaoId, int? ignorarCompraId = null)
        => _cadastro.ObterSaldoBeneficioAsync(cartaoId, ignorarCompraId);

    public Task<List<Transacao>> ListarFaturasAbertasAsync(int cartaoId)
        => _cadastro.ListarFaturasAbertasAsync(cartaoId);
}
