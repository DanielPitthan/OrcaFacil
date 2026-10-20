using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Llm;

/// <summary>
/// Valida o <c>produto_id</c> da ferramenta contra o catálogo e monta
/// <see cref="ItemSaidaComprovante"/>. Id inválido vira null e o item fica não identificado.
/// <see cref="ItemSaidaComprovante.Pendente"/> só destaca. Nada daqui grava compra.
/// </summary>
public static class CasamentoComprovante
{
    public const decimal CorteConfianca = 0.8m;

    public const string MensagemConfirmacaoObrigatoria =
        "Confirme cada item com produto antes de salvar.";

    public static IReadOnlyList<ItemSaidaComprovante> Casar(
        ComprovanteLido comprovante,
        IReadOnlyList<ProdutoCesta> catalogo)
    {
        ArgumentNullException.ThrowIfNull(comprovante);
        ArgumentNullException.ThrowIfNull(catalogo);
        return (comprovante.Itens ?? [])
            .Where(item => item is not null)
            .Select(item => Casar(item!, catalogo))
            .ToList();
    }

    public static ItemSaidaComprovante Casar(ItemComprovanteLido item, IReadOnlyList<ProdutoCesta> catalogo)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(catalogo);

        var confianca = ConfiancaNoIntervalo(item.ConfiancaNumero);
        var produtoId = IdValido(item, catalogo);
        return new ItemSaidaComprovante
        {
            DescricaoLida = item.DescricaoLida ?? string.Empty,
            ProdutoId = produtoId,
            Confianca = confianca,
            Pendente = produtoId is null || confianca < CorteConfianca
        };
    }

    /// <summary>
    /// Id informado que existe no catálogo. Ausente, nulo ou fora do catálogo devolve null.
    /// </summary>
    public static int? IdValido(ItemComprovanteLido item, IReadOnlyList<ProdutoCesta> catalogo)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(catalogo);
        if (!item.ProdutoIdInformado || item.ProdutoId is not int id || id <= 0)
            return null;

        return catalogo.Any(produto => produto is not null && produto.Id == id) ? id : null;
    }

    public static bool PodeSalvar(IReadOnlyList<ItemSaidaComprovante> itens)
    {
        ArgumentNullException.ThrowIfNull(itens);
        return itens.Count > 0 && itens.All(item => item is not null && item.PodeSalvar);
    }

    /// <summary>
    /// Barreira antes de gravar. Não abre transação e não chama <c>SaveChanges</c>.
    /// Todo item com <see cref="ItemSaidaComprovante.ProdutoId"/> precisa de <see cref="ItemSaidaComprovante.Confirmado"/>.
    /// </summary>
    public static void GarantirConfirmacao(IReadOnlyList<ItemSaidaComprovante> itens)
    {
        if (!PodeSalvar(itens))
            throw new InvalidOperationException(MensagemConfirmacaoObrigatoria);
    }

    private static decimal ConfiancaNoIntervalo(decimal? valor) =>
        valor is >= 0m and <= 1m ? valor.Value : 0m;
}
