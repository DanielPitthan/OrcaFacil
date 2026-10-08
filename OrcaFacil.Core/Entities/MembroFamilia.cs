using System.ComponentModel.DataAnnotations;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Entities;

public class MembroFamilia
{
    public int Id { get; set; }

    [Required, MaxLength(60)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(40)]
    public string? Apelido { get; set; }

    [MaxLength(260)]
    public string? FotoPath { get; set; }

    [MaxLength(4)]
    public string Avatar { get; set; } = "👤";

    public PerfilRole Role { get; set; } = PerfilRole.Admin;

    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<Transacao> Transacoes { get; set; } = new List<Transacao>();

    public ICollection<Meta> Metas { get; set; } = new List<Meta>();
}
