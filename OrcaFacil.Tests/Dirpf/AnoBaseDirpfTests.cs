using OrcaFacil.Core.Dirpf;

namespace OrcaFacil.Tests.Dirpf;

public class AnoBaseDirpfTests
{
    [Fact]
    public void Anterior_EAnoCivilAnterior()
    {
        Assert.Equal(2025, AnoBaseDirpf.Anterior(new DateTime(2026, 10, 20)));
        Assert.Equal(2025, AnoBaseDirpf.Anterior(new DateTime(2026, 1, 1)));
        Assert.Equal(2024, AnoBaseDirpf.Anterior(new DateTime(2025, 12, 31)));
    }
}
