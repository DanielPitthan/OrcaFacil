using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Repositories;
using Xunit;

namespace OrcaFacil.Tests.Repositories;

public class OrcamentoRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly OrcamentoRepository _repository;
    private readonly CategoriaRepository _categoriaRepository;

    public OrcamentoRepositoryTests()
    {
        _repository = new OrcamentoRepository(_factory);
        _categoriaRepository = new CategoriaRepository(_factory);
    }

    private async Task<int> CriarCategoriaAsync()
    {
        var categoria = new Categoria { Nome = "Moradia", Icone = "🏠", CorHex = "#28587b" };
        await _categoriaRepository.AddAsync(categoria);
        return categoria.Id;
    }

    [Fact]
    public async Task GetByMesAnoAsync_DeveRetornarSomenteDoMesInformado()
    {
        var categoriaId = await CriarCategoriaAsync();
        await _repository.AddAsync(new Orcamento { CategoriaId = categoriaId, ValorLimite = 1000m, Mes = 1, Ano = 2026 });
        await _repository.AddAsync(new Orcamento { CategoriaId = categoriaId, ValorLimite = 1200m, Mes = 2, Ano = 2026 });

        var doMesUm = await _repository.GetByMesAnoAsync(1, 2026);

        Assert.Single(doMesUm);
        Assert.Equal(1000m, doMesUm[0].ValorLimite);
    }

    [Fact]
    public async Task IndiceUnico_DeveImpedirDoisOrcamentosParaMesmaCategoriaMesAno()
    {
        var categoriaId = await CriarCategoriaAsync();
        await _repository.AddAsync(new Orcamento { CategoriaId = categoriaId, ValorLimite = 1000m, Mes = 3, Ano = 2026 });

        await Assert.ThrowsAsync<DbUpdateException>(async () =>
            await _repository.AddAsync(new Orcamento { CategoriaId = categoriaId, ValorLimite = 500m, Mes = 3, Ano = 2026 }));
    }

    [Fact]
    public async Task AddAsync_DevePersistirMetaReducaoPercentual()
    {
        var categoriaId = await CriarCategoriaAsync();
        await _repository.AddAsync(new Orcamento
        {
            CategoriaId = categoriaId,
            ValorLimite = 900m,
            MetaReducaoPercentual = 10m,
            Mes = 4,
            Ano = 2026
        });

        var doMes = await _repository.GetByMesAnoAsync(4, 2026);

        Assert.Single(doMes);
        Assert.Equal(10m, doMes[0].MetaReducaoPercentual);
    }

    public void Dispose() => _factory.Dispose();
}
