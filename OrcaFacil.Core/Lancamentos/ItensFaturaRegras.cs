using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Lancamentos;

public static class ItensFaturaRegras
{
    public const decimal TotalMinimo = 0.01m;

    /// <summary>Compra entra positiva; estorno, com a mesma magnitude e sinal negativo.</summary>
    public static decimal ValorAssinado(ItemLancamento item)
        => item.EhEstorno ? -item.Valor : item.Valor;

    /// <summary>Compra entra positiva; estorno, com a mesma magnitude e sinal negativo.</summary>
    public static decimal ValorAssinado(ItemLancamentoFormModel item)
        => item.EhEstorno ? -item.Valor : item.Valor;

    public static string MensagemMesFechado(DateTime data)
        => $"O mês {data:MM/yyyy} está fechado. Reabra o período para alterar lançamentos.";

    public static void Validar(decimal valorFatura, int categoriaFaturaId, bool permiteItens, TipoTransacao tipo, IReadOnlyList<ItemLancamento> itens)
    {
        if (itens.Count == 0)
            return;

        if (tipo != TipoTransacao.Despesa || !permiteItens)
            throw new InvalidOperationException("Esta categoria não permite itens na fatura.");

        if (valorFatura < TotalMinimo)
            throw new InvalidOperationException("O total líquido da fatura precisa ser de pelo menos R$ 0,01.");

        if (itens.Any(i => string.IsNullOrWhiteSpace(i.Descricao) || i.Descricao.Length > 120))
            throw new InvalidOperationException("Informe a descrição de cada item da fatura.");

        if (itens.Any(i => i.Descritivo is not null && i.Descritivo.Trim().Length > ItensFaturaInclusao.DescritivoMaximo))
            throw new InvalidOperationException($"O descritivo pode ter no máximo {ItensFaturaInclusao.DescritivoMaximo} caracteres.");

        if (itens.Any(i => i.Valor <= 0))
            throw new InvalidOperationException("Cada item da fatura precisa de um valor maior que zero.");

        if (itens.Any(i => i.EhEstorno && i.Natureza != NaturezaItemLancamento.Avulsa))
            throw new InvalidOperationException("Estorno só pode ser avulso.");

        if (itens.Any(i => i.CategoriaId <= 0))
            throw new InvalidOperationException("Selecione a categoria de cada item da fatura.");

        if (itens.Any(i => i.CategoriaId == categoriaFaturaId))
            throw new InvalidOperationException("O item precisa de uma categoria diferente da fatura.");

        if (itens.Sum(ValorAssinado) > valorFatura)
            throw new InvalidOperationException("A soma dos itens não pode passar do total da fatura.");

        foreach (var item in itens.Where(i => i.Natureza == NaturezaItemLancamento.Parcelada))
        {
            if (item.ParcelaAtual is null || item.TotalParcelas is null
                || item.ParcelaAtual < 1 || item.TotalParcelas < 1
                || item.ParcelaAtual > item.TotalParcelas)
            {
                throw new InvalidOperationException("Informe a parcela atual e o total de parcelas.");
            }
        }
    }

    public static bool DeveCopiar(Transacao origem)
    {
        if (origem.Itens.Count == 0)
            return origem.Recorrencia != TipoRecorrencia.Unica;

        return SelecionarParaCopia(origem.Itens).Count > 0;
    }

    public static List<ItemLancamento> SelecionarParaCopia(IEnumerable<ItemLancamento> itens)
    {
        var copias = new List<ItemLancamento>();
        foreach (var item in itens.OrderBy(i => i.Ordem).ThenBy(i => i.Id))
        {
            if (item.EhEstorno)
                continue;

            if (item.Natureza == NaturezaItemLancamento.Assinatura)
            {
                copias.Add(Clonar(item, item.ParcelaAtual, item.TotalParcelas));
                continue;
            }

            if (item.Natureza == NaturezaItemLancamento.Parcelada
                && item.ParcelaAtual is int atual
                && item.TotalParcelas is int total
                && atual < total)
            {
                copias.Add(Clonar(item, atual + 1, total));
            }
        }

        for (var i = 0; i < copias.Count; i++)
            copias[i].Ordem = i;

        return copias;
    }

    public static decimal ValorDaCopia(Transacao origem, IReadOnlyList<ItemLancamento> itensCopiados)
        => origem.Itens.Count == 0 ? origem.Valor : itensCopiados.Sum(ValorAssinado);

    private static ItemLancamento Clonar(ItemLancamento item, int? parcelaAtual, int? totalParcelas) => new()
    {
        Descricao = item.Descricao,
        Descritivo = string.IsNullOrWhiteSpace(item.Descritivo) ? null : item.Descritivo.Trim(),
        Valor = item.Valor,
        EhEstorno = false,
        Natureza = item.Natureza,
        ParcelaAtual = item.Natureza == NaturezaItemLancamento.Parcelada ? parcelaAtual : null,
        TotalParcelas = item.Natureza == NaturezaItemLancamento.Parcelada ? totalParcelas : null,
        CategoriaId = item.CategoriaId,
        Ordem = item.Ordem
    };
}
