using System.Globalization;

namespace OrcaFacil.Core.Lancamentos;

/// <summary>Lê <c>?mes=yyyy-MM</c>. Fora desse formato, fica o mês de hoje.</summary>
public static class ConsultaMesLancamentos
{
    public static DateTime Resolver(string? mes, DateTime hoje)
    {
        if (DateTime.TryParseExact(
                mes,
                "yyyy-MM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var lido))
        {
            return new DateTime(lido.Year, lido.Month, 1);
        }

        return new DateTime(hoje.Year, hoje.Month, 1);
    }
}
