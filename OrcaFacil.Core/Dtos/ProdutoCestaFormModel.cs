using System.ComponentModel.DataAnnotations;

namespace OrcaFacil.Core.Dtos;

public class ProdutoCestaFormModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Informe o nome"), MaxLength(80)]
    public string Nome { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Selecione a categoria")]
    public int CategoriaId { get; set; }

    public bool Ativo { get; set; } = true;
}
