using System.ComponentModel.DataAnnotations;

namespace OrcaFacil.Core.Entities;

/// <summary>Recarga de saldo de um cartão de benefício.</summary>
public class RecargaCartao
{
    public int Id { get; set; }

    public int CartaoId { get; set; }
    public Cartao? Cartao { get; set; }

    [Required]
    public DateTime Data { get; set; }

    [Range(0.01, 1_000_000_000)]
    public decimal Valor { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
