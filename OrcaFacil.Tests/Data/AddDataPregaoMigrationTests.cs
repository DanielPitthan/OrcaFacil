using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Dirpf;

namespace OrcaFacil.Tests.Data;

public class AddDataPregaoMigrationTests
{
    private const string Anterior = "20261021101911_AddApuracaoIrMensal";

    [Fact]
    public void Backfill_ManualCopiaAData_ImportadaRecuaD2PeloCalendario()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;

        using (var anterior = new AppDbContext(options))
        {
            anterior.Database.Migrate(Anterior);
            anterior.Database.ExecuteSqlRaw(
                """
                INSERT INTO MembrosFamilia (Nome, Avatar, Role, Ativo, CriadoEm)
                VALUES ('Ana', 'A', 0, 1, '2024-01-01 00:00:00');
                INSERT INTO AtivosFinanceiros (Ticker, Nome, Tipo, Ativo, CriadoEm)
                VALUES ('PETR4', 'Petrobras', 0, 1, '2024-01-01 00:00:00');
                INSERT INTO OperacoesAtivo
                    (AtivoId, MembroFamiliaId, Tipo, Data, Quantidade, PrecoUnitario, ValorOperacao, Custos, CriadoEm, Origem)
                VALUES
                    (1, 1, 0, '2024-01-31 00:00:00', '1', '10', '10', '0', '2024-01-31 00:00:00', 0),
                    (1, 1, 1, '2024-11-22 00:00:00', '1', '10', '10', '0', '2024-11-22 00:00:00', 1),
                    (1, 1, 1, '2024-02-02 00:00:00', '1', '10', '10', '0', '2024-02-02 00:00:00', 1),
                    (1, 1, 1, '2025-03-06 00:00:00', '1', '10', '10', '0', '2025-03-06 00:00:00', 1);
                """);
            Assert.Equal(0L, Escalar(connection, "SELECT COUNT(*) FROM pragma_table_info('OperacoesAtivo') WHERE name = 'DataPregao'"));
        }

        using var atual = new AppDbContext(options);
        atual.Database.Migrate();

        var manual = atual.OperacoesAtivo.Single(o => o.Data == new DateTime(2024, 1, 31));
        Assert.Equal(new DateTime(2024, 1, 31), manual.Data);
        Assert.Equal(manual.Data, manual.DataPregao);

        ConferirImportada(atual, new DateTime(2024, 11, 22), new DateTime(2024, 11, 19));
        ConferirImportada(atual, new DateTime(2024, 2, 2), new DateTime(2024, 1, 31));
        ConferirImportada(atual, new DateTime(2025, 3, 6), new DateTime(2025, 2, 28));
    }

    private static void ConferirImportada(AppDbContext contexto, DateTime liquidacao, DateTime pregao)
    {
        var operacao = contexto.OperacoesAtivo.Single(o => o.Data == liquidacao);
        Assert.Equal(liquidacao, operacao.Data);
        Assert.Equal(pregao, operacao.DataPregao);
        Assert.Equal(CalendarioBancarioNacional.RecuarDiasUteis(liquidacao, DataPregaoOperacao.DiasUteisLiquidacaoVista), operacao.DataPregao);
    }

    private static long Escalar(SqliteConnection connection, string sql)
    {
        using var comando = connection.CreateCommand();
        comando.CommandText = sql;
        return (long)comando.ExecuteScalar()!;
    }
}
