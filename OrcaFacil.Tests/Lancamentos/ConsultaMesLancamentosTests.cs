using System.Globalization;
using OrcaFacil.Core.Lancamentos;

namespace OrcaFacil.Tests.Lancamentos;

public class ConsultaMesLancamentosTests
{
    private static readonly DateTime Hoje = new(2026, 10, 20);

    [Fact]
    public void MesInvalido_FicaNoMesAtual()
    {
        Assert.Equal(new DateTime(2026, 10, 1), ConsultaMesLancamentos.Resolver(null, Hoje));
        Assert.Equal(new DateTime(2026, 10, 1), ConsultaMesLancamentos.Resolver("", Hoje));
        Assert.Equal(new DateTime(2026, 10, 1), ConsultaMesLancamentos.Resolver("2026-13", Hoje));
        Assert.Equal(new DateTime(2026, 10, 1), ConsultaMesLancamentos.Resolver("2026-3", Hoje));
        Assert.Equal(new DateTime(2026, 10, 1), ConsultaMesLancamentos.Resolver("marco", Hoje));
        Assert.Equal(new DateTime(2026, 10, 1), ConsultaMesLancamentos.Resolver("2026-03-01", Hoje));
        Assert.Equal(new DateTime(2026, 10, 1), ConsultaMesLancamentos.Resolver(" 2026-03", Hoje));
        Assert.Equal(new DateTime(2026, 10, 1), ConsultaMesLancamentos.Resolver("2026-03 ", Hoje));
        Assert.Equal(new DateTime(2026, 10, 1), ConsultaMesLancamentos.Resolver(" 2026-03 ", Hoje));
        Assert.Equal(new DateTime(2026, 10, 1), ConsultaMesLancamentos.Resolver("2026/03", Hoje));
    }

    [Fact]
    public void MesValido_AbreEsseMes()
    {
        Assert.Equal(new DateTime(2026, 3, 1), ConsultaMesLancamentos.Resolver("2026-03", Hoje));
    }

    [Fact]
    public void Rota_MesValido_LevaAListaDesseMes()
    {
        Assert.Equal("/lancamentos?mes=2026-03", ConsultaMesLancamentos.Rota("2026-03"));
        Assert.Equal("/lancamentos/novo?mes=2026-03", ConsultaMesLancamentos.Rota("2026-03", "/lancamentos/novo"));
    }

    [Fact]
    public void Rota_MesInvalido_FicaSemConsulta()
    {
        Assert.Equal("/lancamentos", ConsultaMesLancamentos.Rota(null));
        Assert.Equal("/lancamentos", ConsultaMesLancamentos.Rota(""));
        Assert.Equal("/lancamentos", ConsultaMesLancamentos.Rota("2026-13"));
        Assert.Equal("/lancamentos", ConsultaMesLancamentos.Rota("2026-3"));
        Assert.Equal("/lancamentos", ConsultaMesLancamentos.Rota("marco"));
        Assert.Equal("/lancamentos", ConsultaMesLancamentos.Rota("2026-03-01"));
        Assert.Equal("/lancamentos", ConsultaMesLancamentos.Rota(" 2026-03"));
        Assert.Equal("/lancamentos", ConsultaMesLancamentos.Rota("2026-03 "));
        Assert.Equal("/lancamentos", ConsultaMesLancamentos.Rota(" 2026-03 "));
        Assert.Equal("/lancamentos", ConsultaMesLancamentos.Rota("2026/03"));
        Assert.Equal("/lancamentos/novo", ConsultaMesLancamentos.Rota("marco", "/lancamentos/novo"));
    }

    [Fact]
    public void Rota_EmCalendarioTailandes_GravaAnoGregoriano()
    {
        var anterior = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("th-TH");
        try
        {
            Assert.Equal("2569-03", new DateTime(2026, 3, 1).ToString("yyyy-MM"));
            Assert.Equal("/lancamentos?mes=2026-03", ConsultaMesLancamentos.Rota("2026-03"));
            Assert.Equal(
                "/lancamentos/novo?mes=2026-10",
                ConsultaMesLancamentos.Rota("2026-10", "/lancamentos/novo"));
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
        }
    }
}
