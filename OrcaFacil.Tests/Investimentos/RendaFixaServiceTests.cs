using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.RendaFixa;
using OrcaFacil.Core.Services;

namespace OrcaFacil.Tests.Investimentos;

public class RendaFixaServiceTests
{
    [Fact]
    public async Task Prejuizo_ViraDespesa_ELucro_ViraReceita()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var titulo = await ambiente.TituloAsync("CDB1", TipoTituloRendaFixa.CDB);

        await ambiente.Service.SalvarAsync(Operacao(titulo, new DateTime(2026, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 1m, 100m), ambiente.MembroId);
        var resgate = await ambiente.Service.SalvarAsync(
            Operacao(titulo, new DateTime(2026, 2, 10), TipoOperacaoRendaFixa.Resgate, 1m, 70m),
            ambiente.MembroId);

        var lancamento = await ambiente.UnicoLancamentoAsync();
        Assert.Equal(TipoTransacao.Despesa, lancamento.Tipo);
        Assert.Equal(30m, lancamento.Valor);
        Assert.Equal(new DateTime(2026, 2, 10), lancamento.Data);
        Assert.Equal(resgate.Id, lancamento.OperacaoRendaFixaId);
        Assert.Equal(ambiente.MembroId, lancamento.MembroFamiliaId);
        Assert.True(lancamento.Pago);
        Assert.Equal(TipoRecorrencia.Unica, lancamento.Recorrencia);
        Assert.Empty(lancamento.Itens);
        Assert.Equal(RendaFixaService.CategoriaPerda, lancamento.Categoria!.Nome);

        await ambiente.Service.SalvarAsync(new OperacaoRendaFixaFormModel
        {
            Id = resgate.Id,
            TituloRendaFixaId = titulo,
            Data = resgate.Data,
            Tipo = TipoOperacaoRendaFixa.Resgate,
            Quantidade = 1m,
            PrecoUnitario = 1m,
            Valor = 110m
        }, ambiente.MembroId);

        var atualizado = await ambiente.UnicoLancamentoAsync();
        Assert.Equal(lancamento.Id, atualizado.Id);
        Assert.Equal(TipoTransacao.Receita, atualizado.Tipo);
        Assert.Equal(10m, atualizado.Valor);
        Assert.Equal(RendaFixaService.CategoriaGanho, atualizado.Categoria!.Nome);
    }

    [Fact]
    public async Task IrRetido_ReduzOResultado()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var titulo = await ambiente.TituloAsync("CDB2", TipoTituloRendaFixa.CDB);
        await ambiente.Service.SalvarAsync(Operacao(titulo, new DateTime(2026, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 1m, 100m), ambiente.MembroId);
        await ambiente.Service.SalvarAsync(new OperacaoRendaFixaFormModel
        {
            TituloRendaFixaId = titulo,
            Data = new DateTime(2026, 2, 10),
            Tipo = TipoOperacaoRendaFixa.Resgate,
            Quantidade = 1m,
            PrecoUnitario = 1m,
            Valor = 120m,
            Custos = 2m,
            IrRetido = 5m
        }, ambiente.MembroId);

        var lancamento = await ambiente.UnicoLancamentoAsync();
        Assert.Equal(TipoTransacao.Receita, lancamento.Tipo);
        Assert.Equal(13m, lancamento.Valor);
    }

    [Fact]
    public async Task ResgateParcial_GeraReceita_EDeixaPosicao()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var titulo = await ambiente.TituloAsync("CDB3", TipoTituloRendaFixa.CDB);
        await ambiente.Service.SalvarAsync(Operacao(titulo, new DateTime(2026, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 10m, 100m), ambiente.MembroId);
        await ambiente.Service.SalvarAsync(Operacao(titulo, new DateTime(2026, 2, 10), TipoOperacaoRendaFixa.Resgate, 4m, 50m), ambiente.MembroId);

        Assert.Equal(10m, (await ambiente.UnicoLancamentoAsync()).Valor);
        var posicao = await ambiente.Service.ObterPosicaoAsync(titulo);
        Assert.Equal(6m, posicao.Quantidade);
        Assert.Equal(60m, posicao.CustoTotal);
        Assert.Equal(10m, posicao.CustoMedio);
    }

