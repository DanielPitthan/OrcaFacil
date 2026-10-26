using MudBlazor;

namespace OrcaFacil.Theme;

/// <summary>Mapeamento da paleta do app (Sage Green, Scarlet Rush, Blue Grey, Lime Moss, Burnt Peach) para MudBlazor.</summary>
public static class OrcaFacilThemes
{
    public static MudTheme Light { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#659ECD",
            Secondary = "#A2B94F",
            Tertiary = "#B2CFE6",
            Success = "#6DA538",
            Warning = "#EF7F5D",
            Error = "#D64242",
            Background = "#EFF3F7",
            Surface = "#ffffff",
            AppbarBackground = "#659ECD",
            AppbarText = "#ffffff",
            DrawerBackground = "#EFF3F7",
            DrawerText = "#426785",
            TextPrimary = "#1E2A33",
            TextSecondary = "#5A6B78",
            // Preto a 38% do MudBlazor fica em 2,64:1 sobre #EFF3F7. #636C78 fica em 4,77:1.
            TextDisabled = "#636C78"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = new[] { "Roboto", "Segoe UI", "sans-serif" } }
        }
    };

    public static MudTheme Dark { get; } = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#6EA8FE",
            PrimaryContrastText = "#0B1220",
            Secondary = "#A2B94F",
            Tertiary = "#659ECD",
            Success = "#4CC38A",
            SuccessContrastText = "#0B1220",
            SuccessLighten = "#6FD9A4",
            Warning = "#F2B84B",
            WarningContrastText = "#0B1220",
            Error = "#E07171",
            Dark = "#12151B",
            Background = "#12151B",
            BackgroundGray = "#151920",
            Surface = "#1B1F27",
            AppbarBackground = "#12151B",
            AppbarText = "#E8EAF0",
            DrawerBackground = "#1B1F27",
            DrawerText = "#E8EAF0",
            DrawerIcon = "#C9D1DD",
            TextPrimary = "#E8EAF0",
            TextSecondary = "#C9D1DD",
            TextDisabled = "#9AA3B2",
            ActionDefault = "#C9D1DD",
            LinesDefault = "#2A303C",
            LinesInputs = "#3A4150",
            TableLines = "#2A303C",
            Divider = "#2A303C",
            DividerLight = "#2A303C",
            GrayDefault = "#9AA3B2",
            GrayLight = "#3A4150",
            GrayLighter = "#262C38"
            // Saldo #B69CFF não cabe em nenhum slot da PaletteDark (Tertiary já é a marca).
            // O token vive em --of-saldo, no CSS do tema.
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = new[] { "Roboto", "Segoe UI", "sans-serif" } }
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "12px"
        }
    };
}
