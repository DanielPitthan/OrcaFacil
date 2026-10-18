using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;

namespace OrcaFacil.Tests.Data;

public class VerificadorIntegridadeBancoTests
{
    private const string PagamentoCesta = "20261013195343_AddPagamentoCompraCesta";
    private const string Cartoes = "20261015194450_AddCartoes";
    private const string Descritivo = "20261016011256_AddDescritivoItemFatura";
    private const string Estorno = "20261016182243_AddEhEstornoItemFatura";
    private const string HashImportacao = "20261017164213_AddHashImportacaoItemFatura";
    private const string IndiceHashImportacao = "20261017172616_AddIndiceHashImportacaoItemFatura";
    private const string IndiceHash = "IX_ItensLancamento_TransacaoId_HashImportacao";

    [Fact]
    public async Task BancoMigrado_EIntegro_ECheckNaoAlteraArquivo()
    {
        using var banco = new BancoTemporario();
        await using var ctx = banco.CriarContexto();
        await ctx.Database.MigrateAsync();
        await InserirAsync(ctx, """
            INSERT INTO MembrosFamilia (Nome, Avatar, Role, Ativo, CriadoEm)
            VALUES ('Daniel', 'D', 0, 1, '2026-01-15 00:00:00')
            """);

        var antes = await FotografarAsync(ctx);
        var relatorio = await VerificadorIntegridadeBanco.VerificarAsync(ctx);
        var depois = await FotografarAsync(ctx);

        Assert.Equal(antes, depois);
        Assert.True(relatorio.Integro);
        Assert.Empty(relatorio.TabelasFaltando);
        Assert.Empty(relatorio.ColunasFaltando);
        Assert.Empty(relatorio.IndicesFaltando);
        Assert.Contains("MembrosFamilia", relatorio.TabelasExistentes);
        Assert.Contains(Estorno, relatorio.MigrationsNoHistorico);
        Assert.Contains(IndiceHashImportacao, relatorio.MigrationsNoHistorico);
        Assert.DoesNotContain("20261013115908_AddItensFatura", relatorio.MigrationsNoHistoricoComObjetoFaltando);
    }

    [Fact]
    public async Task BancoAntigo_AntesDeMigrar_ApontaColunasEMigrationsQueFaltam_DepoisFicaIntegro()
    {
        using var banco = new BancoTemporario();
        await using var ctx = banco.CriarContexto();
        await ctx.Database.MigrateAsync(Cartoes);
        await InserirAsync(ctx, """
            INSERT INTO MembrosFamilia (Nome, Avatar, Role, Ativo, CriadoEm)
            VALUES ('Daniel', 'D', 0, 1, '2026-01-15 00:00:00')
            """);

        var antes = await FotografarAsync(ctx);
        var parcial = await VerificadorIntegridadeBanco.VerificarAsync(ctx);

        Assert.False(parcial.Integro);
        Assert.Contains("ItensLancamento", parcial.TabelasExistentes);
        Assert.Contains("Cartoes", parcial.TabelasExistentes);
        Assert.Empty(parcial.TabelasFaltando);
        Assert.Equal([IndiceHash], parcial.IndicesFaltando);
        Assert.Equal(
            ["ItensLancamento.Descritivo", "ItensLancamento.EhEstorno", "ItensLancamento.HashImportacao"],
            parcial.ColunasFaltando);
        Assert.Equal([Descritivo, Estorno, HashImportacao, IndiceHashImportacao], parcial.MigrationsAusentesNoHistorico);
        Assert.Empty(parcial.MigrationsPelaMetade);
        Assert.DoesNotContain(Cartoes, parcial.MigrationsAusentesNoHistorico);
        Assert.Equal(antes, await FotografarAsync(ctx));

        var resultado = await PreparoBanco.MigrarEVerificarAsync(ctx);
        Assert.Null(resultado.FalhaMigrate);
        Assert.False(resultado.ExigeRecuperacao);
        Assert.True(resultado.Relatorio.Integro);
        Assert.Equal("Daniel", await EscalarTextoAsync(ctx, "SELECT Nome FROM MembrosFamilia"));
    }

