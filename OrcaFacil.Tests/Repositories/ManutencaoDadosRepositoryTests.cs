using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Repositories;
using Xunit;

namespace OrcaFacil.Tests.Repositories;

public class ManutencaoDadosRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly ManutencaoDadosRepository _repository;

    public ManutencaoDadosRepositoryTests()
    {
        _repository = new ManutencaoDadosRepository(_factory);
    }

    private async Task PopularAsync()
    {
        await using var ctx = _factory.CreateDbContext();
        var categoria = new Categoria { Nome = "Moradia", Icone = "🏠", CorHex = "#659ECD" };
        var membro = new MembroFamilia { Nome = "Usuário" };
        ctx.AddRange(categoria, membro);
        await ctx.SaveChangesAsync();

        var origem = new Transacao { Descricao = "Aluguel", Valor = 1000m, Data = new DateTime(2026, 9, 5), Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Mensal, CategoriaId = categoria.Id, MembroFamiliaId = membro.Id };
        ctx.Transacoes.Add(origem);
        await ctx.SaveChangesAsync();

        ctx.Transacoes.Add(new Transacao { Descricao = "Aluguel", Valor = 1000m, Data = new DateTime(2026, 10, 5), Tipo = TipoTransacao.Despesa, CategoriaId = categoria.Id, MembroFamiliaId = membro.Id, TransacaoOrigemId = origem.Id });
        ctx.Orcamentos.Add(new Orcamento { CategoriaId = categoria.Id, ValorLimite = 1500m, Mes = 10, Ano = 2026 });
        ctx.Metas.Add(new Meta { Nome = "Reserva", ValorAlvo = 5000m, MembroFamiliaId = membro.Id });
        ctx.FechamentosPeriodo.Add(new FechamentoPeriodo { Mes = 9, Ano = 2026, Fechado = true });
        ctx.LogsEvento.Add(new LogEvento { Tipo = TipoLogEvento.Fechamento, Mensagem = "Mês 09/2026 fechado" });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task LimparAsync_Lancamentos_DeveManterOrcamentosEMetas()
    {
        await PopularAsync();

        await _repository.LimparAsync(TipoLimpezaDados.Lancamentos);

        var registros = await _repository.ContarRegistrosAsync();
        Assert.Equal(0, registros["Lançamentos"]);
        Assert.Equal(0, registros["Fechamentos de mês"]);
        Assert.Equal(1, registros["Orçamentos mensais"]);
        Assert.Equal(1, registros["Metas de economia"]);
        Assert.Equal(1, registros["Logs"]);
    }

    [Fact]
    public async Task LimparAsync_OrcamentosEMetas_DeveManterLancamentos()
    {
        await PopularAsync();

        await _repository.LimparAsync(TipoLimpezaDados.Orcamentos | TipoLimpezaDados.Metas);

        var registros = await _repository.ContarRegistrosAsync();
        Assert.Equal(2, registros["Lançamentos"]);
        Assert.Equal(0, registros["Orçamentos mensais"]);
        Assert.Equal(0, registros["Metas de economia"]);
    }

    [Fact]
    public async Task LimparAsync_Tudo_DeveZerarTodasAsTabelas()
    {
        await PopularAsync();

        await _repository.LimparAsync(TipoLimpezaDados.Tudo);

        var registros = await _repository.ContarRegistrosAsync();
        Assert.All(registros.Values, quantidade => Assert.Equal(0, quantidade));

        await using var ctx = _factory.CreateDbContext();
        Assert.Empty(ctx.MembrosFamilia);
    }

    public void Dispose() => _factory.Dispose();
}
