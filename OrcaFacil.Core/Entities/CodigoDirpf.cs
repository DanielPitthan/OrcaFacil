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

    /// <summary>
    /// Vazio na linha do tipo. "Fiagro" guarda o código de bem 07/02
    /// sem criar outro tipo de ativo: no cálculo e na importação o Fiagro continua FII.
    /// </summary>
    [MaxLength(16)]
    public string Variante { get; set; } = string.Empty;

    /// <summary>Grupo de Bens e Direitos, com dois dígitos. Vazio na ficha de rendimentos.</summary>
    [MaxLength(2)]
    public string Grupo { get; set; } = string.Empty;

    /// <summary>Código do bem ou do rendimento, com dois dígitos.</summary>
    [Required, MaxLength(2)]
    public string Codigo { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Descricao { get; set; } = string.Empty;

    public DateTime VigenciaInicio { get; set; }

    public DateTime? VigenciaFim { get; set; }

    /// <summary>Linha semeada pela migration. Igual à semente, não conta como dado do usuário.</summary>
    public bool EhSistema { get; set; }
}
