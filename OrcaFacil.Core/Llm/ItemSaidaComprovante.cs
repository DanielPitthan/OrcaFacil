namespace OrcaFacil.Core.Llm;

/// <summary>
/// Contrato de saída de um item do comprovante, para a tela de confirmação.
/// <see cref="Pendente"/> só destaca a linha. <see cref="ProdutoId"/> não grava sozinho.
/// </summary>
public sealed record ItemSaidaComprovante
{
    public string DescricaoLida { get; init; } = string.Empty;

    public int? ProdutoId { get; init; }

    public decimal Confianca { get; init; }

    public bool Pendente { get; init; }

    /// <summary>A pessoa confirmou este item. Começa falso.</summary>
    public bool Confirmado { get; init; }

    public bool NaoIdentificado => ProdutoId is null;

    public bool ExigeConfirmacao => ProdutoId is not null;

    public bool PodeSalvar => !ExigeConfirmacao || Confirmado;

    public ItemSaidaComprovante Confirmar() => this with { Confirmado = true };

    public override string ToString() => "ItemSaidaComprovante";
}
