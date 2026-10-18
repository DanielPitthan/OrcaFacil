namespace OrcaFacil.Core.Armazenamento.Espelho.Pasta;

/// <summary>
/// Token já cancelado não ocupa o pedido do seletor e não abre a activity.
/// </summary>
public static class AberturaSeletorPasta
{
    public static Task<T> ExecutarSeNaoCanceladoAsync<T>(
        CancellationToken cancellationToken,
        Func<Task<T>> abrir,
        T recusado)
    {
        ArgumentNullException.ThrowIfNull(abrir);
        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(recusado);

        return abrir();
    }
}