    [Fact]
    public async Task ColunaCriadaSemHistorico_ApontaAMigration_EAcionaRecuperacao()
    {
        using var banco = new BancoTemporario();
        await using var ctx = banco.CriarContexto();
        await ctx.Database.MigrateAsync(Descritivo);
        await ExecutarAsync(ctx, "ALTER TABLE ItensLancamento ADD COLUMN EhEstorno INTEGER NOT NULL DEFAULT 0");
        await InserirAsync(ctx, """
            INSERT INTO MembrosFamilia (Nome, Avatar, Role, Ativo, CriadoEm)
            VALUES ('Daniel', 'D', 0, 1, '2026-01-15 00:00:00')
            """);

        var antes = await FotografarAsync(ctx);
        var relatorio = await VerificadorIntegridadeBanco.VerificarAsync(ctx);
        Assert.Equal(antes, await FotografarAsync(ctx));

        Assert.False(relatorio.Integro);
        Assert.Equal(["ItensLancamento.HashImportacao"], relatorio.ColunasFaltando);
        Assert.DoesNotContain("ItensLancamento.EhEstorno", relatorio.ColunasFaltando);
        Assert.Equal([IndiceHash], relatorio.IndicesFaltando);
        Assert.Equal([Estorno, HashImportacao, IndiceHashImportacao], relatorio.MigrationsAusentesNoHistorico);
        Assert.Empty(relatorio.MigrationsPelaMetade);
        Assert.Empty(relatorio.TabelasFaltando);
        Assert.Contains(Estorno, relatorio.Resumo);

        var excecao = await Assert.ThrowsAsync<IntegridadeBancoException>(() => PreparoBanco.ExigirIntegroAsync(ctx));
        Assert.True(excecao.Resultado.ExigeRecuperacao);
        Assert.NotNull(excecao.Resultado.FalhaMigrate);
        Assert.Equal(
            [Estorno, HashImportacao, IndiceHashImportacao],
            excecao.Resultado.Relatorio.MigrationsAusentesNoHistorico);
        Assert.DoesNotContain("ItensLancamento.EhEstorno", excecao.Resultado.Relatorio.ColunasFaltando);
        Assert.Contains("ItensLancamento.HashImportacao", excecao.Resultado.Relatorio.ColunasFaltando);
        Assert.Contains(Estorno, excecao.Message);
        Assert.Equal("Daniel", await EscalarTextoAsync(ctx, "SELECT Nome FROM MembrosFamilia"));
        Assert.Equal("EhEstorno", await EscalarTextoAsync(ctx, "SELECT name FROM pragma_table_info('ItensLancamento') WHERE name = 'EhEstorno'"));
    }

