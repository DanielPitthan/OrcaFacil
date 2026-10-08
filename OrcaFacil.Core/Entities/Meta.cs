using System.ComponentModel.DataAnnotations;

namespace OrcaFacil.Core.Entities;

public class Meta
{
    public int Id { get; set; }

    [Required, MaxLength(80)]
    public string Nome { get; set; } = string.Empty;

    [Range(0.01, 1_000_000_000)]
    public decimal ValorAlvo { get; set; }

    public decimal ValorAtual { get; set; }

    public DateTime? Prazo { get; set; }

    [MaxLength(4)]
    public string Icone { get; set; } = "🎯";

    public bool Concluida { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public int MembroFamiliaId { get; set; }
    public MembroFamilia? MembroFamilia { get; set; }

    public decimal PercentualConcluido => ValorAlvo <= 0 ? 0 : Math.Min(100m, Math.Round(ValorAtual / ValorAlvo * 100m, 1));
}
