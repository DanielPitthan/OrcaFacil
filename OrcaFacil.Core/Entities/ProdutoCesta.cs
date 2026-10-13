using System.ComponentModel.DataAnnotations;

namespace OrcaFacil.Core.Entities;

/// <summary>Produto da cesta básica, classificado numa categoria de despesa.</summary>
public class ProdutoCesta
{
    public int Id { get; set; }

    [Required, MaxLength(80)]
    public string Nome { get; set; } = string.Empty;

    public int CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }

    /// <summary>Inativo some das novas compras, mas o histórico de preços permanece.</summary>
    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<CompraCestaItem> Itens { get; set; } = new List<CompraCestaItem>();
}
