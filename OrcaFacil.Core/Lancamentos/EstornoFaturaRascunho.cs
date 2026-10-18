using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Lancamentos;

/// <summary>Pedido do sheet de estorno. O texto livre vai para o descritivo; a magnitude é sempre positiva.</summary>
public sealed record PedidoEstorno(decimal Valor, int CategoriaId, string? Descritivo);

/// <summary>
/// Aplica o estorno no rascunho da tela com as mesmas regras do serviço:
/// valor maior que zero, mês fechado recusado e total líquido de pelo menos R$ 0,01.
/// Fatura já gravada passa pelo serviço de estorno e só espelha o resultado aqui.
/// </summary>
public static class EstornoFaturaRascunho
{
    public static ItemLancamentoFormModel Lancar(
        TransacaoFormModel fatura,
        PedidoEstorno pedido,
        string? nomeCategoria,
        bool mesFechado)
    {
        GarantirMesAberto(fatura, mesFechado);
        pedido = pedido with { Valor = DinheiroEstorno.Arredondar(pedido.Valor) };
        var item = CriarItem(fatura.Itens, pedido, nomeCategoria);
        var novoTotal = DinheiroEstorno.Arredondar(fatura.Valor - pedido.Valor);
        var previstos = fatura.Itens.Append(item).ToList();
        FaturaCartaoEditor.ValidarItens(
            novoTotal, fatura.CategoriaId, permiteItens: true, fatura.Tipo, previstos);

        fatura.Valor = novoTotal;
        fatura.Itens.Add(item);
        return item;
    }

    public static void Editar(
        TransacaoFormModel fatura,
        ItemLancamentoFormModel item,
        PedidoEstorno pedido,
        string? nomeCategoria,
        bool mesFechado)
    {
        if (!item.EhEstorno)
            throw new InvalidOperationException("Só um estorno pode ser editado por este caminho.");

        GarantirMesAberto(fatura, mesFechado);
        pedido = pedido with { Valor = DinheiroEstorno.Arredondar(pedido.Valor) };
        ExigirItem(pedido);

        var descritivo = ItensFaturaInclusao.NormalizarDescritivo(pedido.Descritivo);
        var descricao = Nome(nomeCategoria);
        var valorAnterior = item.Valor;
        var descricaoAnterior = item.Descricao;
        var descritivoAnterior = item.Descritivo;
        var categoriaAnterior = item.CategoriaId;
        var naturezaAnterior = item.Natureza;
        var parcelaAnterior = item.ParcelaAtual;
        var totalParcelasAnterior = item.TotalParcelas;

        item.Valor = pedido.Valor;
        item.Descricao = descricao;
        item.Descritivo = descritivo;
        item.CategoriaId = pedido.CategoriaId;
        item.Natureza = NaturezaItemLancamento.Avulsa;
        item.ParcelaAtual = null;
        item.TotalParcelas = null;

        var novoTotal = DinheiroEstorno.Arredondar(fatura.Valor + valorAnterior - pedido.Valor);
        try
        {
            FaturaCartaoEditor.ValidarItens(
                novoTotal, fatura.CategoriaId, permiteItens: true, fatura.Tipo, fatura.Itens);
        }
        catch
        {
            item.Valor = valorAnterior;
            item.Descricao = descricaoAnterior;
            item.Descritivo = descritivoAnterior;
            item.CategoriaId = categoriaAnterior;
            item.Natureza = naturezaAnterior;
            item.ParcelaAtual = parcelaAnterior;
            item.TotalParcelas = totalParcelasAnterior;
            throw;
        }

        fatura.Valor = novoTotal;
    }

    /// <summary>
    /// Tira o estorno da lista e devolve a magnitude ao total.
    /// Compra não passa por aqui: excluí-la não mexe no total da fatura.
    /// </summary>
    public static void Excluir(TransacaoFormModel fatura, ItemLancamentoFormModel item, bool mesFechado)
    {
        if (!item.EhEstorno)
            throw new InvalidOperationException("Só um estorno altera o total da fatura ao ser excluído.");

        GarantirMesAberto(fatura, mesFechado);
        if (!fatura.Itens.Contains(item))
            throw new InvalidOperationException("Item não encontrado.");

        fatura.Valor = DinheiroEstorno.Arredondar(fatura.Valor + item.Valor);
        fatura.Itens.Remove(item);
    }

