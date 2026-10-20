using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Ativos;

/// <summary>Operação já pronta para o cálculo, sem dependência do banco.</summary>
public sealed class MovimentoPosicao
{
    public int Id { get; init; }
    public DateTime Data { get; init; }
    public TipoOperacao Tipo { get; init; }
    public decimal Quantidade { get; init; }
    public decimal PrecoUnitario { get; init; }

    /// <summary>Valor total da operação. O resultado da venda usa este campo.</summary>
    public decimal ValorOperacao { get; init; }

    public decimal Custos { get; init; }
}
