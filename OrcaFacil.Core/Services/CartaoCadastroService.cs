using OrcaFacil.Core.Cesta;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Repositories;

namespace OrcaFacil.Core.Services;

public class CartaoCadastroService
{
    private readonly ICartaoRepository _cartoes;

    public CartaoCadastroService(ICartaoRepository cartoes)
    {
        _cartoes = cartoes;
    }

    public Task<List<Cartao>> ListarAsync(bool somenteAtivos = false, TipoCartao? tipo = null)
        => _cartoes.ListarAsync(somenteAtivos, tipo);

    public Task<Cartao?> ObterAsync(int id) => _cartoes.ObterAsync(id);

    public async Task<Cartao> SalvarAsync(CartaoFormModel modelo)
    {
        var nome = NormalizarTexto(modelo.Nome, 80, "Informe o nome do cartão.");
        var bandeira = NormalizarTexto(modelo.Bandeira, 40, "Informe a bandeira do cartão.");
        var digitos = NormalizarDigitos(modelo.UltimosDigitos);

        if (modelo.Tipo == TipoCartao.Beneficio && modelo.TipoBeneficio is null)
            throw new InvalidOperationException("Selecione o tipo do benefício.");

        if (await _cartoes.ExisteNomeAsync(nome, modelo.Id))
            throw new InvalidOperationException("Já existe um cartão com esse nome.");

        var tipoBeneficio = modelo.Tipo == TipoCartao.Beneficio ? modelo.TipoBeneficio : null;

        if (modelo.Id is null)
        {
            var novo = new Cartao
            {
                Nome = nome,
                Bandeira = bandeira,
                UltimosDigitos = digitos,
                Tipo = modelo.Tipo,
                TipoBeneficio = tipoBeneficio,
                Ativo = true
            };
            await _cartoes.AdicionarAsync(novo);
            return novo;
        }

        var cartao = await _cartoes.ObterAsync(modelo.Id.Value)
            ?? throw new InvalidOperationException("Cartão não encontrado.");

        if (cartao.Tipo != modelo.Tipo && await _cartoes.PossuiVinculosAsync(cartao.Id))
            throw new InvalidOperationException("Este cartão já tem movimentos e não pode mudar entre Crédito e Benefício.");

        cartao.Nome = nome;
        cartao.Bandeira = bandeira;
        cartao.UltimosDigitos = digitos;
        cartao.Tipo = modelo.Tipo;
        cartao.TipoBeneficio = tipoBeneficio;
        cartao.Ativo = modelo.Ativo;

        await _cartoes.AtualizarAsync(cartao);
        return cartao;
    }

    public async Task ExcluirAsync(int id)
    {
        var cartao = await _cartoes.ObterAsync(id)
            ?? throw new InvalidOperationException("Cartão não encontrado.");

        if (await _cartoes.PossuiVinculosAsync(id))
            throw new InvalidOperationException("Este cartão já tem compras, recargas ou faturas e não pode ser excluído. Inative-o para ocultá-lo.");

        await _cartoes.ExcluirAsync(cartao);
    }

    public async Task InativarAsync(int id)
    {
        var cartao = await _cartoes.ObterAsync(id)
            ?? throw new InvalidOperationException("Cartão não encontrado.");

        cartao.Ativo = false;
        await _cartoes.AtualizarAsync(cartao);
    }

    public Task<List<RecargaCartao>> ListarRecargasAsync(int cartaoId)
        => _cartoes.ListarRecargasAsync(cartaoId);

    public async Task<RecargaCartao> RecarregarAsync(RecargaCartaoFormModel modelo)
    {
        if (modelo.Data == default)
            throw new InvalidOperationException("Informe a data da recarga.");

        var valor = CestaDespesaRegras.Dinheiro(modelo.Valor);
        if (valor <= 0)
            throw new InvalidOperationException("Informe um valor de recarga maior que zero.");

        var cartao = await _cartoes.ObterAsync(modelo.CartaoId)
            ?? throw new InvalidOperationException("Cartão não encontrado.");

        if (cartao.Tipo != TipoCartao.Beneficio)
            throw new InvalidOperationException("Apenas cartões de benefício podem receber recarga.");

        if (!cartao.Ativo)
            throw new InvalidOperationException("Este cartão está inativo.");

        var recarga = new RecargaCartao
        {
            CartaoId = cartao.Id,
            Data = modelo.Data.Date,
            Valor = valor
        };

        await _cartoes.AdicionarRecargaAsync(recarga);
        return recarga;
    }

    public async Task ExcluirRecargaAsync(int recargaId)
    {
        var recarga = await _cartoes.ObterRecargaAsync(recargaId)
            ?? throw new InvalidOperationException("Recarga não encontrada.");

        var saldoSemRecarga = await ObterSaldoBeneficioAsync(recarga.CartaoId) - recarga.Valor;
        if (saldoSemRecarga < 0)
            throw new InvalidOperationException("Não é possível excluir esta recarga porque parte do saldo já foi usada em compras.");

        await _cartoes.ExcluirRecargaAsync(recarga);
    }

    public async Task<decimal> ObterSaldoBeneficioAsync(int cartaoId, int? ignorarCompraId = null)
    {
        var totalRecargas = await _cartoes.ObterTotalRecargasAsync(cartaoId);
        var totalUsado = await _cartoes.ObterTotalUsadoBeneficioAsync(cartaoId, ignorarCompraId);
        return CestaDespesaRegras.Dinheiro(totalRecargas - totalUsado);
    }

    public Task<List<Transacao>> ListarFaturasAbertasAsync(int cartaoId)
        => _cartoes.ListarFaturasAbertasAsync(cartaoId);

    private static string NormalizarTexto(string valor, int limite, string mensagemVazio)
    {
        var texto = valor?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(texto))
            throw new InvalidOperationException(mensagemVazio);
        if (texto.Length > limite)
            throw new InvalidOperationException($"O texto pode ter no máximo {limite} caracteres.");
        return texto;
    }

    private static string NormalizarDigitos(string valor)
    {
        var texto = valor?.Trim() ?? string.Empty;
        if (texto.Length != 4 || !texto.All(char.IsAsciiDigit))
            throw new InvalidOperationException("Informe exatamente os 4 últimos números do cartão.");
        return texto;
    }
}
