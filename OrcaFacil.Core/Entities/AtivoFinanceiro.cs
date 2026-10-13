using System.ComponentModel.DataAnnotations;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Entities;

/// <summary>Papel da B3 cadastrado uma vez e compartilhado por quem operar nele.</summary>
public class AtivoFinanceiro
{
    public int Id { get; set; }

    [Required, MaxLength(8)]
    public string Ticker { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string Nome { get; set; } = string.Empty;

    public TipoAtivo Tipo { get; set; }

    /// <summary>Inativo some das novas operações, mas o histórico permanece.</summary>
    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<OperacaoAtivo> Operacoes { get; set; } = new List<OperacaoAtivo>();
}
