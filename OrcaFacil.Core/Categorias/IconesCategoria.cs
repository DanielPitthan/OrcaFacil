namespace OrcaFacil.Core.Categorias;

/// <summary>
/// Opções do seletor de ícone no cadastro de categorias (o mesmo seletor das metas).
/// O valor gravado em <c>Categoria.Icone</c> é o próprio emoji, exibido como texto
/// nas demais telas. A ordem dos itens já existentes permanece, para não alterar
/// categorias salvas.
/// </summary>
public static class IconesCategoria
{
    public static IReadOnlyList<string> Opcoes { get; } =
    [
        "🏠", "🚗", "💡", "📱", "🛒", "💳", "🏥", "🎓", "🎉", "💰",
        "📈", "🧾", "🍔", "☕", "🚌", "✈️", "🎁", "🐾", "🧴", "📦",
        "🏷️", "⚡", "🎯", "🛟", "🧑‍🍼", "📚", "🏋️", "🎮", "🚿", "🌐",
        // Vestuário, combustível, banco, doações e assinaturas.
        "👕", "⛽", "🏦", "🤝", "📺"
    ];
}
