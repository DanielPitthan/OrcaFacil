namespace OrcaFacil.Core.Dirpf;

/// <summary>
/// Dias úteis do calendário bancário nacional, calculados aqui, sem tabela externa.
/// Feriados fixos: Lei nº 662/1949, Lei nº 6.802/1980 (12 de outubro) e,
/// a partir de 2024, Lei nº 14.759/2023 (20 de novembro).
/// Carnaval (segunda e terça), Sexta-feira Santa e Corpus Christi saem da Páscoa.
/// O DARF vence no último dia útil do mês seguinte ao ganho
/// (Instrução Normativa RFB nº 1.585/2015, art. 56, § 5º).
/// </summary>
public static class CalendarioBancarioNacional
{
    private static readonly (int Mes, int Dia)[] Fixos =
    [
        (1, 1),
        (4, 21),
        (5, 1),
        (9, 7),
        (10, 12),
        (11, 2),
        (11, 15),
        (12, 25)
    ];

    public static bool EhDiaUtil(DateTime data)
    {
        var dia = data.Date;
        if (dia.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return false;

        if (Fixos.Contains((dia.Month, dia.Day)))
            return false;

        if (dia.Year >= 2024 && dia.Month == 11 && dia.Day == 20)
            return false;

        var pascoa = Pascoa(dia.Year);
        return dia != pascoa.AddDays(-48)
            && dia != pascoa.AddDays(-47)
            && dia != pascoa.AddDays(-2)
            && dia != pascoa.AddDays(60);
    }

    public static DateTime UltimoDiaUtil(int ano, int mes)
    {
        var dia = new DateTime(ano, mes, DateTime.DaysInMonth(ano, mes));
        while (!EhDiaUtil(dia))
            dia = dia.AddDays(-1);

        return dia;
    }

    /// <summary>Computus gregoriano (Meeus). Domingo de Páscoa.</summary>
    private static DateTime Pascoa(int ano)
    {
        var a = ano % 19;
        var b = ano / 100;
        var c = ano % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var mes = (h + l - 7 * m + 114) / 31;
        var dia = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateTime(ano, mes, dia);
    }
}
