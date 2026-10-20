using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.B3;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Services;

namespace OrcaFacil.Tests.Investimentos;

public class DestinoLancamentosImportacaoTests
{
    [Fact]
    public async Task Modelo_EscolheMarco2026_EListaOsOnzeMeses()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        await using var csv = File.OpenRead(CaminhoCsv());
        var previa = await ambiente.Service.PreverAsync(csv, ambiente.MembroId);

        var destino = DestinoLancamentosImportacao.Calcular(
            previa.Linhas.Where(linha => linha.Marcada && linha.PodeMarcar && linha.Comando is not null));

        Assert.Equal(43, destino.Quantidade);
        Assert.Equal(new DateTime(2026, 3, 1), destino.MesMaisRecente);
        Assert.Equal("/lancamentos?mes=2026-03", destino.Rota);
        Assert.Equal(
            [
                new DateTime(2023, 1, 1),
                new DateTime(2023, 2, 1),
                new DateTime(2023, 4, 1),
                new DateTime(2023, 5, 1),
                new DateTime(2025, 4, 1),
                new DateTime(2025, 7, 1),
                new DateTime(2025, 8, 1),
                new DateTime(2025, 9, 1),
                new DateTime(2025, 10, 1),
                new DateTime(2025, 11, 1),
                new DateTime(2026, 3, 1)
            ],
            destino.Meses);
        Assert.Equal(
            "43 lançamentos em jan/2023, fev/2023, abr/2023, mai/2023, abr/2025, jul/2025, ago/2025, set/2025, out/2025, nov/2025 e mar/2026.",
            destino.Texto);
    }

    [Fact]
    public void ResultadoZero_NaoGeraLancamento_ProventoGera()
    {
        var linhas = new LinhaPreviaExtratoB3[]
        {
            Linha(1, Comando(DestinoImportacaoB3.OperacaoAtivo, new DateTime(2024, 1, 2), TipoOperacao.Compra, 10m, 100m)),
            Linha(2, Comando(DestinoImportacaoB3.OperacaoAtivo, new DateTime(2024, 2, 2), TipoOperacao.Venda, 10m, 100m)),
            Linha(3, Comando(DestinoImportacaoB3.RendaFixa, new DateTime(2024, 1, 3), quantidade: 5m, valor: 50m, renda: TipoOperacaoRendaFixa.Aplicacao)),
            Linha(4, Comando(DestinoImportacaoB3.RendaFixa, new DateTime(2024, 2, 3), quantidade: 5m, valor: 50m, renda: TipoOperacaoRendaFixa.Resgate)),
            Linha(5, Comando(DestinoImportacaoB3.Provento, new DateTime(2024, 3, 4), quantidade: 1m, valor: 2m))
        };

        var destino = DestinoLancamentosImportacao.Calcular(linhas);

        Assert.Equal(1, destino.Quantidade);
        Assert.Equal([new DateTime(2024, 3, 1)], destino.Meses);
        Assert.Equal("/lancamentos?mes=2024-03", destino.Rota);
    }

    [Fact]
    public void SoCompra_NaoGeraLancamento()
    {
        var destino = DestinoLancamentosImportacao.Calcular(
        [
            Linha(1, Comando(DestinoImportacaoB3.OperacaoAtivo, new DateTime(2024, 6, 1), TipoOperacao.Compra, 1m, 10m))
        ]);

        Assert.Equal(0, destino.Quantidade);
        Assert.Empty(destino.Meses);
        Assert.Equal("/ativos", destino.Rota);
        Assert.Equal("Nenhum lançamento foi gerado.", destino.Texto);
    }

    private static LinhaPreviaExtratoB3 Linha(int numero, ComandoImportacaoB3 comando) => new()
    {
        Numero = numero,
        Marcada = true,
        PodeMarcar = true,
        Comando = comando
    };

    private static ComandoImportacaoB3 Comando(
        DestinoImportacaoB3 destino,
        DateTime data,
        TipoOperacao operacao = TipoOperacao.Compra,
        decimal quantidade = 1m,
        decimal valor = 1m,
        TipoOperacaoRendaFixa renda = TipoOperacaoRendaFixa.Aplicacao) => new()
    {
        Destino = destino,
        Hash = "h" + data.Ticks,
        Data = data,
        Ticker = destino == DestinoImportacaoB3.RendaFixa ? "CDB1" : "PETR4",
        Nome = "Teste",
        Instituicao = "XP",
        Corretora = "XP",
        TipoAtivo = TipoAtivo.Acao,
        TipoOperacao = operacao,
        TipoRendaFixa = renda,
        Quantidade = quantidade,
        PrecoUnitario = quantidade == 0 ? 0 : valor / quantidade,
        Valor = valor
    };

    private static string CaminhoCsv()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var noProjeto = Path.Combine(dir.FullName, "Fixtures", "B3", "extrato-b3-modelo.csv");
            if (File.Exists(noProjeto))
                return noProjeto;
            var naRaiz = Path.Combine(dir.FullName, "OrcaFacil.Tests", "Fixtures", "B3", "extrato-b3-modelo.csv");
            if (File.Exists(naRaiz))
                return naRaiz;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Fixture do extrato B3 não encontrada.");
    }

    private sealed class Ambiente : IAsyncDisposable
    {
        public ImportacaoExtratoB3Service Service { get; }
        public int MembroId { get; }
        private readonly SqliteConnection _conexao;

        private Ambiente(ImportacaoExtratoB3Service service, int membroId, SqliteConnection conexao)
        {
            Service = service;
            MembroId = membroId;
            _conexao = conexao;
        }

        public static async Task<Ambiente> CriarAsync()
        {
            var conexao = new SqliteConnection("DataSource=:memory:");
            await conexao.OpenAsync();
            var opcoes = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conexao).Options;
            int membroId;
            await using (var contexto = new AppDbContext(opcoes))
            {
                contexto.Database.EnsureCreated();
                var membro = new MembroFamilia { Nome = "Usuário" };
                contexto.MembrosFamilia.Add(membro);
                await contexto.SaveChangesAsync();
                membroId = membro.Id;
            }

            var fabrica = new Fabrica(opcoes);
            var servico = new ImportacaoExtratoB3Service(
                new ParserExtratoB3(),
                fabrica,
                new ResultadoRealizadoService(fabrica),
                new ProventoService(fabrica),
                new RendaFixaService(fabrica));
            return new Ambiente(servico, membroId, conexao);
        }

        public async ValueTask DisposeAsync() => await _conexao.DisposeAsync();

        private sealed class Fabrica(DbContextOptions<AppDbContext> opcoes) : IDbContextFactory<AppDbContext>
        {
            public AppDbContext CreateDbContext() => new(opcoes);
            public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(CreateDbContext());
        }
    }
}
