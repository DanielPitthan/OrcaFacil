namespace OrcaFacil.Services.Interfaces;

/// <summary>Fechamento (trava) e reabertura de meses de lançamentos.</summary>
public interface IFechamentoService
{
    Task<bool> EstaFechadoAsync(int mes, int ano);

    /// <summary>Fecha o mês se estiver aberto ou reabre se estiver fechado; registra log e retorna o novo estado.</summary>
    Task<bool> AlternarAsync(int mes, int ano);

    /// <summary>Lança <see cref="InvalidOperationException"/> se o mês da data estiver fechado.</summary>
    Task GarantirPeriodoAbertoAsync(DateTime data);
}
