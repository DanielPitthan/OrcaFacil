namespace OrcaFacil.Core.Categorias;

/// <summary>
/// Opções do seletor de cor no cadastro de categorias.
/// O valor gravado em <c>Categoria.CorHex</c> é o próprio hexadecimal.
/// A ordem dos itens já existentes permanece, para não alterar categorias salvas.
/// </summary>
public static class CoresCategoria
{
    public static IReadOnlyList<string> Opcoes { get; } =
    [
        "#6DA538", "#D64242", "#659ECD", "#A2B94F", "#EF7F5D",
        "#c7a98f", "#a97ca0", "#8fa9c7",
        // Verde-água, âmbar e violeta.
        "#21867A", "#A8740A", "#9751CD"
    ];
}
