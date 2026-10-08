using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Repositories;
using Xunit;

namespace OrcaFacil.Tests.Repositories;

public class LogEventoRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly LogEventoRepository _repository;

    public LogEventoRepositoryTests()
    {
        _repository = new LogEventoRepository(_factory);
    }

    [Fact]
    public async Task ListarAsync_DeveRetornarMaisRecentesPrimeiroEFiltrarPorTipo()
    {
        await _repository.RegistrarAsync(TipoLogEvento.Fechamento, "Mês 09/2026 fechado");
        await _repository.RegistrarAsync(TipoLogEvento.Reabertura, "Mês 09/2026 reaberto");

        var todos = await _repository.ListarAsync();
        Assert.Equal(2, todos.Count);
        Assert.Equal(TipoLogEvento.Reabertura, todos[0].Tipo);

        var fechamentos = await _repository.ListarAsync(TipoLogEvento.Fechamento);
        Assert.Single(fechamentos);
        Assert.Equal("Mês 09/2026 fechado", fechamentos[0].Mensagem);
    }

    [Fact]
    public async Task LimparAsync_DeveRemoverTodosOsLogs()
    {
        await _repository.RegistrarAsync(TipoLogEvento.Sistema, "a");
        await _repository.RegistrarAsync(TipoLogEvento.Sistema, "b");

        var removidos = await _repository.LimparAsync();

        Assert.Equal(2, removidos);
        Assert.Empty(await _repository.ListarAsync());
    }

    public void Dispose() => _factory.Dispose();
}
