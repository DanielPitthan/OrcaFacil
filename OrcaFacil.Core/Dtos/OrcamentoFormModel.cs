using System.ComponentModel.DataAnnotations;

namespace OrcaFacil.Core.Dtos;

public class OrcamentoFormModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Selecione uma categoria")]
    public int CategoriaId { get; set; }

    [Range(0.01, 1_000_000_000, ErrorMessage = "Informe um limite maior que zero")]
    public decimal ValorLimite { get; set; }

    /// <summary>Meta opcional de redução (%) vs gasto do mês anterior (0–100).</summary>
    [Range(0, 100, ErrorMessage = "Informe um percentual entre 0 e 100")]
    public decimal? MetaReducaoPercentual { get; set; }

    [Range(1, 12)]
    public int Mes { get; set; }

    [Range(2000, 2100)]
    public int Ano { get; set; }
}
