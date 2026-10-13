using System.ComponentModel.DataAnnotations;

namespace OrcaFacil.Core.Dtos;

public class LocalCompraFormModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Informe o nome"), MaxLength(80)]
    public string Nome { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;
}
