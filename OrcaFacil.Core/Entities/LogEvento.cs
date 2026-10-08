using System.ComponentModel.DataAnnotations;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Entities;

/// <summary>Registro simples de ações relevantes do usuário (fechamento, reabertura, limpeza de dados).</summary>
public class LogEvento
{
    public int Id { get; set; }

    public TipoLogEvento Tipo { get; set; }

    [Required, MaxLength(300)]
    public string Mensagem { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; } = DateTime.Now;
}
