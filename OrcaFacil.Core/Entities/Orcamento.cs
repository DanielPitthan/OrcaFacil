using System.ComponentModel.DataAnnotations;

namespace OrcaFacil.Core.Entities;

public class Orcamento
{
    public int Id { get; set; }

    public int CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }

    [Range(0.01, 1_000_000_000)]
    public decimal ValorLimite { get; set; }

    /// <summary>Meta opcional de redução percentual em relação ao gasto do mês anterior.</summary>
    [Range(0, 100)]
    public decimal? MetaReducaoPercentual { get; set; }

    [Range(1, 12)]
    public int Mes { get; set; }

    [Range(2000, 2100)]
    public int Ano { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
