namespace OrcaFacil.Core.Armazenamento.Partida;

/// <summary>Caminho do <c>orcafacil.db</c> desta execução. Não é o espelho e não é um backup.</summary>
public sealed class CaminhoBancoVivo
{
    public CaminhoBancoVivo(string caminho)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminho);
        Valor = caminho;
    }

    public string Valor { get; }
}
