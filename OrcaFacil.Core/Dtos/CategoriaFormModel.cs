using System.ComponentModel.DataAnnotations;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Dtos;

public class CategoriaFormModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Informe um nome")]
    [MaxLength(40, ErrorMessage = "Máximo de 40 caracteres")]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escolha um ícone")]
    public string Icone { get; set; } = "🏷️";

    [Required(ErrorMessage = "Escolha uma cor")]
    public string CorHex { get; set; } = "#7f7caf";

    public TipoTransacao TipoPadrao { get; set; } = TipoTransacao.Despesa;
}
