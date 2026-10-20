using System.ComponentModel.DataAnnotations;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Entities;

/// <summary>
/// CDB ou título do Tesouro cadastrado à parte dos papéis de quantidade inteira.
/// O par (Codigo, Corretora) identifica o título.
/// </summary>
public class TituloRendaFixa
{
    public int Id { get; set; }

    public TipoTituloRendaFixa Tipo { get; set; }

    /// <summary>Código B3 do CDB ou nome do Tesouro.</summary>
    [Required, MaxLength(80)]
    public string Codigo { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? Emissor { get; set; }

    public int? AnoVencimento { get; set; }

    /// <summary>Apelido da corretora, no máximo 40 caracteres.</summary>
    [MaxLength(40)]
    public string Corretora { get; set; } = string.Empty;

    /// <summary>Inativo some dos novos lançamentos, mas o histórico permanece.</summary>
    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<OperacaoRendaFixa> Operacoes { get; set; } = new List<OperacaoRendaFixa>();
}
