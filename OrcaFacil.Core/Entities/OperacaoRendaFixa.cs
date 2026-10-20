using System.ComponentModel.DataAnnotations;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Entities;

/// <summary>
/// Aplicação ou resgate. O cálculo usa <see cref="Valor"/>, não quantidade × preço.
/// Preço unitário é só informativo. Custos e IR retido nascem zerados.
/// </summary>
public class OperacaoRendaFixa
{
    public int Id { get; set; }

    public int TituloRendaFixaId { get; set; }
    public TituloRendaFixa? Titulo { get; set; }

    public DateTime Data { get; set; }

    public TipoOperacaoRendaFixa Tipo { get; set; }

    public decimal Quantidade { get; set; }

    public decimal PrecoUnitario { get; set; }

    public decimal Valor { get; set; }

    public decimal Custos { get; set; }

    public decimal IrRetido { get; set; }

    public OrigemOperacaoRendaFixa Origem { get; set; } = OrigemOperacaoRendaFixa.Manual;

    /// <summary>
    /// Identidade da linha importada. Operação manual fica sem hash;
    /// o índice único ignora nulos, então manual não colide com importada.
    /// </summary>
    [MaxLength(64)]
    public string? HashImportacao { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? AtualizadoEm { get; set; }
}
