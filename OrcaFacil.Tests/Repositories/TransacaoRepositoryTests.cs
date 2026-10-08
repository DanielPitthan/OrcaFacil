using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Repositories;
using Xunit;

namespace OrcaFacil.Tests.Repositories;

public class TransacaoRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly TransacaoRepository _repository;
    private readonly CategoriaRepository _categoriaRepository;
    private readonly MembroFamiliaRepository _membroRepository;

    public TransacaoRepositoryTests()
    {
        _repository = new TransacaoRepository(_factory);
        _categoriaRepository = new CategoriaRepository(_factory);
        _membroRepository = new MembroFamiliaRepository(_factory);
    }

    private async Task<(int categoriaId, int membroId)> CriarDependenciasAsync()
    {
        var categoria = new Categoria { Nome = "Alimentação", Icone = "🛒", CorHex = "#9fb4c7" };
        await _categoriaRepository.AddAsync(categoria);

        var membro = new MembroFamilia { Nome = "Usuário" };
        await _membroRepository.AddAsync(membro);

        return (categoria.Id, membro.Id);
    }

    [Fact]
    public async Task SomaPorCategoriaAsync_DeveAgruparCorretamente()
    {
        var (categoriaId, membroId) = await CriarDependenciasAsync();
        var mes = new DateTime(2026, 3, 1);

        await _repository.AddAsync(new Transacao { Descricao = "Mercado", Valor = 100m, Data = mes.AddDays(2), Tipo = TipoTransacao.Despesa, CategoriaId = categoriaId, MembroFamiliaId = membroId });
        await _repository.AddAsync(new Transacao { Descricao = "Feira", Valor = 50m, Data = mes.AddDays(10), Tipo = TipoTransacao.Despesa, CategoriaId = categoriaId, MembroFamiliaId = membroId });

        var somas = await _repository.SomaPorCategoriaAsync(mes, mes.AddMonths(1).AddTicks(-1), TipoTransacao.Despesa);

        Assert.Equal(150m, somas[categoriaId]);
    }

    [Fact]
    public async Task SomaPorTipoAsync_DeveFiltrarPorPeriodo()
    {
        var (categoriaId, membroId) = await CriarDependenciasAsync();

        await _repository.AddAsync(new Transacao { Descricao = "Dentro", Valor = 200m, Data = new DateTime(2026, 3, 15), Tipo = TipoTransacao.Receita, CategoriaId = categoriaId, MembroFamiliaId = membroId });
        await _repository.AddAsync(new Transacao { Descricao = "Fora", Valor = 999m, Data = new DateTime(2026, 4, 1), Tipo = TipoTransacao.Receita, CategoriaId = categoriaId, MembroFamiliaId = membroId });

        var total = await _repository.SomaPorTipoAsync(TipoTransacao.Receita, new DateTime(2026, 3, 1), new DateTime(2026, 3, 31, 23, 59, 59));

        Assert.Equal(200m, total);
    }

    [Fact]
    public async Task GetRecorrentesAtivasAsync_DeveIgnorarUnicas()
    {
        var (categoriaId, membroId) = await CriarDependenciasAsync();

        await _repository.AddAsync(new Transacao { Descricao = "Assinatura", Valor = 30m, Data = DateTime.Today, Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Mensal, CategoriaId = categoriaId, MembroFamiliaId = membroId });
        await _repository.AddAsync(new Transacao { Descricao = "Compra avulsa", Valor = 30m, Data = DateTime.Today, Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Unica, CategoriaId = categoriaId, MembroFamiliaId = membroId });

        var recorrentes = await _repository.GetRecorrentesAtivasAsync();

        Assert.Single(recorrentes);
        Assert.Equal("Assinatura", recorrentes[0].Descricao);
    }

    [Fact]
    public async Task SomaPorTipoAsync_DeveFiltrarPorPago()
    {
        var (categoriaId, membroId) = await CriarDependenciasAsync();
        var mes = new DateTime(2026, 3, 1);

        await _repository.AddAsync(new Transacao { Descricao = "Paga", Valor = 100m, Data = mes.AddDays(2), Tipo = TipoTransacao.Despesa, Pago = true, CategoriaId = categoriaId, MembroFamiliaId = membroId });
        await _repository.AddAsync(new Transacao { Descricao = "Pendente", Valor = 50m, Data = mes.AddDays(10), Tipo = TipoTransacao.Despesa, Pago = false, CategoriaId = categoriaId, MembroFamiliaId = membroId });

        var totalPago = await _repository.SomaPorTipoAsync(TipoTransacao.Despesa, mes, mes.AddMonths(1).AddTicks(-1), pago: true);
        var totalGeral = await _repository.SomaPorTipoAsync(TipoTransacao.Despesa, mes, mes.AddMonths(1).AddTicks(-1));

        Assert.Equal(100m, totalPago);
        Assert.Equal(150m, totalGeral);
    }

    [Fact]
    public async Task GetDespesasRecorrentesPorPeriodoAsync_DeveFiltrarTipoRecorrenciaEPeriodo()
    {
        var (categoriaId, membroId) = await CriarDependenciasAsync();
        var marco = new DateTime(2026, 3, 15);

        await _repository.AddAsync(new Transacao { Descricao = "Assinatura", Valor = 30m, Data = marco, Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Mensal, CategoriaId = categoriaId, MembroFamiliaId = membroId });
        await _repository.AddAsync(new Transacao { Descricao = "Avulsa", Valor = 30m, Data = marco, Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Unica, CategoriaId = categoriaId, MembroFamiliaId = membroId });
        await _repository.AddAsync(new Transacao { Descricao = "Salário", Valor = 3000m, Data = marco, Tipo = TipoTransacao.Receita, Recorrencia = TipoRecorrencia.Mensal, CategoriaId = categoriaId, MembroFamiliaId = membroId });
        await _repository.AddAsync(new Transacao { Descricao = "Fora do período", Valor = 30m, Data = marco.AddMonths(1), Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Mensal, CategoriaId = categoriaId, MembroFamiliaId = membroId });

        var elegiveis = await _repository.GetDespesasRecorrentesPorPeriodoAsync(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31, 23, 59, 59));

        Assert.Single(elegiveis);
        Assert.Equal("Assinatura", elegiveis[0].Descricao);
    }

    [Fact]
    public async Task GetOrigemIdsComCopiaNoPeriodoAsync_DeveRetornarApenasOrigensJaCopiadasNoPeriodo()
    {
        var (categoriaId, membroId) = await CriarDependenciasAsync();

        var origem = new Transacao { Descricao = "Assinatura", Valor = 30m, Data = new DateTime(2026, 3, 15), Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Mensal, CategoriaId = categoriaId, MembroFamiliaId = membroId };
        await _repository.AddAsync(origem);

        var copiaAbril = new Transacao { Descricao = "Assinatura", Valor = 30m, Data = new DateTime(2026, 4, 15), Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Mensal, Pago = false, CategoriaId = categoriaId, MembroFamiliaId = membroId, TransacaoOrigemId = origem.Id };
        await _repository.AddAsync(copiaAbril);

        var idsAbril = await _repository.GetOrigemIdsComCopiaNoPeriodoAsync(new DateTime(2026, 4, 1), new DateTime(2026, 4, 30, 23, 59, 59));
        var idsMaio = await _repository.GetOrigemIdsComCopiaNoPeriodoAsync(new DateTime(2026, 5, 1), new DateTime(2026, 5, 31, 23, 59, 59));

        Assert.Contains(origem.Id, idsAbril);
        Assert.DoesNotContain(origem.Id, idsMaio);
    }

    [Fact]
    public async Task UpdateAsync_DevePermitirAlterarCategoriaComNavegacaoCarregada()
    {
        var (categoriaId, membroId) = await CriarDependenciasAsync();
        var outra = new Categoria { Nome = "Moradia", Icone = "🏠", CorHex = "#659ecd" };
        await _categoriaRepository.AddAsync(outra);

        var transacao = new Transacao
        {
            Descricao = "Conta",
            Valor = 100m,
            Data = DateTime.Today,
            Tipo = TipoTransacao.Despesa,
            CategoriaId = categoriaId,
            MembroFamiliaId = membroId
        };
        await _repository.AddAsync(transacao);

        var carregada = await _repository.GetByIdAsync(transacao.Id);
        Assert.NotNull(carregada);
        Assert.NotNull(carregada.Categoria);

        carregada.CategoriaId = outra.Id;
        await _repository.UpdateAsync(carregada);

        var atualizada = await _repository.GetByIdAsync(transacao.Id);
        Assert.NotNull(atualizada);
        Assert.Equal(outra.Id, atualizada.CategoriaId);
        Assert.Equal("Moradia", atualizada.Categoria?.Nome);
    }

    [Fact]
    public async Task GetByPeriodoAsync_DeveOrdenarReceitasAntesDeDespesas()
    {
        var (categoriaId, membroId) = await CriarDependenciasAsync();
        var mes = new DateTime(2026, 3, 1);

        await _repository.AddAsync(new Transacao { Descricao = "Mercado", Valor = 50m, Data = mes.AddDays(10), Tipo = TipoTransacao.Despesa, CategoriaId = categoriaId, MembroFamiliaId = membroId });
        await _repository.AddAsync(new Transacao { Descricao = "Salário", Valor = 3000m, Data = mes.AddDays(5), Tipo = TipoTransacao.Receita, CategoriaId = categoriaId, MembroFamiliaId = membroId });
        await _repository.AddAsync(new Transacao { Descricao = "Freelance", Valor = 200m, Data = mes.AddDays(15), Tipo = TipoTransacao.Receita, CategoriaId = categoriaId, MembroFamiliaId = membroId });

        var lista = await _repository.GetByPeriodoAsync(mes, mes.AddMonths(1).AddTicks(-1));

        Assert.Equal(3, lista.Count);
        Assert.All(lista.Take(2), t => Assert.Equal(TipoTransacao.Receita, t.Tipo));
        Assert.Equal(TipoTransacao.Despesa, lista[2].Tipo);
        Assert.Equal("Freelance", lista[0].Descricao);
        Assert.Equal("Salário", lista[1].Descricao);
    }

    public void Dispose() => _factory.Dispose();
}
