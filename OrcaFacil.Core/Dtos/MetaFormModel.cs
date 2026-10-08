using System.ComponentModel.DataAnnotations;

namespace OrcaFacil.Core.Dtos;

public class MetaFormModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Informe um nome")]
    [MaxLength(80, ErrorMessage = "Máximo de 80 caracteres")]
    public string Nome { get; set; } = string.Empty;

    [Range(0.01, 1_000_000_000, ErrorMessage = "Informe um valor alvo maior que zero")]
    public decimal ValorAlvo { get; set; }

    public decimal ValorAtual { get; set; }

    public DateTime? Prazo { get; set; }

    [Required(ErrorMessage = "Escolha um ícone")]
    public string Icone { get; set; } = "🎯";
}

public class ContribuicaoMetaModel
{
    [Range(0.01, 1_000_000_000, ErrorMessage = "Informe um valor maior que zero")]
    public decimal Valor { get; set; }

    public bool RegistrarComoLancamento { get; set; }
}
