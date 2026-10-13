using System.ComponentModel.DataAnnotations;

namespace OrcaFacil.Core.Entities;

public class CompraCestaItem
{
    public int Id { get; set; }

    public int CompraCestaId { get; set; }
    public CompraCesta? CompraCesta { get; set; }

    public int ProdutoCestaId { get; set; }
    public ProdutoCesta? Produto { get; set; }

    [Range(0.01, 1_000_000_000)]
    public decimal Preco { get; set; }

    public int Ordem { get; set; }
}
