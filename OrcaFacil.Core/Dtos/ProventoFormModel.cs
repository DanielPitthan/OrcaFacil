using System.ComponentModel.DataAnnotations;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Dtos;

public class ProventoFormModel
{
    public int? Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecione um ativo")]
    public int AtivoId { get; set; }

    public TipoProvento Tipo { get; set; } = TipoProvento.Rendimento;

    public DateTime DataPagamento { get; set; } = DateTime.Today;

    [Range(0.01, 1_000_000_000, ErrorMessage = "O valor líquido deve ser maior que zero")]
    public decimal ValorLiquido { get; set; }

    public OrigemProvento Origem { get; set; } = OrigemProvento.Manual;

    /// <summary>Na edição o serviço preserva o hash já gravado. Manual fica nulo.</summary>
    [MaxLength(64)]
    public string? HashImportacao { get; set; }
}
