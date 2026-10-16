namespace OrcaFacil.Components.Shared;

/// <summary>
/// Barras de receita e despesa com MudChart (Bar) do MudBlazor 9.9.0.
/// A primeira série é Receitas e usa <c>var(--of-success)</c>; a segunda é Despesas
/// e usa <c>var(--of-primary)</c>. A paleta padrão do gráfico invertia essas cores.
/// O eixo Y abreviado e a omissão dos meses vazios vêm do <c>PainelInicio</c>.
/// O mês atual, quando é o último com dados, ganha peso no último rótulo do eixo
/// porque o MudChart só aceita texto simples nos rótulos.
/// </summary>
public partial class MonthlyComparisonChart;
