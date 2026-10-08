using OrcaFacil.Core.Repositories;
using Xunit;

namespace OrcaFacil.Tests.Repositories;

public class FechamentoRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly FechamentoRepository _repository;

    public FechamentoRepositoryTests()
    {
        _repository = new FechamentoRepository(_factory);
    }

    [Fact]
    public async Task EstaFechadoAsync_SemRegistro_DeveRetornarFalso()
    {
        Assert.False(await _repository.EstaFechadoAsync(10, 2026));
    }

    [Fact]
    public async Task AlternarAsync_DeveFecharEReabrirOPeriodo()
    {
        Assert.True(await _repository.AlternarAsync(10, 2026));
        Assert.True(await _repository.EstaFechadoAsync(10, 2026));

        Assert.False(await _repository.AlternarAsync(10, 2026));
        Assert.False(await _repository.EstaFechadoAsync(10, 2026));

        var registros = await _repository.GetAllAsync();
        Assert.Single(registros);
    }

    [Fact]
    public async Task AlternarAsync_NaoDeveAfetarOutrosMeses()
    {
        await _repository.AlternarAsync(10, 2026);

        Assert.False(await _repository.EstaFechadoAsync(9, 2026));
        Assert.False(await _repository.EstaFechadoAsync(10, 2025));
    }

    public void Dispose() => _factory.Dispose();
}
