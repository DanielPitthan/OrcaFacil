using System.ComponentModel.DataAnnotations;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Entities;

public class Transacao
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Descricao { get; set; } = string.Empty;

    [Range(0.01, 1_000_000_000)]
    public decimal Valor { get; set; }

    [Required]
    public DateTime Data { get; set; }

    public TipoTransacao Tipo { get; set; }

    public TipoRecorrencia Recorrencia { get; set; } = TipoRecorrencia.Unica;

    /// <summary>Só é relevante para Despesas — Receitas são sempre consideradas "pagas".</summary>
    public bool Pago { get; set; } = true;

    [MaxLength(500)]
    public string? Observacao { get; set; }

    public int CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }

    public int MembroFamiliaId { get; set; }
    public MembroFamilia? MembroFamilia { get; set; }

    /// <summary>Aponta para o lançamento "modelo" quando esta é uma ocorrência gerada a partir de uma recorrência.</summary>
    public int? TransacaoOrigemId { get; set; }
    public Transacao? TransacaoOrigem { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? AtualizadoEm { get; set; }
}
