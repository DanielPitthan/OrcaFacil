using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Repositories;
using Xunit;

namespace OrcaFacil.Tests.Repositories;

public class MetaRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly MetaRepository _repository;
    private readonly MembroFamiliaRepository _membroRepository;

    public MetaRepositoryTests()
    {
        _repository = new MetaRepository(_factory);
        _membroRepository = new MembroFamiliaRepository(_factory);
    }

    private async Task<int> CriarMembroAsync()
    {
        var membro = new MembroFamilia { Nome = "Usuário" };
        await _membroRepository.AddAsync(membro);
        return membro.Id;
    }

    [Fact]
    public async Task GetAtivasAsync_NaoDeveRetornarMetasConcluidas()
    {
        var membroId = await CriarMembroAsync();
        await _repository.AddAsync(new Meta { Nome = "Ativa", ValorAlvo = 1000m, ValorAtual = 200m, Concluida = false, MembroFamiliaId = membroId });
        await _repository.AddAsync(new Meta { Nome = "Concluída", ValorAlvo = 1000m, ValorAtual = 1000m, Concluida = true, MembroFamiliaId = membroId });

        var ativas = await _repository.GetAtivasAsync();

        Assert.Single(ativas);
        Assert.Equal("Ativa", ativas[0].Nome);
    }

    [Fact]
    public async Task GetByMembroAsync_DeveFiltrarPorMembro()
    {
        var membroId1 = await CriarMembroAsync();
        var membroId2 = await CriarMembroAsync();

        await _repository.AddAsync(new Meta { Nome = "Meta 1", ValorAlvo = 500m, MembroFamiliaId = membroId1 });
        await _repository.AddAsync(new Meta { Nome = "Meta 2", ValorAlvo = 500m, MembroFamiliaId = membroId2 });

        var doMembro1 = await _repository.GetByMembroAsync(membroId1);

        Assert.Single(doMembro1);
        Assert.Equal("Meta 1", doMembro1[0].Nome);
    }

    public void Dispose() => _factory.Dispose();
}
