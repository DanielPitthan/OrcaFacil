using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Repositories;

public interface ICestaRepository
{
    Task<List<ProdutoCesta>> ListarProdutosAsync(bool somenteAtivos);
    Task<ProdutoCesta?> ObterProdutoAsync(int id);
    Task<bool> ExisteNomeProdutoAsync(string nome, int? ignorarId = null);
    Task<bool> ProdutoPossuiComprasAsync(int produtoId);
    Task AdicionarProdutoAsync(ProdutoCesta produto);
    Task AtualizarProdutoAsync(ProdutoCesta produto);
    Task ExcluirProdutoAsync(ProdutoCesta produto);

    Task<List<LocalCompra>> ListarLocaisAsync(bool somenteAtivos);
    Task<LocalCompra?> ObterLocalAsync(int id);
    Task<bool> ExisteNomeLocalAsync(string nome, int? ignorarId = null);
    Task<bool> LocalPossuiComprasAsync(int localId);
    Task AdicionarLocalAsync(LocalCompra local);
    Task AtualizarLocalAsync(LocalCompra local);
    Task ExcluirLocalAsync(LocalCompra local);

    Task<List<CompraCesta>> ListarComprasAsync(int? mes, int? ano);
    Task<CompraCesta?> ObterCompraAsync(int id);
    Task<CompraCesta> SalvarCompraAsync(
        CompraCesta compra,
        Transacao? despesa,
        Core.Cesta.FaturaCreditoCesta? faturaCredito = null,
        IReadOnlyList<ProdutoNovoCompra>? produtosNovos = null);
    Task ExcluirCompraAsync(int id);

    Task<int> ObterOuCriarCategoriaCestaAsync();
    Task<int> ObterOuCriarCategoriaCartaoCreditoAsync();

    Task<string> TextoCatalogoLeituraAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProdutoCesta>> ProdutosCatalogoLeituraAsync(CancellationToken cancellationToken = default);
}
