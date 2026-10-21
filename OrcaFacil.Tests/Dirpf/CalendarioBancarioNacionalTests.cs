using OrcaFacil.Core.Dirpf;

namespace OrcaFacil.Tests.Dirpf;

public class CalendarioBancarioNacionalTests
{
    [Fact]
    public void PascoasDe2024A2027_EVinteDeNovembroAPartirDe2024()
    {
        // Segunda e terça de Carnaval, Sexta-feira Santa e Corpus Christi.
        (int Ano, int Mes, int Dia)[] feriados =
        [
            (2024, 2, 12), (2024, 2, 13), (2024, 3, 29), (2024, 5, 30),
            (2025, 3, 3), (2025, 3, 4), (2025, 4, 18), (2025, 6, 19),
            (2026, 2, 16), (2026, 2, 17), (2026, 4, 3), (2026, 6, 4),
            (2027, 2, 8), (2027, 2, 9), (2027, 3, 26), (2027, 5, 27)
        ];

        foreach (var (ano, mes, dia) in feriados)
            Assert.False(CalendarioBancarioNacional.EhDiaUtil(new DateTime(ano, mes, dia)));

        Assert.True(CalendarioBancarioNacional.EhDiaUtil(new DateTime(2024, 2, 14)));
        Assert.True(CalendarioBancarioNacional.EhDiaUtil(new DateTime(2024, 3, 28)));
        Assert.True(CalendarioBancarioNacional.EhDiaUtil(new DateTime(2024, 5, 31)));
        Assert.True(CalendarioBancarioNacional.EhDiaUtil(new DateTime(2025, 3, 5)));
        Assert.True(CalendarioBancarioNacional.EhDiaUtil(new DateTime(2025, 4, 17)));
        Assert.True(CalendarioBancarioNacional.EhDiaUtil(new DateTime(2025, 6, 18)));
        Assert.True(CalendarioBancarioNacional.EhDiaUtil(new DateTime(2026, 2, 18)));
        Assert.True(CalendarioBancarioNacional.EhDiaUtil(new DateTime(2026, 4, 2)));
        Assert.True(CalendarioBancarioNacional.EhDiaUtil(new DateTime(2026, 6, 5)));
        Assert.True(CalendarioBancarioNacional.EhDiaUtil(new DateTime(2027, 2, 10)));
        Assert.True(CalendarioBancarioNacional.EhDiaUtil(new DateTime(2027, 3, 25)));
        Assert.True(CalendarioBancarioNacional.EhDiaUtil(new DateTime(2027, 5, 26)));

        Assert.True(CalendarioBancarioNacional.EhDiaUtil(new DateTime(2023, 11, 20)));
        Assert.False(CalendarioBancarioNacional.EhDiaUtil(new DateTime(2024, 11, 20)));
        Assert.False(CalendarioBancarioNacional.EhDiaUtil(new DateTime(2025, 11, 20)));
        Assert.False(CalendarioBancarioNacional.EhDiaUtil(new DateTime(2026, 11, 20)));
        Assert.True(CalendarioBancarioNacional.EhDiaUtil(new DateTime(2024, 11, 19)));
        Assert.True(CalendarioBancarioNacional.EhDiaUtil(new DateTime(2024, 11, 21)));
    }
}