    [Fact]
    public async Task ResultadoZero_RemoveOLancamento_SemDuplicarNaEdicao()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var titulo = await ambiente.TituloAsync("CDB4", TipoTituloRendaFixa.CDB);
        await ambiente.Service.SalvarAsync(Operacao(titulo, new DateTime(2026, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 1m, 100m), ambiente.MembroId);
        var resgate = await ambiente.Service.SalvarAsync(Operacao(titulo, new DateTime(2026, 2, 10), TipoOperacaoRendaFixa.Resgate, 1m, 110m), ambiente.MembroId);
        var idLancamento = (await ambiente.UnicoLancamentoAsync()).Id;

        await ambiente.Service.SalvarAsync(new OperacaoRendaFixaFormModel
        {
            Id = resgate.Id,
            TituloRendaFixaId = titulo,
            Data = resgate.Data,
            Tipo = TipoOperacaoRendaFixa.Resgate,
            Quantidade = 1m,
            PrecoUnitario = 1m,
            Valor = 100m
        }, ambiente.MembroId);

        Assert.Empty(await ambiente.LancamentosAsync());
        Assert.NotEqual(0, idLancamento);
    }

    [Fact]
    public async Task ExcluirResgate_RemoveOLancamento()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var titulo = await ambiente.TituloAsync("CDB5", TipoTituloRendaFixa.CDB);
        await ambiente.Service.SalvarAsync(Operacao(titulo, new DateTime(2026, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 1m, 100m), ambiente.MembroId);
        var resgate = await ambiente.Service.SalvarAsync(Operacao(titulo, new DateTime(2026, 2, 10), TipoOperacaoRendaFixa.Resgate, 1m, 110m), ambiente.MembroId);

        await ambiente.Service.ExcluirAsync(resgate.Id, ambiente.MembroId);

        Assert.Empty(await ambiente.LancamentosAsync());
        var posicao = await ambiente.Service.ObterPosicaoAsync(titulo);
        Assert.Equal(1m, posicao.Quantidade);
        Assert.Equal(100m, posicao.CustoTotal);
    }

