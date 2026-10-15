using OrcaFacil.Core.Cesta;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Services.Interfaces;

public interface ICestaService
{
    Task<List<ProdutoCesta>> ListarProdutosAsync(bool somenteAtivos = false);
    Task<ProdutoCesta> SalvarProdutoAsync(ProdutoCestaFormModel modelo);
    Task<ResultadoImportacaoProdutos> ImportarProdutosAsync(IReadOnlyList<ProdutoCestaCsv.Linha> linhas, IReadOnlyList<Categoria> categorias, int? categoriaPadraoId);
    Task ExcluirProdutoAsync(int id);
    Task InativarProdutoAsync(int id);

    Task<List<LocalCompra>> ListarLocaisAsync(bool somenteAtivos = false);
    Task<LocalCompra> SalvarLocalAsync(LocalCompraFormModel modelo);
    Task ExcluirLocalAsync(int id);
    Task InativarLocalAsync(int id);

    Task<List<CompraCesta>> ListarComprasAsync(int mes, int ano);
    Task<CompraCesta> SalvarCompraAsync(CompraCestaFormModel modelo);
    Task ExcluirCompraAsync(int id);
    Task<List<CompraCestaFormModel>> ListarRascunhosAsync(int mes, int ano);
    Task<ComparativoCesta> CompararAsync(int mes, int ano);
}
