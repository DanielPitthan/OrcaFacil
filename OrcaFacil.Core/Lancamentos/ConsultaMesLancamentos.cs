using System.Globalization;

namespace OrcaFacil.Core.Lancamentos;

/// <summary>Lê <c>?mes=yyyy-MM</c>. Fora desse formato, fica o mês de hoje.</summary>
public static class ConsultaMesLancamentos
{
    public static DateTime Resolver(string? mes, DateTime hoje)
    {
        if (TryLer(mes, out var inicio))
            return inicio;

        return new DateTime(hoje.Year, hoje.Month, 1);
    }

    /// <summary>
    /// Rota da lista no mês informado. Fora de <c>yyyy-MM</c>, fica o caminho sem <c>?mes=</c>.
    /// </summary>
    public static string Rota(string? mes, string caminho = "/lancamentos")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminho);
        return TryLer(mes, out var inicio)
            ? $"{caminho}?mes={inicio:yyyy-MM}"
            : caminho;
    }

    private static bool TryLer(string? mes, out DateTime inicio)
    {
        if (DateTime.TryParseExact(
                mes,
                "yyyy-MM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var lido))
        {
            inicio = new DateTime(lido.Year, lido.Month, 1);
            return true;
        }

        inicio = default;
        return false;
    }
}
