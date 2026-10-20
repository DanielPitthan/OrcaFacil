using System.ComponentModel.DataAnnotations;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Dtos;

public class OperacaoAtivoFormModel
{
    public int? Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecione um ativo")]
    public int AtivoId { get; set; }

    public TipoOperacao Tipo { get; set; } = TipoOperacao.Compra;

    public DateTime Data { get; set; } = DateTime.Today;

    [Range(0.00000001, 1_000_000_000, ErrorMessage = "A quantidade deve ser maior que zero")]
    public decimal Quantidade { get; set; }

    [Range(0.00000001, 1_000_000_000, ErrorMessage = "O preço deve ser maior que zero")]
    public decimal PrecoUnitario { get; set; }

    [Range(0, 1_000_000_000, ErrorMessage = "Os custos não podem ser negativos")]
    public decimal Custos { get; set; }

    [MaxLength(40, ErrorMessage = "A corretora deve ter no máximo 40 caracteres")]
    public string? Corretora { get; set; }

    public OrigemOperacaoAtivo Origem { get; set; } = OrigemOperacaoAtivo.Manual;

    /// <summary>Na edição o serviço preserva o hash já gravado. Manual fica nulo.</summary>
    [MaxLength(64)]
    public string? HashImportacao { get; set; }
}
