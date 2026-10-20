using System.ComponentModel.DataAnnotations;

namespace OrcaFacil.Core.Entities;

/// <summary>
/// Parâmetro fiscal com vigência. O cálculo lê a linha válida na data da operação.
/// O valor não fica no código da calculadora.
/// </summary>
public class RegraFiscal
{
    public int Id { get; set; }

    [Required, MaxLength(40)]
    public string Codigo { get; set; } = string.Empty;

    public decimal Valor { get; set; }

    public DateTime VigenciaInicio { get; set; }

    public DateTime? VigenciaFim { get; set; }

    /// <summary>Linha semeada pela migration. Igual à semente, não conta como dado do usuário.</summary>
    public bool EhSistema { get; set; }
}
