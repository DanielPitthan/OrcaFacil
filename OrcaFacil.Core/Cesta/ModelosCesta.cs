using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Cesta;

public sealed record ItemComparacao(int ProdutoId, string ProdutoNome, decimal Preco);

public sealed record CompraComparacao(
    int Id,
    int LocalId,
    string LocalNome,
    DateTime Data,
    decimal ValorBeneficio,
    TipoCartaoBeneficio? TipoBeneficio,
    IReadOnlyList<ItemComparacao> Itens,
    decimal ValorDinheiro = 0,
    decimal ValorCartaoCredito = 0);

public sealed record ItemRascunho(int ProdutoId, string ProdutoNome, decimal Preco);

public sealed record CompraRascunho(
    int LocalId,
    string LocalNome,
    DateTime Data,
    decimal ValorBeneficio,
    TipoCartaoBeneficio? TipoBeneficio,
    IReadOnlyList<ItemRascunho> Itens,
    decimal ValorDinheiro = 0,
    decimal ValorCartaoCredito = 0);

public readonly record struct PagamentoSaldo(decimal Dinheiro, decimal Cartao);

public sealed class PrecoNoLocal
{
    public int LocalId { get; init; }
    public string LocalNome { get; init; } = string.Empty;
    public decimal? Preco { get; init; }
    public bool MaisBarato { get; set; }
}

public sealed class LinhaComparativoProduto
{
    public int ProdutoId { get; init; }
    public string Nome { get; init; } = string.Empty;
    public IReadOnlyList<PrecoNoLocal> Precos { get; init; } = Array.Empty<PrecoNoLocal>();
    public decimal? EconomiaReais { get; init; }
    public decimal? EconomiaPercentual { get; init; }
}

public sealed class TotalLocalCesta
{
    public int LocalId { get; init; }
    public string Nome { get; init; } = string.Empty;
    public decimal? TotalComparavel { get; init; }
    public bool MaisBarato { get; init; }
    public decimal EconomiaReais { get; init; }
    public decimal EconomiaPercentual { get; init; }
}

public sealed class MetricaBeneficio
{
    public int LocalId { get; init; }
    public string LocalNome { get; init; } = string.Empty;
    public TipoCartaoBeneficio Tipo { get; init; }
    public decimal ValorBeneficio { get; init; }
    public decimal TotalCesta { get; init; }
    public decimal CustoDeBolso { get; init; }
    public decimal AproveitamentoPercentual { get; init; }
    public decimal Rendimento { get; init; }
    public bool RendeMais { get; init; }
}

public sealed class EvolucaoCesta
{
    public bool TemMesAnterior { get; init; }
    public decimal TotalMes { get; init; }
    public decimal TotalMesAnterior { get; init; }
    public decimal DiferencaReais { get; init; }
    public decimal DiferencaPercentual { get; init; }
    public string? Mensagem { get; init; }
}

public sealed class ComparativoCesta
{
    public bool Disponivel { get; init; }
    public string Mensagem { get; init; } = string.Empty;
    public IReadOnlyList<LinhaComparativoProduto> Produtos { get; init; } = Array.Empty<LinhaComparativoProduto>();
    public IReadOnlyList<TotalLocalCesta> Locais { get; init; } = Array.Empty<TotalLocalCesta>();
    public IReadOnlyList<MetricaBeneficio> Beneficios { get; init; } = Array.Empty<MetricaBeneficio>();
    public EvolucaoCesta Evolucao { get; init; } = new();
}

public sealed class ItemPrecoCategoria
{
    public string Nome { get; init; } = string.Empty;
    public int CategoriaId { get; init; }
    public decimal Preco { get; init; }
}

public sealed class ParcelaRateio
{
    public string Nome { get; set; } = string.Empty;
    public int CategoriaId { get; set; }
    public decimal Valor { get; set; }
}

public sealed class DespesaMontada
{
    public bool Gerar { get; init; }
    public decimal Valor { get; init; }
    public string Descricao { get; init; } = string.Empty;
    public string Observacao { get; init; } = string.Empty;
    public IReadOnlyList<ParcelaRateio> Itens { get; init; } = Array.Empty<ParcelaRateio>();
    public decimal Dinheiro { get; init; }
    public decimal Cartao { get; init; }
}

public sealed record FaturaCreditoCesta(
    int CartaoCreditoId,
    int? FaturaTransacaoId,
    decimal Valor,
    string DescricaoItem,
    int CategoriaCestaId,
    DateTime DataCompra,
    int MembroFamiliaId);