    [Fact]
    public async Task ResgateSemPosicao_NaoGrava()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var titulo = await ambiente.TituloAsync("CDBC25AQGPU", TipoTituloRendaFixa.CDB, "Itaú");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ambiente.Service.SalvarAsync(
            Operacao(titulo, new DateTime(2026, 3, 11), TipoOperacaoRendaFixa.Resgate, 264024m, 2718.50m, 0.018117m),
            ambiente.MembroId));

        Assert.Contains("CDBC25AQGPU", ex.Message, StringComparison.Ordinal);
        Assert.Contains("excede a posição", ex.Message, StringComparison.Ordinal);
        Assert.Empty(await ambiente.OperacoesAsync());
        Assert.Empty(await ambiente.LancamentosAsync());
    }

    [Fact]
    public async Task MesFechado_DeixaResgateSomenteLeitura_ENaoGrava()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var titulo = await ambiente.TituloAsync("CDB6", TipoTituloRendaFixa.CDB);
        await ambiente.Service.SalvarAsync(Operacao(titulo, new DateTime(2026, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 1m, 100m), ambiente.MembroId);
        var resgate = await ambiente.Service.SalvarAsync(Operacao(titulo, new DateTime(2026, 2, 10), TipoOperacaoRendaFixa.Resgate, 1m, 110m), ambiente.MembroId);
        await ambiente.FecharAsync(2, 2026);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ambiente.Service.SalvarAsync(new OperacaoRendaFixaFormModel
        {
            Id = resgate.Id,
            TituloRendaFixaId = titulo,
            Data = resgate.Data,
            Tipo = TipoOperacaoRendaFixa.Resgate,
            Quantidade = 1m,
            PrecoUnitario = 1m,
            Valor = 130m
        }, ambiente.MembroId));

        Assert.Contains("fechado", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Nada foi gravado", ex.Message, StringComparison.Ordinal);
        Assert.Equal(10m, (await ambiente.UnicoLancamentoAsync()).Valor);
        Assert.Equal(110m, (await ambiente.OperacaoAsync(resgate.Id)).Valor);
    }

    [Fact]
    public async Task AplicacaoRetroativa_QueMudariaResgateEmMesFechado_NaoGrava()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var titulo = await ambiente.TituloAsync("CDB7", TipoTituloRendaFixa.CDB);
        var aplicacao = await ambiente.Service.SalvarAsync(
            Operacao(titulo, new DateTime(2026, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 10m, 100m),
            ambiente.MembroId);
        await ambiente.Service.SalvarAsync(Operacao(titulo, new DateTime(2026, 2, 10), TipoOperacaoRendaFixa.Resgate, 10m, 150m), ambiente.MembroId);
        await ambiente.FecharAsync(2, 2026);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ambiente.Service.SalvarAsync(new OperacaoRendaFixaFormModel
        {
            Id = aplicacao.Id,
            TituloRendaFixaId = titulo,
            Data = aplicacao.Data,
            Tipo = TipoOperacaoRendaFixa.Aplicacao,
            Quantidade = 10m,
            PrecoUnitario = 1m,
            Valor = 80m
        }, ambiente.MembroId));

        Assert.Contains("Mês fechado", ex.Message, StringComparison.Ordinal);
        Assert.Contains("CDB7", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Nada foi gravado", ex.Message, StringComparison.Ordinal);
        Assert.Equal(100m, (await ambiente.OperacaoAsync(aplicacao.Id)).Valor);
        var lancamento = await ambiente.UnicoLancamentoAsync();
        Assert.Equal(50m, lancamento.Valor);
        Assert.Equal(TipoTransacao.Receita, lancamento.Tipo);
    }

    [Fact]
    public async Task AplicacaoEmMesFechado_SemLancarResultado_EPermitida()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var titulo = await ambiente.TituloAsync("CDB8", TipoTituloRendaFixa.CDB);
        await ambiente.FecharAsync(1, 2026);

        await ambiente.Service.SalvarAsync(Operacao(titulo, new DateTime(2026, 1, 15), TipoOperacaoRendaFixa.Aplicacao, 2m, 40m), ambiente.MembroId);

        Assert.Equal(2m, (await ambiente.Service.ObterPosicaoAsync(titulo)).Quantidade);
        Assert.Empty(await ambiente.LancamentosAsync());
    }

    [Fact]
    public async Task ExcluirTitulo_ComLancamentoEmMesFechado_NaoGrava()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var titulo = await ambiente.TituloAsync("CDB9", TipoTituloRendaFixa.CDB);
        await ambiente.Service.SalvarAsync(Operacao(titulo, new DateTime(2026, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 1m, 100m), ambiente.MembroId);
        await ambiente.Service.SalvarAsync(Operacao(titulo, new DateTime(2026, 2, 10), TipoOperacaoRendaFixa.Resgate, 1m, 110m), ambiente.MembroId);
        await ambiente.FecharAsync(2, 2026);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ambiente.Service.ExcluirTituloAsync(titulo));

        Assert.Contains("Mês fechado", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Nada foi gravado", ex.Message, StringComparison.Ordinal);
        Assert.NotNull(await ambiente.ContextoAsync(ctx => ctx.TitulosRendaFixa.FirstOrDefaultAsync(t => t.Id == titulo)));
        Assert.Equal(2, (await ambiente.OperacoesAsync()).Count);
        Assert.Single(await ambiente.LancamentosAsync());
    }

    [Fact]
    public async Task ExcluirTitulo_ComMesAberto_ApagaOperacoesELancamento()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var titulo = await ambiente.TituloAsync("CDB10", TipoTituloRendaFixa.CDB);
        await ambiente.Service.SalvarAsync(Operacao(titulo, new DateTime(2026, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 1m, 100m), ambiente.MembroId);
        await ambiente.Service.SalvarAsync(Operacao(titulo, new DateTime(2026, 2, 10), TipoOperacaoRendaFixa.Resgate, 1m, 90m), ambiente.MembroId);

        await ambiente.Service.ExcluirTituloAsync(titulo);

        Assert.Empty(await ambiente.OperacoesAsync());
        Assert.Empty(await ambiente.LancamentosAsync());
        Assert.Null(await ambiente.ContextoAsync(ctx => ctx.TitulosRendaFixa.FirstOrDefaultAsync(t => t.Id == titulo)));
    }

    [Fact]
    public async Task ManualEImportada_NaoColidemNoHash_EdicaoPreservaOrigem()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var titulo = await ambiente.TituloAsync("CDB11", TipoTituloRendaFixa.CDB);
        await ambiente.Service.SalvarAsync(Operacao(titulo, new DateTime(2026, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 1m, 10m), ambiente.MembroId);
        await ambiente.Service.SalvarAsync(Operacao(titulo, new DateTime(2026, 1, 11), TipoOperacaoRendaFixa.Aplicacao, 1m, 10m), ambiente.MembroId);

        var importada = await ambiente.Service.SalvarAsync(new OperacaoRendaFixaFormModel
        {
            TituloRendaFixaId = titulo,
            Data = new DateTime(2026, 1, 12),
            Tipo = TipoOperacaoRendaFixa.Aplicacao,
            Quantidade = 1m,
            PrecoUnitario = 1m,
            Valor = 10m,
            Origem = OrigemOperacaoRendaFixa.ExtratoB3,
            HashImportacao = "abc123"
        }, ambiente.MembroId);

        var repetida = await Assert.ThrowsAsync<InvalidOperationException>(() => ambiente.Service.SalvarAsync(new OperacaoRendaFixaFormModel
        {
            TituloRendaFixaId = titulo,
            Data = new DateTime(2026, 1, 13),
            Tipo = TipoOperacaoRendaFixa.Aplicacao,
            Quantidade = 1m,
            PrecoUnitario = 1m,
            Valor = 11m,
            Origem = OrigemOperacaoRendaFixa.ExtratoB3,
            HashImportacao = "abc123"
        }, ambiente.MembroId));
        Assert.Equal(RendaFixaService.MensagemHashRepetido, repetida.Message);
        Assert.Equal(3, (await ambiente.OperacoesAsync()).Count);

        await ambiente.Service.SalvarAsync(new OperacaoRendaFixaFormModel
        {
            Id = importada.Id,
            TituloRendaFixaId = titulo,
            Data = importada.Data,
            Tipo = TipoOperacaoRendaFixa.Aplicacao,
            Quantidade = 1m,
            PrecoUnitario = 1m,
            Valor = 12m,
            Origem = OrigemOperacaoRendaFixa.Manual,
            HashImportacao = null
        }, ambiente.MembroId);

        var gravada = await ambiente.OperacaoAsync(importada.Id);
        Assert.Equal(12m, gravada.Valor);
        Assert.Equal(OrigemOperacaoRendaFixa.ExtratoB3, gravada.Origem);
        Assert.Equal("abc123", gravada.HashImportacao);
    }

    [Fact]
    public async Task QuantidadeFracionaria_PreservaOitoCasas()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var titulo = await ambiente.TituloAsync("Tesouro IPCA+ 2045", TipoTituloRendaFixa.TesouroIPCA, "Clear");
        var aplicacao = await ambiente.Service.SalvarAsync(
            Operacao(titulo, new DateTime(2023, 1, 20), TipoOperacaoRendaFixa.Aplicacao, 0.06m, 61.50m, 1050.41m),
            ambiente.MembroId);
        await ambiente.Service.SalvarAsync(
            Operacao(titulo, new DateTime(2023, 5, 26), TipoOperacaoRendaFixa.Resgate, 0.06m, 70.07m, 1979.71m),
            ambiente.MembroId);

        Assert.Equal(0.06m, (await ambiente.OperacaoAsync(aplicacao.Id)).Quantidade);
        Assert.Equal(8.57m, (await ambiente.UnicoLancamentoAsync()).Valor);
        Assert.Equal(0m, (await ambiente.Service.ObterPosicaoAsync(titulo)).Quantidade);
    }

    [Fact]
    public async Task FalhaNoMeioDoSave_DesfazResgateELancamento()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var opcoes = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var falha = new FalhaNoMeioDoSave();
        var opcoesComFalha = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(falha).Options;

        int tituloId;
        int membroId;
        await using (var contexto = new AppDbContext(opcoes))
        {
            contexto.Database.EnsureCreated();
            var membro = new MembroFamilia { Nome = "Usuário" };
            var titulo = new TituloRendaFixa
            {
                Tipo = TipoTituloRendaFixa.CDB,
                Codigo = "CDB12",
                Nome = "CDB12",
                Corretora = "XP"
            };
            contexto.AddRange(membro, titulo);
            await contexto.SaveChangesAsync();
            tituloId = titulo.Id;
            membroId = membro.Id;
        }

        var limpo = new RendaFixaService(new Fabrica(opcoes));
        await limpo.SalvarAsync(Operacao(tituloId, new DateTime(2026, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 1m, 100m), membroId);

        var comFalha = new RendaFixaService(new Fabrica(opcoesComFalha));
        var ex = await Assert.ThrowsAsync<IOException>(() => comFalha.SalvarAsync(
            Operacao(tituloId, new DateTime(2026, 2, 10), TipoOperacaoRendaFixa.Resgate, 1m, 110m),
            membroId));

        Assert.Contains("meio do SaveChanges", ex.Message, StringComparison.Ordinal);
        await using var conferido = new AppDbContext(opcoes);
        Assert.Equal(100m, Assert.Single(await conferido.OperacoesRendaFixa.ToListAsync()).Valor);
        Assert.Empty(await conferido.Transacoes.ToListAsync());
    }

    [Fact]
    public void LancamentoGerado_NaoSeEditaPelaTela()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ProtecaoLancamentoRendaFixa.GarantirEditavelNaTela(4));
        Assert.Equal(ProtecaoLancamentoRendaFixa.Mensagem, ex.Message);
        ProtecaoLancamentoRendaFixa.GarantirEditavelNaTela(null);
    }

    private static OperacaoRendaFixaFormModel Operacao(
        int tituloId,
        DateTime data,
        TipoOperacaoRendaFixa tipo,
        decimal quantidade,
        decimal valor,
        decimal preco = 1m)
        => new()
        {
            TituloRendaFixaId = tituloId,
            Data = data,
            Tipo = tipo,
            Quantidade = quantidade,
            PrecoUnitario = preco,
            Valor = valor
        };

    private sealed class FalhaNoMeioDoSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
            => throw new IOException("falha no meio do SaveChanges");
    }

    private sealed class Fabrica(DbContextOptions<AppDbContext> opcoes) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(opcoes);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class Ambiente : IAsyncDisposable
    {
        private readonly TestDbContextFactory _factory;
        public RendaFixaService Service { get; }
        public int MembroId { get; }

        private Ambiente(TestDbContextFactory factory, RendaFixaService service, int membroId)
        {
            _factory = factory;
            Service = service;
            MembroId = membroId;
        }

        public static async Task<Ambiente> CriarAsync()
        {
            var factory = new TestDbContextFactory();
            await using var contexto = await factory.CreateDbContextAsync();
            var membro = new MembroFamilia { Nome = "Usuário" };
            contexto.MembrosFamilia.Add(membro);
            await contexto.SaveChangesAsync();
            return new Ambiente(factory, new RendaFixaService(factory), membro.Id);
        }

        public async Task<int> TituloAsync(string codigo, TipoTituloRendaFixa tipo, string corretora = "XP")
        {
            await using var contexto = await _factory.CreateDbContextAsync();
            var titulo = new TituloRendaFixa
            {
                Tipo = tipo,
                Codigo = codigo,
                Nome = codigo,
                Corretora = corretora
            };
            contexto.TitulosRendaFixa.Add(titulo);
            await contexto.SaveChangesAsync();
            return titulo.Id;
        }

        public async Task FecharAsync(int mes, int ano)
        {
            await using var contexto = await _factory.CreateDbContextAsync();
            contexto.FechamentosPeriodo.Add(new FechamentoPeriodo { Mes = mes, Ano = ano, Fechado = true });
            await contexto.SaveChangesAsync();
        }

        public async Task<List<Transacao>> LancamentosAsync()
        {
            await using var contexto = await _factory.CreateDbContextAsync();
            return await contexto.Transacoes.Include(t => t.Categoria).Include(t => t.Itens).ToListAsync();
        }

        public async Task<Transacao> UnicoLancamentoAsync()
            => Assert.Single(await LancamentosAsync());

        public async Task<List<OperacaoRendaFixa>> OperacoesAsync()
        {
            await using var contexto = await _factory.CreateDbContextAsync();
            return await contexto.OperacoesRendaFixa.ToListAsync();
        }

        public async Task<OperacaoRendaFixa> OperacaoAsync(int id)
        {
            await using var contexto = await _factory.CreateDbContextAsync();
            return await contexto.OperacoesRendaFixa.FirstAsync(o => o.Id == id);
        }

        public async Task<T> ContextoAsync<T>(Func<AppDbContext, Task<T>> consulta)
        {
            await using var contexto = await _factory.CreateDbContextAsync();
            return await consulta(contexto);
        }

        public ValueTask DisposeAsync()
        {
            _factory.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
