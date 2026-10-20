using System.ComponentModel.DataAnnotations;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Entities;

/// <summary>
/// Código da Receita para um tipo de ativo, com vigência.
/// O grupo e o código do bem, e o código do rendimento isento, não ficam no cálculo.
/// </summary>
public class CodigoDirpf
{
    public int Id { get; set; }

    public TipoFichaDirpf Ficha { get; set; }

    public TipoAtivo TipoAtivo { get; set; }

    /// <summary>Grupo de Bens e Direitos, com dois dígitos. Vazio na ficha de rendimentos.</summary>
    [MaxLength(2)]
    public string Grupo { get; set; } = string.Empty;

    /// <summary>Código do bem ou do rendimento, com dois dígitos.</summary>
    [Required, MaxLength(2)]
    public string Codigo { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string Descricao { get; set; } = string.Empty;

    public DateTime VigenciaInicio { get; set; }

    public DateTime? VigenciaFim { get; set; }

    /// <summary>Linha semeada pela migration. Igual à semente, não conta como dado do usuário.</summary>
    public bool EhSistema { get; set; }
}
