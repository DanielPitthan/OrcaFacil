using System.ComponentModel.DataAnnotations;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Dtos;

public class ItemLancamentoFormModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Informe a descrição do item")]
    [MaxLength(120, ErrorMessage = "Máximo de 120 caracteres")]
    public string Descricao { get; set; } = string.Empty;

    [MaxLength(120, ErrorMessage = "O descritivo pode ter no máximo 120 caracteres")]
    public string? Descritivo { get; set; }

    [Range(0.01, 1_000_000_000, ErrorMessage = "Informe um valor maior que zero")]
    public decimal Valor { get; set; }

    public NaturezaItemLancamento Natureza { get; set; } = NaturezaItemLancamento.Avulsa;

    public int? ParcelaAtual { get; set; }

    public int? TotalParcelas { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecione a categoria do item")]
    public int CategoriaId { get; set; }
}
