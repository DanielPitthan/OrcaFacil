namespace OrcaFacil.Core.Llm.Avaliacao;

/// <summary>
/// Critério de engenharia da leitura. Não é regra de negócio do usuário e não tem tabela.
/// Total e data são métricas separadas: cada uma precisa atingir o próprio mínimo.
/// </summary>
public sealed record MetasLeituraComprovante(decimal TotalMinimo, decimal DataMinima, decimal PrecoItemMinimo)
{
    public static MetasLeituraComprovante Padrao { get; } = new(95m, 95m, 90m);

    public override string ToString() => "MetasLeituraComprovante";
}
