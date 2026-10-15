using System.ComponentModel.DataAnnotations;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Entities;

/// <summary>Uma ida às compras da cesta básica, num local e numa data.</summary>
public class CompraCesta
{
    public int Id { get; set; }

    public int Mes { get; set; }

    public int Ano { get; set; }

    [Required]
    public DateTime Data { get; set; }

    public int LocalCompraId { get; set; }
    public LocalCompra? LocalCompra { get; set; }

    /// <summary>Valor do cartão de benefício usado nesta compra. Entra como desconto da despesa.</summary>
    public decimal ValorBeneficio { get; set; }

    public TipoCartaoBeneficio? TipoCartaoBeneficio { get; set; }

    public int? CartaoBeneficioId { get; set; }
    public Cartao? CartaoBeneficio { get; set; }

    /// <summary>Parte do saldo (total menos benefício) paga em dinheiro.</summary>
    public decimal ValorDinheiro { get; set; }

    /// <summary>Parte do saldo paga no cartão de crédito.</summary>
    public decimal ValorCartaoCredito { get; set; }

    public int? CartaoCreditoId { get; set; }
    public Cartao? CartaoCredito { get; set; }

    /// <summary>Item de fatura gerado nos lançamentos de Cartão de Crédito para esta compra.</summary>
    public int? ItemFaturaId { get; set; }
    public ItemLancamento? ItemFatura { get; set; }

    /// <summary>Despesa gerada nos lançamentos. Nula quando o benefício cobre a cesta inteira.</summary>
    public int? TransacaoId { get; set; }
    public Transacao? Transacao { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<CompraCestaItem> Itens { get; set; } = new List<CompraCestaItem>();
}
