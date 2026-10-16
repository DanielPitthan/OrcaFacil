using System.Globalization;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Lancamentos;

public enum StatusFaturaCartao
{
    Paga,
    Pendente,
    Atrasada
}

/// <summary>
/// Regras de apresentação e de rascunho da fatura de cartão.
/// Não altera o contrato de persistência: a validação final continua em <see cref="ItensFaturaRegras"/>.
/// </summary>
public static class FaturaCartaoEditor
{
    public const int ParcelasMinimas = 2;
    public const int CategoriasEmDestaque = 6;

    public static StatusFaturaCartao Status(bool pago, DateTime data, DateTime hoje)
    {
        if (pago)
            return StatusFaturaCartao.Paga;

        return data.Date < hoje.Date ? StatusFaturaCartao.Atrasada : StatusFaturaCartao.Pendente;
    }

    public static string RotuloStatus(StatusFaturaCartao status) => status switch
    {
        StatusFaturaCartao.Paga => "Paga",
        StatusFaturaCartao.Atrasada => "Atrasada",
        _ => "Pendente"
    };

    public static decimal Soma(IEnumerable<decimal> valores) => valores.Sum();

    public static decimal Restante(decimal total, decimal soma) => total - soma;

    public static decimal ADetalhar(decimal total, decimal soma) => Math.Max(0m, total - soma);

    public static bool Estoura(int quantidade, decimal soma, decimal total)
        => quantidade > 0 && soma > total;

    public static double PercentualClassificado(decimal total, decimal soma)
        => total <= 0 ? 100d : (double)Math.Min(100m, soma / total * 100m);

    public static (string Simbolo, string Numero) PartirMoeda(decimal valor, CultureInfo cultura)
    {
        var formato = cultura.NumberFormat;
        return (formato.CurrencySymbol, valor.ToString("N2", cultura));
    }

    public static string Subtitulo(string? categoria, NaturezaItemLancamento natureza, int? parcelaAtual, int? totalParcelas)
    {
        var nome = string.IsNullOrWhiteSpace(categoria) ? "Sem categoria" : categoria.Trim();
        return $"{nome} · {RotuloNatureza(natureza, parcelaAtual, totalParcelas)}";
    }

    public static string RotuloNatureza(NaturezaItemLancamento natureza, int? parcelaAtual, int? totalParcelas) => natureza switch
    {
        NaturezaItemLancamento.Parcelada => $"Parcela {parcelaAtual}/{totalParcelas}",
        NaturezaItemLancamento.Assinatura => "Assinatura",
        _ => "Avulsa"
    };

    public static string TextoApoioPago(bool pago)
        => pago ? "A fatura entra como paga." : "A fatura continua pendente até o pagamento.";

    public static string? ValidarItem(
        int categoriaId,
        decimal valor,
        NaturezaItemLancamento natureza,
        int? parcelaAtual,
        int? totalParcelas)
    {
        if (categoriaId <= 0)
            return "Selecione a despesa.";

        if (valor <= 0)
            return "Informe o valor do item.";

        if (natureza == NaturezaItemLancamento.Parcelada
            && (parcelaAtual is not > 0 || totalParcelas is not > 0 || parcelaAtual > totalParcelas))
        {
            return "Informe a parcela atual e o total, por exemplo 2 de 12.";
        }

        return null;
    }

    public static (int? ParcelaAtual, int? TotalParcelas) AoEscolherNatureza(
        NaturezaItemLancamento natureza,
        int? parcelaAtual,
        int? totalParcelas)
    {
        if (natureza != NaturezaItemLancamento.Parcelada)
            return (null, null);

        var total = totalParcelas is >= ParcelasMinimas ? totalParcelas.Value : ParcelasMinimas;
        var atual = parcelaAtual is > 0 ? Math.Min(parcelaAtual.Value, total) : 1;
        return (atual, total);
    }

    public static int AjustarTotal(int total, int delta)
    {
        if (delta == 0)
            return total;

        if (delta < 0)
        {
            if (total <= ParcelasMinimas)
                return total;

            var reduzido = total + delta;
            return reduzido < ParcelasMinimas ? ParcelasMinimas : reduzido;
        }

        return total > int.MaxValue - delta ? total : total + delta;
    }

    public static int AjustarParcelaAtual(int atual, int delta, int total)
    {
        var limite = Math.Max(total, 1);
        var novo = atual + delta;
        if (novo < 1)
            return 1;

        return novo > limite ? limite : novo;
    }

    public static IReadOnlyList<T> EmDestaque<T>(IReadOnlyList<T> todas, Func<T, bool> selecionada, int limite = CategoriasEmDestaque)
        where T : class
    {
        if (limite < 1)
            limite = 1;

        var destaque = todas.Take(limite).ToList();
        var escolhida = todas.FirstOrDefault(selecionada);
        if (escolhida is not null && !destaque.Any(selecionada))
            destaque.Add(escolhida);

        return destaque;
    }

    public static void AplicarEdicao(
        ItemLancamentoFormModel item,
        int categoriaId,
        string nomeCategoria,
        decimal valor,
        NaturezaItemLancamento natureza,
        int? parcelaAtual,
        int? totalParcelas,
        string? descritivo)
    {
        var erro = ValidarItem(categoriaId, valor, natureza, parcelaAtual, totalParcelas);
        if (erro is not null)
            throw new InvalidOperationException(erro);

        var descricao = string.IsNullOrWhiteSpace(nomeCategoria) ? "Item" : nomeCategoria.Trim();
        if (descricao.Length > 120)
            descricao = descricao[..120];

        item.CategoriaId = categoriaId;
        item.Descricao = descricao;
        item.Descritivo = ItensFaturaInclusao.NormalizarDescritivo(descritivo);
        item.Valor = valor;
        item.Natureza = natureza;
        item.ParcelaAtual = natureza == NaturezaItemLancamento.Parcelada ? parcelaAtual : null;
        item.TotalParcelas = natureza == NaturezaItemLancamento.Parcelada ? totalParcelas : null;
    }

    public static TransacaoFormModel ClonarComoNova(TransacaoFormModel origem) => new()
    {
        Descricao = origem.Descricao,
        Valor = origem.Valor,
        Data = origem.Data,
        Tipo = origem.Tipo,
        Recorrencia = origem.Recorrencia,
        Pago = origem.Pago,
        Observacao = origem.Observacao,
        CategoriaId = origem.CategoriaId,
        CartaoId = origem.CartaoId,
        Itens = origem.Itens.Select(ClonarItem).ToList()
    };

    public static string RotuloRecorrencia(TipoRecorrencia recorrencia) => recorrencia switch
    {
        TipoRecorrencia.Semanal => "Semanal",
        TipoRecorrencia.Mensal => "Mensal",
        TipoRecorrencia.Anual => "Anual",
        _ => "Única"
    };

    public static string ContagemItens(int quantidade) => quantidade switch
    {
        0 => "Nenhum item",
        1 => "1 item",
        _ => $"{quantidade} itens"
    };

    private static ItemLancamentoFormModel ClonarItem(ItemLancamentoFormModel item) => new()
    {
        Descricao = item.Descricao,
        Descritivo = item.Descritivo,
        Valor = item.Valor,
        Natureza = item.Natureza,
        ParcelaAtual = item.ParcelaAtual,
        TotalParcelas = item.TotalParcelas,
        CategoriaId = item.CategoriaId
    };
}
