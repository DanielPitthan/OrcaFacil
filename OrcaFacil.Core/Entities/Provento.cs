using System.ComponentModel.DataAnnotations;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Entities;

/// <summary>
/// Rendimento ou JCP recebido. O valor líquido vira receita do mês do pagamento.
/// A gravação passa só pelo serviço de proventos.
/// </summary>
public class Provento
{
    public int Id { get; set; }

    public int AtivoId { get; set; }
    public AtivoFinanceiro? Ativo { get; set; }

    public int MembroFamiliaId { get; set; }
    public MembroFamilia? MembroFamilia { get; set; }

    public TipoProvento Tipo { get; set; }

    public DateTime DataPagamento { get; set; }

    public decimal ValorLiquido { get; set; }

    public OrigemProvento Origem { get; set; } = OrigemProvento.Manual;

    /// <summary>
    /// Identidade da linha importada. Provento manual fica sem hash;
    /// o índice único ignora nulos, então manual não colide com importado.
    /// </summary>
    [MaxLength(64)]
    public string? HashImportacao { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? AtualizadoEm { get; set; }
}
