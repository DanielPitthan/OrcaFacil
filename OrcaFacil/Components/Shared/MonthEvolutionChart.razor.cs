namespace OrcaFacil.Components.Shared;

/// <summary>
/// Evolução acumulada do mês com MudChart (Line) do MudBlazor 9.9.0.
/// A área do saldo usa <c>SeriesDisplayOverride.LineDisplayType.Area</c> com
/// <c>FillOpacity</c> 0,16 no realizado e 0,07 no previsto.
/// O <c>Line.razor</c> dessa versão não emite <c>stroke-dasharray</c>; o tracejado
/// do previsto é aplicado por CSS em <c>path.mud-chart-line</c>. A série também não
/// aceita lacuna (NaN quebra o mínimo do eixo), então realizado e previsto ocupam
/// todos os dias e um <c>clip-path</c> mostra cada um só no seu trecho, os dois
/// incluindo o ponto de hoje. Não há API de anotação vertical: o marcador "Hoje"
/// é um overlay alinhado à margem padrão do eixo. Blazor-ApexCharts não entrou
/// porque área, tracejado e marcador ficam resolvidos com o MudChart instalado.
/// </summary>
public partial class MonthEvolutionChart;
