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
            TextSecondary = "#5A6B78"
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
            Primary = "#B2CFE6",
            Secondary = "#A2B94F",
            Tertiary = "#659ECD",
            Success = "#6DA538",
            Warning = "#EF7F5D",
            Error = "#E07171",
            Background = "#1A2029",
            Surface = "#232B36",
            AppbarBackground = "#12161C",
            AppbarText = "#EDEFF2",
            DrawerBackground = "#1A2029",
            DrawerText = "#EDEFF2",
            TextPrimary = "#EDEFF2",
            TextSecondary = "#B7C1CB"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = new[] { "Roboto", "Segoe UI", "sans-serif" } }
        }
    };
}