    [Fact]
    public async Task TabelaFaltando_ComHistoricoCompleto_ApontaTabelaEAcionaRecuperacao()
    {
        using var banco = new BancoTemporario();
        await using var ctx = banco.CriarContexto();
        await ctx.Database.MigrateAsync();
        await InserirAsync(ctx, """
            INSERT INTO MembrosFamilia (Nome, Avatar, Role, Ativo, CriadoEm)
            VALUES ('Daniel', 'D', 0, 1, '2026-01-15 00:00:00')
            """);
        await ExecutarAsync(ctx, "PRAGMA foreign_keys = OFF");
        await ExecutarAsync(ctx, "DROP TABLE LogsEvento");

        var relatorio = await VerificadorIntegridadeBanco.VerificarAsync(ctx);
        Assert.Contains("LogsEvento", relatorio.TabelasFaltando);
        Assert.Contains("LogsEvento.Mensagem", relatorio.ColunasFaltando);
        Assert.Contains("IX_LogsEvento_CriadoEm", relatorio.IndicesFaltando);
        Assert.Contains("20261008141717_PerfilFechamentoLogs", relatorio.MigrationsNoHistoricoComObjetoFaltando);
        Assert.Empty(relatorio.MigrationsAusentesNoHistorico);
        Assert.Contains("LogsEvento", relatorio.Resumo);

        var excecao = await Assert.ThrowsAsync<IntegridadeBancoException>(() => PreparoBanco.ExigirIntegroAsync(ctx));
        Assert.True(excecao.Resultado.ExigeRecuperacao);
        Assert.Null(excecao.Resultado.FalhaMigrate);
        Assert.Contains("LogsEvento", excecao.Resultado.Relatorio.TabelasFaltando);
        Assert.Equal("Daniel", await EscalarTextoAsync(ctx, "SELECT Nome FROM MembrosFamilia"));
        Assert.Equal(0L, await EscalarAsync(ctx, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'LogsEvento'"));
    }

    [Fact]
    public async Task ColunaCartaoIdCriada_SemTabelaCartoes_VerificadorAponta_EColunaVaziaEReconciliada()
    {
        using var banco = new BancoTemporario();
        await using var ctx = banco.CriarContexto();
        await ctx.Database.MigrateAsync(PagamentoCesta);
        await ExecutarAsync(ctx, "ALTER TABLE Transacoes ADD COLUMN CartaoId INTEGER NULL");
        await InserirAsync(ctx, """
            INSERT INTO MembrosFamilia (Nome, Avatar, Role, Ativo, CriadoEm)
            VALUES ('Daniel', 'D', 0, 1, '2026-01-15 00:00:00')
            """);

        var antes = await FotografarAsync(ctx);
        var relatorio = await VerificadorIntegridadeBanco.VerificarAsync(ctx);
        Assert.Equal(antes, await FotografarAsync(ctx));

        Assert.Contains(Cartoes, relatorio.MigrationsPelaMetade);
        Assert.DoesNotContain(Descritivo, relatorio.MigrationsPelaMetade);
        Assert.DoesNotContain(Estorno, relatorio.MigrationsPelaMetade);
        Assert.Contains("Cartoes", relatorio.TabelasFaltando);
        Assert.Contains("RecargasCartao", relatorio.TabelasFaltando);
        Assert.Contains("ComprasCesta.CartaoBeneficioId", relatorio.ColunasFaltando);
        Assert.Contains("ComprasCesta.CartaoCreditoId", relatorio.ColunasFaltando);
        Assert.Contains("ComprasCesta.ItemFaturaId", relatorio.ColunasFaltando);
        Assert.DoesNotContain("Transacoes.CartaoId", relatorio.ColunasFaltando);
        Assert.Contains("ItensLancamento.Descritivo", relatorio.ColunasFaltando);
        Assert.Contains("ItensLancamento.EhEstorno", relatorio.ColunasFaltando);
        Assert.Contains("IX_Cartoes_Nome", relatorio.IndicesFaltando);
        Assert.Contains(Cartoes, relatorio.MigrationsAusentesNoHistorico);
        Assert.Contains("Transacoes", relatorio.TabelasExistentes);
        Assert.Contains("ComprasCesta", relatorio.TabelasExistentes);

        await PreparoBanco.ExigirIntegroAsync(ctx);
        await PreparoBanco.ExigirIntegroAsync(ctx);

        Assert.Equal("Daniel", await EscalarTextoAsync(ctx, "SELECT Nome FROM MembrosFamilia"));
        Assert.Equal("CartaoId", await EscalarTextoAsync(ctx, "SELECT name FROM pragma_table_info('Transacoes') WHERE name = 'CartaoId'"));
        Assert.Equal(1L, await EscalarAsync(ctx, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Cartoes'"));
        Assert.Contains(Cartoes, (await VerificadorIntegridadeBanco.VerificarAsync(ctx)).MigrationsNoHistorico);
        Assert.True((await VerificadorIntegridadeBanco.VerificarAsync(ctx)).Integro);
    }

    [Fact]
    public async Task ColunaCartaoIdComValor_SemTabelaCartoes_NaoRepeteMigrate_EMensagemClara()
    {
        using var banco = new BancoTemporario();
        await using var ctx = banco.CriarContexto();
        await ctx.Database.MigrateAsync(PagamentoCesta);
        await ExecutarAsync(ctx, "ALTER TABLE Transacoes ADD COLUMN CartaoId INTEGER NULL");
        await InserirAsync(ctx, """
            INSERT INTO Categorias (Nome, Icone, CorHex, TipoPadrao, EhPadraoDoSistema, PermiteItensFatura, Ativo)
            VALUES ('Alimentação', 'A', '#9fb4c7', 1, 1, 0, 1)
            """);
        await InserirAsync(ctx, """
            INSERT INTO MembrosFamilia (Nome, Avatar, Role, Ativo, CriadoEm)
            VALUES ('Daniel', 'D', 0, 1, '2026-01-15 00:00:00')
            """);
        var categoriaId = await EscalarAsync(ctx, "SELECT Id FROM Categorias WHERE Nome = 'Alimentação'");
        var membroId = await EscalarAsync(ctx, "SELECT Id FROM MembrosFamilia WHERE Nome = 'Daniel'");
        await InserirAsync(ctx, $"""
            INSERT INTO Transacoes (Descricao, Valor, Data, Tipo, Recorrencia, Pago, CategoriaId, MembroFamiliaId, CriadoEm, CartaoId)
            VALUES ('Mercado', '10.00', '2026-02-03 00:00:00', 1, 0, 1, {categoriaId}, {membroId}, '2026-02-03 00:00:00', 7)
            """);

        var antes = await VerificadorIntegridadeBanco.VerificarAsync(ctx);
        Assert.Contains("Cartoes", antes.TabelasFaltando);
        Assert.DoesNotContain("Transacoes.CartaoId", antes.ColunasFaltando);
        Assert.Contains(Cartoes, antes.MigrationsPelaMetade);

        var excecao = await Assert.ThrowsAsync<IntegridadeBancoException>(() => PreparoBanco.ExigirIntegroAsync(ctx));
        var deNovo = await Assert.ThrowsAsync<IntegridadeBancoException>(() => PreparoBanco.ExigirIntegroAsync(ctx));

        Assert.Contains("AddCartoes", excecao.Message);
        Assert.Contains("CartaoId", excecao.Message);
        Assert.Contains("tabela Cartoes não existe", excecao.Message);
        Assert.DoesNotContain("duplicate column", excecao.Message);
        Assert.Equal(excecao.Message, deNovo.Message);
        Assert.IsType<InvalidOperationException>(excecao.InnerException);
        Assert.Contains("Cartoes", excecao.Resultado.Relatorio.TabelasFaltando);
        Assert.Contains(Cartoes, excecao.Resultado.Relatorio.MigrationsPelaMetade);
        Assert.DoesNotContain(Cartoes, excecao.Resultado.Relatorio.MigrationsNoHistorico);
        Assert.Equal(7L, await EscalarAsync(ctx, "SELECT CartaoId FROM Transacoes"));
        Assert.Equal("Daniel", await EscalarTextoAsync(ctx, "SELECT Nome FROM MembrosFamilia"));
        Assert.Equal(0L, await EscalarAsync(ctx, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Cartoes'"));
    }

    [Fact]
    public async Task SchemaAddCartoesCompleto_SemHistorico_NaoRepeteAddDaColuna()
    {
        using var banco = new BancoTemporario();
        await using var ctx = banco.CriarContexto();
        await ctx.Database.MigrateAsync(Cartoes);
        await InserirAsync(ctx, """
            INSERT INTO MembrosFamilia (Nome, Avatar, Role, Ativo, CriadoEm)
            VALUES ('Daniel', 'D', 0, 1, '2026-01-15 00:00:00')
            """);
        await ExecutarAsync(ctx, $"DELETE FROM __EFMigrationsHistory WHERE MigrationId = '{Cartoes}'");

        var antes = await VerificadorIntegridadeBanco.VerificarAsync(ctx);
        Assert.Contains(Cartoes, antes.MigrationsAusentesNoHistorico);
        Assert.DoesNotContain("Transacoes.CartaoId", antes.ColunasFaltando);
        Assert.DoesNotContain("Cartoes", antes.TabelasFaltando);

        await PreparoBanco.ExigirIntegroAsync(ctx);
        await PreparoBanco.ExigirIntegroAsync(ctx);

        var depois = await VerificadorIntegridadeBanco.VerificarAsync(ctx);
        Assert.True(depois.Integro);
        Assert.Contains(Cartoes, depois.MigrationsNoHistorico);
        Assert.Equal("Daniel", await EscalarTextoAsync(ctx, "SELECT Nome FROM MembrosFamilia"));
        Assert.Equal(1L, await EscalarAsync(ctx, "SELECT COUNT(*) FROM pragma_table_info('Transacoes') WHERE name = 'CartaoId'"));
    }

    private static async Task<string> FotografarAsync(AppDbContext ctx)
    {
        var membros = await EscalarAsync(ctx, "SELECT COUNT(*) FROM MembrosFamilia");
        var esquema = await EscalarTextoAsync(ctx, """
            SELECT group_concat(type || ':' || name || ':' || ifnull(sql, ''), char(10))
            FROM (SELECT type, name, sql FROM sqlite_master ORDER BY type, name)
            """);
        var historico = await EscalarTextoAsync(ctx, """
            SELECT group_concat(MigrationId, ',') FROM (SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId)
            """);
        return $"{membros}|{historico}|{esquema}";
    }

    private static async Task InserirAsync(AppDbContext ctx, string sql)
    {
        var afetadas = await ctx.Database.ExecuteSqlRawAsync(sql);
        Assert.Equal(1, afetadas);
    }

    private static Task ExecutarAsync(AppDbContext ctx, string sql) => ctx.Database.ExecuteSqlRawAsync(sql);

    private static async Task<long> EscalarAsync(AppDbContext ctx, string sql)
    {
        var valor = await EscalarObjetoAsync(ctx, sql);
        return Convert.ToInt64(valor);
    }

    private static async Task<string> EscalarTextoAsync(AppDbContext ctx, string sql)
    {
        var valor = await EscalarObjetoAsync(ctx, sql);
        return valor is null or DBNull ? "" : Convert.ToString(valor) ?? "";
    }

    private static async Task<object?> EscalarObjetoAsync(AppDbContext ctx, string sql)
    {
        var conexao = ctx.Database.GetDbConnection();
        if (conexao.State != ConnectionState.Open)
            await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        return await comando.ExecuteScalarAsync();
    }

    private sealed class BancoTemporario : IDisposable
    {
        private readonly string _caminho;
        private readonly SqliteConnection _conexao;

        public BancoTemporario()
        {
            _caminho = Path.Combine(Path.GetTempPath(), $"orcafacil-int-{Guid.NewGuid():N}.db");
            _conexao = new SqliteConnection($"Data Source={_caminho}");
            _conexao.Open();
        }

        public AppDbContext CriarContexto()
        {
            var opcoes = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_conexao)
                .Options;
            return new AppDbContext(opcoes);
        }

        public void Dispose()
        {
            _conexao.Dispose();
            SqliteConnection.ClearAllPools();
            if (File.Exists(_caminho))
                File.Delete(_caminho);
        }
    }
}
