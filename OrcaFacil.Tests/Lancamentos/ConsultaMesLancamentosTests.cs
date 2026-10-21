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
}
