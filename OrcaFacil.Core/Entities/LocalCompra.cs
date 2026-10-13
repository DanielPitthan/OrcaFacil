using System.ComponentModel.DataAnnotations;

namespace OrcaFacil.Core.Entities;

/// <summary>Mercado, atacado ou loja onde a cesta foi comprada.</summary>
public class LocalCompra
{
    public int Id { get; set; }

    [Required, MaxLength(80)]
    public string Nome { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<CompraCesta> Compras { get; set; } = new List<CompraCesta>();
}
