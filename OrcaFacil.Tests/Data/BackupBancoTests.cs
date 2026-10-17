using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;

namespace OrcaFacil.Tests.Data;

public class BackupBancoTests
{
    private const string PagamentoCesta = "20261013195343_AddPagamentoCompraCesta";
    private const string Cartoes = "20261015194450_AddCartoes";

    [Fact]
    public async Task BackupPelaApiDoSqlite_IncluiLinhaQueACopiaDoArquivoPerdeNoWal_ENaoApagaOAnterior()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "orcafacil-bak-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        var caminho = Path.Combine(pasta, "orcafacil.db");
        await using var conexao = new SqliteConnection($"Data Source={caminho}");
        await conexao.OpenAsync();

        try
        {
            await ExecutarAsync(conexao, "PRAGMA journal_mode=WAL;");
            await ExecutarAsync(conexao, "PRAGMA wal_autocheckpoint=0;");

            var opcoes = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conexao).Options;
            await using var ctx = new AppDbContext(opcoes);
            await ctx.Database.MigrateAsync(PagamentoCesta);
            await ExecutarAsync(conexao, "PRAGMA wal_checkpoint(TRUNCATE);");
            await ctx.Database.ExecuteSqlRawAsync("""
                INSERT INTO MembrosFamilia (Nome, Avatar, Role, Ativo, CriadoEm)
                VALUES ('SoNoWal', 'W', 0, 1, '2026-01-15 00:00:00')
                """);

            var copia = Path.Combine(pasta, "copia-so-db.db");
            File.Copy(caminho, copia);
            Assert.Equal(0L, await ContarAsync(copia, "SELECT COUNT(*) FROM MembrosFamilia WHERE Nome = 'SoNoWal'"));

            var agora = new DateTime(2026, 10, 17, 21, 4, 5, 6, DateTimeKind.Utc);
            var primeiro = await BackupBanco.CriarSeMigrationPendenteAsync(ctx, "1.2.3", default, agora);
            var colisao = await BackupBanco.CriarSeMigrationPendenteAsync(ctx, "1.2.3", default, agora);
            var segundo = await BackupBanco.CriarSeMigrationPendenteAsync(ctx, "1.2.3", default, agora.AddSeconds(1));

            Assert.NotNull(primeiro);
            Assert.NotNull(colisao);
            Assert.NotNull(segundo);
            Assert.Contains("20261017T210405006Z", Path.GetFileName(primeiro));
            Assert.Contains("1.2.3", Path.GetFileName(primeiro));
            Assert.Contains(Cartoes, Path.GetFileName(primeiro));
            Assert.EndsWith("-2.db", colisao);
            Assert.NotEqual(primeiro, segundo);
            Assert.Equal("SoNoWal", await EscalarTextoAsync(primeiro, "SELECT Nome FROM MembrosFamilia"));
            Assert.True(File.Exists(primeiro));
            Assert.True(File.Exists(colisao));
            Assert.True(File.Exists(segundo));

            await ctx.Database.MigrateAsync();
            Assert.Null(await BackupBanco.CriarSeMigrationPendenteAsync(ctx, "1.2.3"));
            Assert.True(File.Exists(primeiro));
            Assert.True(File.Exists(colisao));
            Assert.True(File.Exists(segundo));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(pasta))
                Directory.Delete(pasta, recursive: true);
        }
    }

    [Fact]
    public async Task Preparo_CriaBackupAntesDeMigrar_ENaoCriaOutroQuandoNaoHaPendente()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "orcafacil-prep-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        var caminho = Path.Combine(pasta, "orcafacil.db");
        var conexaoTexto = $"Data Source={caminho}";

        try
        {
            var opcoes = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conexaoTexto).Options;
            await using (var ctx = new AppDbContext(opcoes))
            {
                await ctx.Database.MigrateAsync(PagamentoCesta);
                await ctx.Database.ExecuteSqlRawAsync("""
                    INSERT INTO MembrosFamilia (Nome, Avatar, Role, Ativo, CriadoEm)
                    VALUES ('Daniel', 'D', 0, 1, '2026-01-15 00:00:00')
                    """);
            }

            await using (var ctx = new AppDbContext(opcoes))
            {
                var resultado = await PreparoBanco.MigrarEVerificarAsync(
                    ctx,
                    new OpcoesPreparoBanco { VersaoAplicativo = "9.9.9" });

                Assert.False(resultado.ExigeRecuperacao);
                Assert.NotNull(resultado.CaminhoBackup);
                Assert.Contains("9.9.9", Path.GetFileName(resultado.CaminhoBackup));
                Assert.Contains(Cartoes, Path.GetFileName(resultado.CaminhoBackup));
                Assert.Equal("Daniel", await EscalarTextoAsync(resultado.CaminhoBackup, "SELECT Nome FROM MembrosFamilia"));
                Assert.Equal("Daniel", await EscalarTextoAsync(caminho, "SELECT Nome FROM MembrosFamilia"));

                var deNovo = await PreparoBanco.MigrarEVerificarAsync(
                    ctx,
                    new OpcoesPreparoBanco { VersaoAplicativo = "9.9.9" });
                Assert.Null(deNovo.CaminhoBackup);
                Assert.True(File.Exists(resultado.CaminhoBackup));
                Assert.Single(Directory.GetFiles(Path.Combine(pasta, "backups"), "orcafacil-backup-*.db"));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(pasta))
                Directory.Delete(pasta, recursive: true);
        }
    }

    [Fact]
    public async Task SemTabelaDeUsuario_OuMemoria_NaoCriaBackup()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "orcafacil-vazio-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        var caminho = Path.Combine(pasta, "orcafacil.db");

        try
        {
            var opcoesArquivo = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={caminho}")
                .Options;
            await using (var ctx = new AppDbContext(opcoesArquivo))
            {
                Assert.Null(await BackupBanco.CriarSeMigrationPendenteAsync(ctx, "1.0.0"));
            }

            Assert.False(Directory.Exists(Path.Combine(pasta, "backups")));

            var opcoesMemoria = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;
            await using var memoria = new AppDbContext(opcoesMemoria);
            Assert.Null(await BackupBanco.CriarSeMigrationPendenteAsync(memoria, "1.0.0"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(pasta))
                Directory.Delete(pasta, recursive: true);
        }
    }

    private static async Task ExecutarAsync(SqliteConnection conexao, string sql)
    {
        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<long> ContarAsync(string caminho, string sql)
    {
        await using var conexao = new SqliteConnection($"Data Source={caminho}");
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        return Convert.ToInt64(await comando.ExecuteScalarAsync());
    }

    private static async Task<string> EscalarTextoAsync(string caminho, string sql)
    {
        await using var conexao = new SqliteConnection($"Data Source={caminho}");
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        return Convert.ToString(await comando.ExecuteScalarAsync()) ?? "";
    }
}
