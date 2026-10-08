using System.ComponentModel.DataAnnotations;

namespace OrcaFacil.Core.Entities;

/// <summary>Trava de um mês de lançamentos. Enquanto <see cref="Fechado"/> for true, nenhum lançamento do período pode ser alterado.</summary>
public class FechamentoPeriodo
{
    public int Id { get; set; }

    [Range(1, 12)]
    public int Mes { get; set; }

    [Range(2000, 2100)]
    public int Ano { get; set; }

    public bool Fechado { get; set; }

    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
}
