using System.ComponentModel.DataAnnotations;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Entities;

/// <summary>Cartão de crédito ou de benefício cadastrado para lançamentos e compras da cesta.</summary>
public class Cartao
{
    public int Id { get; set; }

    [Required, MaxLength(80)]
    public string Nome { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string Bandeira { get; set; } = string.Empty;

    [Required, MaxLength(4)]
    public string UltimosDigitos { get; set; } = string.Empty;

    public TipoCartao Tipo { get; set; }

    public TipoCartaoBeneficio? TipoBeneficio { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<RecargaCartao> Recargas { get; set; } = new List<RecargaCartao>();

    public ICollection<Transacao> Transacoes { get; set; } = new List<Transacao>();

    public string Rotulo => string.IsNullOrWhiteSpace(UltimosDigitos)
        ? $"{Nome} ({Bandeira})"
        : $"{Nome} •••• {UltimosDigitos}";
}
