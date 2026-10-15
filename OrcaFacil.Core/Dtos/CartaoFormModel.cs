using System.ComponentModel.DataAnnotations;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Dtos;

public class CartaoFormModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Informe o nome do cartão")]
    [MaxLength(80)]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a bandeira")]
    [MaxLength(40)]
    public string Bandeira { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe os 4 últimos números")]
    [StringLength(4, MinimumLength = 4, ErrorMessage = "Informe exatamente 4 números")]
    public string UltimosDigitos { get; set; } = string.Empty;

    public TipoCartao Tipo { get; set; } = TipoCartao.Credito;

    public TipoCartaoBeneficio? TipoBeneficio { get; set; } = TipoCartaoBeneficio.ValeAlimentacao;

    public bool Ativo { get; set; } = true;
}

public class RecargaCartaoFormModel
{
    public int CartaoId { get; set; }

    public DateTime Data { get; set; } = DateTime.Today;

    public decimal Valor { get; set; }
}