    /// <summary>Copia o lançamento já gravado para a lista, abatendo a magnitude uma vez e indo para o fim.</summary>
    public static void EspelharLancamento(TransacaoFormModel fatura, ItemLancamento gravado)
    {
        var item = ParaFormulario(gravado);
        item.Ordem = ItensFaturaInclusao.ProximaOrdem(fatura.Itens);
        fatura.Valor = DinheiroEstorno.Arredondar(fatura.Valor - gravado.Valor);
        fatura.Itens.Add(item);
    }

    public static void EspelharEdicao(TransacaoFormModel fatura, ItemLancamentoFormModel item, ItemLancamento gravado)
    {
        fatura.Valor = DinheiroEstorno.Arredondar(fatura.Valor + item.Valor - gravado.Valor);
        item.Id = gravado.Id;
        item.Descricao = gravado.Descricao;
        item.Descritivo = gravado.Descritivo;
        item.Valor = gravado.Valor;
        item.EhEstorno = true;
        item.Natureza = NaturezaItemLancamento.Avulsa;
        item.ParcelaAtual = null;
        item.TotalParcelas = null;
        item.CategoriaId = gravado.CategoriaId;
        item.Ordem = gravado.Ordem;
        item.HashImportacao = gravado.HashImportacao;
    }

    public static void EspelharExclusao(TransacaoFormModel fatura, ItemLancamentoFormModel item)
    {
        if (item.EhEstorno)
            fatura.Valor = DinheiroEstorno.Arredondar(fatura.Valor + item.Valor);

        fatura.Itens.Remove(item);
    }

    public static ItemLancamentoFormModel ParaFormulario(ItemLancamento item) => new()
    {
        Id = item.Id,
        Descricao = item.Descricao,
        Descritivo = item.Descritivo,
        Valor = item.Valor,
        EhEstorno = item.EhEstorno,
        Natureza = item.Natureza,
        ParcelaAtual = item.ParcelaAtual,
        TotalParcelas = item.TotalParcelas,
        CategoriaId = item.CategoriaId,
        Ordem = item.Ordem,
        HashImportacao = item.HashImportacao
    };

    /// <summary>Texto do sheet: o descritivo, ou a descrição antiga quando ela não é o nome da categoria.</summary>
    public static string? TextoParaEdicao(ItemLancamentoFormModel item, string? nomeCategoria)
    {
        if (ItensFaturaInclusao.TemDescritivo(item.Descritivo))
            return item.Descritivo!.Trim();

        if (string.IsNullOrWhiteSpace(item.Descricao))
            return null;

        var descricao = item.Descricao.Trim();
        if (!string.IsNullOrWhiteSpace(nomeCategoria)
            && string.Equals(descricao, nomeCategoria.Trim(), StringComparison.CurrentCultureIgnoreCase))
            return null;

        return descricao;
    }

    private static ItemLancamentoFormModel CriarItem(
        IEnumerable<ItemLancamentoFormModel> itens,
        PedidoEstorno pedido,
        string? nomeCategoria)
    {
        ExigirItem(pedido);
        return new ItemLancamentoFormModel
        {
            Descricao = Nome(nomeCategoria),
            Descritivo = ItensFaturaInclusao.NormalizarDescritivo(pedido.Descritivo),
            Valor = pedido.Valor,
            EhEstorno = true,
            Natureza = NaturezaItemLancamento.Avulsa,
            CategoriaId = pedido.CategoriaId,
            Ordem = ItensFaturaInclusao.ProximaOrdem(itens)
        };
    }

    private static void ExigirItem(PedidoEstorno pedido)
    {
        var erro = FaturaCartaoEditor.ValidarItem(
            pedido.CategoriaId,
            pedido.Valor,
            NaturezaItemLancamento.Avulsa,
            parcelaAtual: null,
            totalParcelas: null,
            ehEstorno: true);
        if (erro is not null)
            throw new InvalidOperationException(erro);
    }

    private static void GarantirMesAberto(TransacaoFormModel fatura, bool mesFechado)
    {
        if (mesFechado)
            throw new InvalidOperationException(ItensFaturaRegras.MensagemMesFechado(fatura.Data));
    }

    private static string Nome(string? nomeCategoria)
    {
        var descricao = string.IsNullOrWhiteSpace(nomeCategoria) ? "Estorno" : nomeCategoria.Trim();
        return descricao.Length > 120 ? descricao[..120] : descricao;
    }
}
