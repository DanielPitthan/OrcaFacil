using System.ComponentModel.DataAnnotations;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Dtos;

public class OperacaoRendaFixaFormModel
{
    public int? Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecione um título")]
    public int TituloRendaFixaId { get; set; }

    public DateTime Data { get; set; } = DateTime.Today;

    public TipoOperacaoRendaFixa Tipo { get; set; } = TipoOperacaoRendaFixa.Aplicacao;

    [Range(typeof(decimal), "0.00000001", "1000000000", ErrorMessage = "A quantidade deve ser maior que zero")]
    public decimal Quantidade { get; set; }

    /// <summary>Só informativo. O cálculo usa <see cref="Valor"/>.</summary>
    [Range(0, 1_000_000_000, ErrorMessage = "O preço não pode ser negativo")]
    public decimal PrecoUnitario { get; set; }

    [Range(typeof(decimal), "0.01", "1000000000", ErrorMessage = "O valor deve ser maior que zero")]
    public decimal Valor { get; set; }

    [Range(0, 1_000_000_000, ErrorMessage = "Os custos não podem ser negativos")]
    public decimal Custos { get; set; }

    [Range(0, 1_000_000_000, ErrorMessage = "O IR retido não pode ser negativo")]
    public decimal IrRetido { get; set; }

    public OrigemOperacaoRendaFixa Origem { get; set; } = OrigemOperacaoRendaFixa.Manual;

    /// <summary>Na edição o serviço preserva o hash já gravado.</summary>
    [MaxLength(64)]
    public string? HashImportacao { get; set; }
}
