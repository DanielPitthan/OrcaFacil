using MudColor = MudBlazor.Color;

namespace OrcaFacil.Services;

/// <summary>
/// Regra única de cor para o progresso de orçamento por categoria, reutilizada no Dashboard
/// e na tela de Orçamento Mensal. A paleta do app não tem um "vermelho"/"amarelo" nativo, então
/// as duas faixas mais críticas (90-99% e 100%+) usam as cores semânticas padrão do MudBlazor
/// (Warning/Error) para garantir clareza de acessibilidade nesses estados críticos.
/// </summary>
public static class BudgetColorRule
{
    public const string CorSaudavel = "#6DA538";   // Sage Green
    public const string CorAtencao = "#659ECD";    // Blue Grey
    public const string CorPertoDoLimite = "#EF7F5D"; // Burnt Peach (MudBlazor Warning)
    public const string CorEstourado = "#D64242";  // Scarlet Rush (MudBlazor Error)

    public static string ToHex(decimal percentual) => percentual switch
    {
        < 70 => CorSaudavel,
        < 90 => CorAtencao,
        < 100 => CorPertoDoLimite,
        _ => CorEstourado
    };

    public static MudColor ToMudColor(decimal percentual) => percentual switch
    {
        < 70 => MudColor.Success,
        < 90 => MudColor.Primary,
        < 100 => MudColor.Warning,
        _ => MudColor.Error
    };
}
