using System.ComponentModel.DataAnnotations;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Dtos;

public class AtivoFormModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Informe o ticker")]
    public string Ticker { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o nome"), MaxLength(80)]
    public string Nome { get; set; } = string.Empty;

    public TipoAtivo Tipo { get; set; } = TipoAtivo.Acao;
}
