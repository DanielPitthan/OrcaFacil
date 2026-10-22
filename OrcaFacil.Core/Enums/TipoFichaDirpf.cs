namespace OrcaFacil.Core.Enums;

/// <summary>Ficha da DIRPF que o código com vigência alimenta.</summary>
public enum TipoFichaDirpf
{
    Bem = 0,
    RendimentoIsento = 1,

    /// <summary>
    /// Rendimentos sujeitos à tributação exclusiva/definitiva.
    /// JCP e o líquido da renda fixa entram aqui. O número do código fica na tabela.
    /// </summary>
    RendimentoExclusivo = 2
}
