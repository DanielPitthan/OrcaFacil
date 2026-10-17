using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;

namespace OrcaFacil.Tests.Lancamentos;

public class AddHashImportacaoItemFaturaMigrationTests
{
    [Fact]
    public void Migracao_AdicionaHashNuloEDownRemoveSoAColuna()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var anterior = new AppDbContext(options))
        {
            anterior.Database.Migrate("20261016182243_AddEhEstornoItemFatura");
            anterior.Database.ExecuteSqlRaw(
                """
                INSERT INTO Categorias (Nome, Icone, CorHex, TipoPadrao, EhPadraoDoSistema, PermiteItensFatura, Ativo)
                VALUES ('Cartão de Crédito', '💳', '#B2CFE6', 1, 1, 1, 1),
                       ('Alimentação', '🛒', '#B2CFE6', 1, 0, 0, 1);
                INSERT INTO MembrosFamilia (Nome, Avatar, Ativo, CriadoEm, Role)
                VALUES ('Daniel', '👤', 1, '2026-10-01 00:00:00', 0);
                INSERT INTO Transacoes (Descricao, Valor, Data, Tipo, Recorrencia, Pago, CategoriaId, MembroFamiliaId, CriadoEm)
                VALUES ('Fatura Nubank', '80', '2026-10-18 00:00:00', 1, 0, 1, 1, 1, '2026-10-18 00:00:00');
                INSERT INTO ItensLancamento (Descricao, Descritivo, Valor, Natureza, Ordem, TransacaoId, CategoriaId, EhEstorno)
                VALUES ('Alimentação', 'McDonald''s', '40', 0, 0, 1, 2, 1);
                """);
        }

        using (var atual = new AppDbContext(options))
        {
            atual.Database.Migrate();

            var item = Assert.Single(atual.ItensLancamento);
            Assert.Null(item.HashImportacao);
            Assert.True(item.EhEstorno);
            Assert.Equal("McDonald's", item.Descritivo);
            Assert.Equal(40m, item.Valor);
            Assert.Equal(80m, atual.Transacoes.Single().Valor);

            item.HashImportacao = new string('A', 64);
            atual.SaveChanges();
        }

        using (var voltar = new AppDbContext(options))
        {
            voltar.Database.Migrate("20261016182243_AddEhEstornoItemFatura");
            Assert.Equal(0L, Escalar(connection, "SELECT COUNT(*) FROM pragma_table_info('ItensLancamento') WHERE name = 'HashImportacao'"));
            Assert.Equal("Alimentação", EscalarTexto(connection, "SELECT Descricao FROM ItensLancamento"));
            Assert.Equal("McDonald's", EscalarTexto(connection, "SELECT Descritivo FROM ItensLancamento"));
            Assert.Equal("40", EscalarTexto(connection, "SELECT Valor FROM ItensLancamento"));
            Assert.Equal(1L, Escalar(connection, "SELECT EhEstorno FROM ItensLancamento"));
            Assert.Equal("80", EscalarTexto(connection, "SELECT Valor FROM Transacoes"));
        }

        using var deNovo = new AppDbContext(options);
        deNovo.Database.Migrate();
        var preservado = Assert.Single(deNovo.ItensLancamento);
        Assert.Null(preservado.HashImportacao);
        Assert.True(preservado.EhEstorno);
        Assert.Equal("McDonald's", preservado.Descritivo);
        Assert.Equal(40m, preservado.Valor);
        Assert.Equal("Alimentação", preservado.Descricao);
        Assert.Equal(80m, deNovo.Transacoes.Single().Valor);
    }

    private static long Escalar(SqliteConnection connection, string sql)
    {
        using var comando = connection.CreateCommand();
        comando.CommandText = sql;
        return (long)comando.ExecuteScalar()!;
    }

    private static string EscalarTexto(SqliteConnection connection, string sql)
    {
        using var comando = connection.CreateCommand();
        comando.CommandText = sql;
        return (string)comando.ExecuteScalar()!;
    }
}
