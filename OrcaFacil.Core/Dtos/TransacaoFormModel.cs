using System.ComponentModel.DataAnnotations;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Dtos;

public class TransacaoFormModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Informe uma descrição")]
    [MaxLength(120, ErrorMessage = "Máximo de 120 caracteres")]
    public string Descricao { get; set; } = string.Empty;

    [Range(0.01, 1_000_000_000, ErrorMessage = "Informe um valor maior que zero")]
    public decimal Valor { get; set; }

    [Required(ErrorMessage = "Informe a data")]
    public DateTime Data { get; set; } = DateTime.Today;

    public TipoTransacao Tipo { get; set; } = TipoTransacao.Despesa;

    public TipoRecorrencia Recorrencia { get; set; } = TipoRecorrencia.Unica;

    public bool Pago { get; set; } = false;

    [MaxLength(500, ErrorMessage = "Máximo de 500 caracteres")]
    public string? Observacao { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecione uma categoria")]
    public int CategoriaId { get; set; }
}
