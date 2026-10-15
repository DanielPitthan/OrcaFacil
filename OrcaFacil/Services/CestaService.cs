using OrcaFacil.Core.Cesta;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Core.Services;
using OrcaFacil.Services.Interfaces;

namespace OrcaFacil.Services;

public class CestaService : ICestaService
{
    private readonly ICestaRepository _cestas;
    private readonly CestaCadastroService _cadastro;
    private readonly IPerfilContext _perfil;
    private readonly IFechamentoService _fechamento;

    public CestaService(ICestaRepository cestas, ICartaoRepository cartoes, IPerfilContext perfil, IFechamentoService fechamento)
    {
        _cestas = cestas;
        _cadastro = new CestaCadastroService(cestas, cartoes);
        _perfil = perfil;
        _fechamento = fechamento;
    }

    public Task<List<ProdutoCesta>> ListarProdutosAsync(bool somenteAtivos = false) => _cadastro.ListarProdutosAsync(somenteAtivos);

    public Task<ProdutoCesta> SalvarProdutoAsync(ProdutoCestaFormModel modelo) => _cadastro.SalvarProdutoAsync(modelo);

    public Task<ResultadoImportacaoProdutos> ImportarProdutosAsync(IReadOnlyList<ProdutoCestaCsv.Linha> linhas, IReadOnlyList<Categoria> categorias, int? categoriaPadraoId)
        => _cadastro.ImportarProdutosAsync(linhas, categorias, categoriaPadraoId);

    public Task ExcluirProdutoAsync(int id) => _cadastro.ExcluirProdutoAsync(id);

    public Task InativarProdutoAsync(int id) => _cadastro.InativarProdutoAsync(id);

    public Task<List<LocalCompra>> ListarLocaisAsync(bool somenteAtivos = false) => _cadastro.ListarLocaisAsync(somenteAtivos);

    public Task<LocalCompra> SalvarLocalAsync(LocalCompraFormModel modelo) => _cadastro.SalvarLocalAsync(modelo);

    public Task ExcluirLocalAsync(int id) => _cadastro.ExcluirLocalAsync(id);

    public Task InativarLocalAsync(int id) => _cadastro.InativarLocalAsync(id);

    public Task<List<CompraCesta>> ListarComprasAsync(int mes, int ano) => _cadastro.ListarComprasAsync(mes, ano);

    public async Task<CompraCesta> SalvarCompraAsync(CompraCestaFormModel modelo)
    {
        var membroId = _perfil.Atual?.Id ?? throw new InvalidOperationException("Nenhum perfil ativo.");
        await _fechamento.GarantirPeriodoAbertoAsync(modelo.Data);
        if (modelo.Id is int id)
        {
            var atual = await _cadastro.ObterCompraAsync(id)
                ?? throw new InvalidOperationException("Compra não encontrada.");
            await _fechamento.GarantirPeriodoAbertoAsync(atual.Data);
        }

        return await _cadastro.SalvarCompraAsync(modelo, membroId);
    }

    public async Task ExcluirCompraAsync(int id)
    {
        var compra = await _cadastro.ObterCompraAsync(id)
            ?? throw new InvalidOperationException("Compra não encontrada.");
        await _fechamento.GarantirPeriodoAbertoAsync(compra.Data);
        await _cadastro.ExcluirCompraAsync(id);
    }

    public async Task<List<CompraCestaFormModel>> ListarRascunhosAsync(int mes, int ano)
    {
        var compras = await _cestas.ListarComprasAsync(null, null);
        return CestaComparacaoRegras.Rascunhos(compras.Select(Mapear).ToList(), mes, ano)
            .Select(rascunho => new CompraCestaFormModel
            {
                LocalCompraId = rascunho.LocalId,
                Data = rascunho.Data,
                ValorBeneficio = rascunho.ValorBeneficio,
                TipoCartaoBeneficio = rascunho.TipoBeneficio,
                ValorDinheiro = rascunho.ValorDinheiro,
                ValorCartaoCredito = rascunho.ValorCartaoCredito,
                Itens = rascunho.Itens.Select(item => new ItemCompraCestaFormModel
                {
                    ProdutoCestaId = item.ProdutoId,
                    Preco = item.Preco
                }).ToList()
            })
            .ToList();
    }

    public async Task<ComparativoCesta> CompararAsync(int mes, int ano)
    {
        var compras = await _cestas.ListarComprasAsync(null, null);
        return CestaComparacaoRegras.Montar(compras.Select(Mapear).ToList(), mes, ano);
    }

    private static CompraComparacao Mapear(CompraCesta compra) => new(
        compra.Id,
        compra.LocalCompraId,
        compra.LocalCompra?.Nome ?? "Local",
        compra.Data,
        compra.ValorBeneficio,
        compra.TipoCartaoBeneficio,
        compra.Itens
            .OrderBy(i => i.Ordem)
            .ThenBy(i => i.Id)
            .Select(i => new ItemComparacao(i.ProdutoCestaId, i.Produto?.Nome ?? "Produto", i.Preco))
            .ToList(),
        compra.ValorDinheiro,
        compra.ValorCartaoCredito);
}
