using System.Globalization;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
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

    public static decimal Soma(IEnumerable<ItemLancamentoFormModel> itens)
        => itens.Sum(ItensFaturaRegras.ValorAssinado);

    /// <summary>
    /// "A detalhar" e a barra usam a soma com sinal do estorno.
    /// A barra fica entre 0 e 100.
    /// </summary>
    public static ResumoClassificacaoFatura Classificar(decimal total, IEnumerable<ItemLancamentoFormModel> itens)
    {
        var soma = Soma(itens);
        return new ResumoClassificacaoFatura(soma, ADetalhar(total, soma), PercentualClassificado(total, soma));
    }

    public static IReadOnlyList<ItemLancamentoFormModel> Ordenar(IEnumerable<ItemLancamentoFormModel> itens)
        => itens.OrderBy(i => i.Ordem).ToList();

    public static string TituloLinha(string? descritivo, string? descricao, string? categoria, bool ehEstorno)
    {
        if (ehEstorno)
        {
            var reserva = string.IsNullOrWhiteSpace(descricao) ? categoria : descricao;
            return ItensFaturaInclusao.Titulo(descritivo, string.IsNullOrWhiteSpace(reserva) ? "Estorno" : reserva);
        }

        return ItensFaturaInclusao.Titulo(descritivo, categoria ?? descricao ?? "");
    }

    public static string SubtituloLinha(
        string? categoria,
        bool ehEstorno,
        NaturezaItemLancamento natureza,
        int? parcelaAtual,
        int? totalParcelas)
    {
        if (!ehEstorno)
            return Subtitulo(categoria, natureza, parcelaAtual, totalParcelas);

        var nome = string.IsNullOrWhiteSpace(categoria) ? "Sem categoria" : categoria.Trim();
        return $"{nome} · Estorno";
    }

    public static decimal Soma(IEnumerable<ItemLancamento> itens)
        => itens.Sum(ItensFaturaRegras.ValorAssinado);

    public static decimal Restante(decimal total, decimal soma) => total - soma;

    /// <summary>
    /// Soma assinada sem o item em edição. A soma já traz o estorno negativo;
    /// subtrair a magnitude bruta encolhe o crédito outra vez.
    /// Estorno de 30 com líquido classificado 50 devolve 80.
    /// </summary>
    public static decimal SomaForaDoEditado(decimal somaAssinada, ItemLancamentoFormModel? editando)
        => editando is null ? somaAssinada : somaAssinada - ItensFaturaRegras.ValorAssinado(editando);

    /// <summary>Restante da fatura enquanto um item está em edição, com o sinal do estorno.</summary>
    public static decimal RestanteAoEditar(decimal total, decimal somaAssinada, ItemLancamentoFormModel? editando)
        => Restante(total, SomaForaDoEditado(somaAssinada, editando));

    public static decimal ADetalhar(decimal total, decimal soma) => Math.Max(0m, total - soma);

    public static bool Estoura(int quantidade, decimal soma, decimal total)
        => quantidade > 0 && soma > total;

    public static double PercentualClassificado(decimal total, decimal soma)
    {
        if (total <= 0)
            return 100d;

        if (soma <= 0)
            return 0d;

        return (double)Math.Min(100m, soma / total * 100m);
    }

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
        int? totalParcelas,
        bool ehEstorno = false)
    {
        if (categoriaId <= 0)
            return "Selecione a despesa.";

        if (valor <= 0)
            return "Informe o valor do item.";

        if (ehEstorno && natureza != NaturezaItemLancamento.Avulsa)
            return "Estorno só pode ser avulso.";

        if (natureza == NaturezaItemLancamento.Parcelada
            && (parcelaAtual is not > 0 || totalParcelas is not > 0 || parcelaAtual > totalParcelas))
        {
            return "Informe a parcela atual e o total, por exemplo 2 de 12.";
        }

        return null;
    }

    /// <summary>
    /// Mesma regra de <see cref="ItensFaturaRegras.Validar"/>, a partir do rascunho da tela.
    /// A cesta e o salvamento da fatura passam pela regra de itens; esta entrada cobre o editor.
    /// </summary>
    public static void ValidarItens(
        decimal total,
        int categoriaFaturaId,
        bool permiteItens,
        TipoTransacao tipo,
        IReadOnlyList<ItemLancamentoFormModel> itens)
    {
        var entidades = itens.Select(item => new ItemLancamento
        {
            Descricao = item.Descricao,
            Descritivo = item.Descritivo,
            Valor = item.Valor,
            EhEstorno = item.EhEstorno,
            Natureza = item.Natureza,
            ParcelaAtual = item.ParcelaAtual,
            TotalParcelas = item.TotalParcelas,
            CategoriaId = item.CategoriaId
        }).ToList();

        ItensFaturaRegras.Validar(total, categoriaFaturaId, permiteItens, tipo, entidades);
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
        var erro = ValidarItem(categoriaId, valor, natureza, parcelaAtual, totalParcelas, item.EhEstorno);
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
        EhEstorno = item.EhEstorno,
        Natureza = item.Natureza,
        ParcelaAtual = item.ParcelaAtual,
        TotalParcelas = item.TotalParcelas,
        CategoriaId = item.CategoriaId,
        Ordem = item.Ordem
    };
}

/// <summary>Soma com sinal, o que falta detalhar e o percentual da barra (0–100).</summary>
public readonly record struct ResumoClassificacaoFatura(decimal SomaAssinada, decimal ADetalhar, double Percentual);
