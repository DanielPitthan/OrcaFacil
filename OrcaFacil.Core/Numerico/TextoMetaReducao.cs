namespace OrcaFacil.Core.Numerico;

/// <summary>
/// Meta de redução na tela: até uma casa em pt-BR, sem zero à direita (12,5, 0,5, 10).
/// Trunca como o campo. 12,59 fica 12,5, não 12,6.
/// </summary>
public static class TextoMetaReducao
{
    public static string Percentual(decimal valor)
    {
        return MascaraDireitaEsquerda.CanonicoFracao(valor, 1);
    }
}
