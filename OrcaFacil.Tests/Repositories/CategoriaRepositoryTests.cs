using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Repositories;
using Xunit;

namespace OrcaFacil.Tests.Repositories;

public class CategoriaRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly CategoriaRepository _repository;

    public CategoriaRepositoryTests()
    {
        _repository = new CategoriaRepository(_factory);
    }

    [Fact]
    public async Task AddAsync_DevePersistirCategoria()
    {
        var categoria = new Categoria { Nome = "Alimentação", Icone = "🛒", CorHex = "#9fb4c7", TipoPadrao = TipoTransacao.Despesa };

        await _repository.AddAsync(categoria);
        var todas = await _repository.GetAllAsync();

        Assert.Single(todas);
        Assert.Equal("Alimentação", todas[0].Nome);
    }

    [Fact]
    public async Task ExisteNomeAsync_DeveSerCaseInsensitive()
    {
        await _repository.AddAsync(new Categoria { Nome = "Moradia", Icone = "🏠", CorHex = "#28587b" });

        var existe = await _repository.ExisteNomeAsync("MORADIA");

        Assert.True(existe);
    }

    [Fact]
    public async Task ExisteNomeAsync_DeveIgnorarIdInformado()
    {
        var categoria = new Categoria { Nome = "Lazer", Icone = "🎉", CorHex = "#7f7caf" };
        await _repository.AddAsync(categoria);

        var existe = await _repository.ExisteNomeAsync("Lazer", ignorarId: categoria.Id);

        Assert.False(existe);
    }

    [Fact]
    public async Task NomeDaInativa_ContinuaNoIndiceUnico()
    {
        await _repository.AddAsync(new Categoria
        {
            Nome = "Recebimento",
            Icone = "💰",
            CorHex = "#6DA538",
            TipoPadrao = TipoTransacao.Receita,
            EhPadraoDoSistema = true
        });
        var antiga = (await _repository.GetAllAsync()).Single();
        antiga.Ativo = false;
        await _repository.UpdateAsync(antiga);

        Assert.False(antiga.Ativo);
        Assert.True(await _repository.ExisteNomeAsync("Recebimento"));
        await Assert.ThrowsAsync<DbUpdateException>(() => _repository.AddAsync(new Categoria
        {
            Nome = "Recebimento",
            Icone = "💵",
            CorHex = "#A2B94F",
            TipoPadrao = TipoTransacao.Receita
        }));
    }

    [Fact]
    public async Task GetAtivasAsync_NaoDeveRetornarCategoriasInativas()
    {
        await _repository.AddAsync(new Categoria { Nome = "Ativa", Icone = "✅", CorHex = "#9fb798", Ativo = true });
        await _repository.AddAsync(new Categoria { Nome = "Inativa", Icone = "❌", CorHex = "#9fb798", Ativo = false });

        var ativas = await _repository.GetAtivasAsync();

        Assert.Single(ativas);
        Assert.Equal("Ativa", ativas[0].Nome);
    }

    public void Dispose() => _factory.Dispose();
}
