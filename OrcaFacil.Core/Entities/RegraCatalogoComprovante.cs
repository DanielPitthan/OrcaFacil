namespace OrcaFacil.Core.Entities;

/// <summary>
/// Regra de negócio da leitura do comprovante. O teto do catálogo enviado ao modelo mora nesta tabela.
/// Sem linha, ou com valor menor que 1, vale <see cref="LimitePadrao"/>.
/// </summary>
public class RegraCatalogoComprovante
{
    public const int LimitePadrao = 300;

    public int Id { get; set; }

    public int LimiteProdutos { get; set; } = LimitePadrao;
}
