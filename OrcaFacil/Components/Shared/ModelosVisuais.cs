namespace OrcaFacil.Components.Shared;

public enum TomResumo
{
    Neutro,
    Primario,
    Sucesso,
    Alerta,
    Erro
}

public sealed record ChipSomenteLeitura(string Texto, string? Icone = null);

public sealed record LegendaResumo(string Rotulo, string Valor, TomResumo Tom);

public sealed record OpcaoSegmento<T>(T Valor, string Texto) where T : notnull;

public sealed record OpcaoFiltro<T>(T Valor, string Texto) where T : notnull;

public sealed record OpcaoCategoriaFiltro(int Id, string Nome, string Icone);
