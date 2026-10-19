using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Llm;

namespace OrcaFacil.Core.Cesta;

/// <summary>
/// Cópia da compra que o diálogo de confirmação devolve para a gravação.
/// O produto novo ainda não tem id: a chave e o nome digitado precisam ir juntos.
/// </summary>
public static class CompraCestaItens
{
    public static List<ItemCompraCestaFormModel> CopiarItens(CompraCestaFormModel modelo)
    {
        ArgumentNullException.ThrowIfNull(modelo);
        return modelo.Itens.Select(item => Copiar(item, modelo.ProdutosNovos)).ToList();
    }

    public static ItemCompraCestaFormModel Copiar(
        ItemCompraCestaFormModel item,
        IReadOnlyList<ProdutoNovoCompra> produtosNovos)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(produtosNovos);

        var definicao = string.IsNullOrEmpty(item.ProdutoNovoChave)
            ? null
            : produtosNovos.FirstOrDefault(produto => produto.Chave == item.ProdutoNovoChave);
        var nome = item.NomePendente;
        var categoria = item.CategoriaPendente;
        if (definicao is not null)
        {
            if (!string.IsNullOrWhiteSpace(definicao.Nome))
                nome = definicao.Nome;
            if (!string.IsNullOrWhiteSpace(definicao.CategoriaNome))
                categoria = definicao.CategoriaNome;
        }

        return new ItemCompraCestaFormModel
        {
            ProdutoCestaId = item.ProdutoCestaId,
            ProdutoNovoChave = item.ProdutoNovoChave,
            NomePendente = string.IsNullOrWhiteSpace(nome) ? null : nome.Trim(),
            CategoriaPendente = string.IsNullOrWhiteSpace(categoria) ? null : categoria.Trim(),
            Preco = item.Preco
        };
    }

    /// <summary>
    /// Catálogo que a compra enxerga. Id 0 não é produto cadastrado: o novo entra pelo nome pendente.
    /// </summary>
    public static List<ProdutoCesta> FiltrarProdutos(
        IEnumerable<ProdutoCesta> produtos,
        CompraCestaFormModel modelo)
    {
        ArgumentNullException.ThrowIfNull(produtos);
        ArgumentNullException.ThrowIfNull(modelo);

        var visiveis = produtos
            .Where(produto => produto.Id > 0 && (produto.Ativo || modelo.Itens.Any(item => item.ProdutoCestaId == produto.Id)))
            .ToList();

        foreach (var novo in modelo.ProdutosNovos)
        {
            if (string.IsNullOrWhiteSpace(novo.Nome))
                continue;

            if (visiveis.Any(produto => NormalizadorComprovante.TextoComparacao(produto.Nome) == novo.Chave))
                continue;

            visiveis.Add(new ProdutoCesta
            {
                Nome = novo.Nome.Trim(),
                CategoriaId = novo.CategoriaId,
                Ativo = true,
                Categoria = string.IsNullOrWhiteSpace(novo.CategoriaNome)
                    ? null
                    : new Categoria { Id = novo.CategoriaId, Nome = novo.CategoriaNome.Trim() }
            });
        }

        return visiveis;
    }

    public static string Titulo(ItemCompraCestaFormModel item, IEnumerable<ProdutoCesta> produtos)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(produtos);

        if (!string.IsNullOrEmpty(item.ProdutoNovoChave) && !string.IsNullOrWhiteSpace(item.NomePendente))
            return item.NomePendente.Trim();

        if (item.ProdutoCestaId > 0)
            return produtos.FirstOrDefault(produto => produto.Id == item.ProdutoCestaId)?.Nome ?? "Produto";

        return string.IsNullOrWhiteSpace(item.NomePendente) ? "Produto" : item.NomePendente.Trim();
    }
}
