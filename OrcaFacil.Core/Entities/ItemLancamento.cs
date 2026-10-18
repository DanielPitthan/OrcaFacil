using System.ComponentModel.DataAnnotations;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Entities;

public class ItemLancamento
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Descricao { get; set; } = string.Empty;

    /// <summary>Texto livre para distinguir compras da mesma categoria na fatura. Ex.: McDonald's, Netflix.</summary>
    [MaxLength(120)]
    public string? Descritivo { get; set; }

    /// <summary>Magnitude sempre positiva. O sinal entra pela flag <see cref="EhEstorno"/>.</summary>
    [Range(0.01, 1_000_000_000)]
    public decimal Valor { get; set; }

    /// <summary>
    /// Quando verdadeiro, o item é crédito na fatura: a soma usa o valor com sinal negativo
    /// e a natureza fica avulsa. Não copia para o mês seguinte.
    /// </summary>
    public bool EhEstorno { get; set; }

    public NaturezaItemLancamento Natureza { get; set; } = NaturezaItemLancamento.Avulsa;

    public int? ParcelaAtual { get; set; }

    public int? TotalParcelas { get; set; }

    public int Ordem { get; set; }

    /// <summary>
    /// SHA-256 da linha de CSV que originou o item.
    /// Itens lançados na mão ficam sem hash; a reimportação reconhece os que já entraram.
    /// </summary>
    [MaxLength(64)]
    public string? HashImportacao { get; set; }

    public int TransacaoId { get; set; }
    public Transacao? Transacao { get; set; }

    public int CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }
}
