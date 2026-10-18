using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;

namespace OrcaFacil.Tests.Lancamentos;

public class AddEhEstornoItemFaturaMigrationTests
{
    [Fact]
    public void Migracao_AdicionaEhEstornoFalsoSemAlterarLinhasAntigas()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var anterior = new AppDbContext(options))
        {
            anterior.Database.Migrate("20261016011256_AddDescritivoItemFatura");
            anterior.Database.ExecuteSqlRaw(
                """
                INSERT INTO Categorias (Nome, Icone, CorHex, TipoPadrao, EhPadraoDoSistema, PermiteItensFatura, Ativo)
                VALUES ('Cartão de Crédito', '💳', '#B2CFE6', 1, 1, 1, 1),
                       ('Alimentação', '🛒', '#B2CFE6', 1, 0, 0, 1);
                INSERT INTO MembrosFamilia (Nome, Avatar, Ativo, CriadoEm, Role)
                VALUES ('Daniel', '👤', 1, '2026-10-01 00:00:00', 0);
                INSERT INTO Transacoes (Descricao, Valor, Data, Tipo, Recorrencia, Pago, CategoriaId, MembroFamiliaId, CriadoEm)
                VALUES ('Fatura Nubank', '80', '2026-10-18 00:00:00', 1, 0, 1, 1, 1, '2026-10-18 00:00:00');
                INSERT INTO ItensLancamento (Descricao, Descritivo, Valor, Natureza, Ordem, TransacaoId, CategoriaId)
                VALUES ('Alimentação', 'McDonald''s', '40', 0, 0, 1, 2),
                       ('Alimentação', 'Big Steak', '40', 0, 1, 1, 2);
                """);
        }

        using (var atual = new AppDbContext(options))
        {
            atual.Database.Migrate();

            var fatura = Assert.Single(atual.Transacoes);
            Assert.Equal(80m, fatura.Valor);
            Assert.Equal("Fatura Nubank", fatura.Descricao);
            Assert.True(fatura.Pago);

            var itens = atual.ItensLancamento.OrderBy(i => i.Ordem).ToList();
            Assert.Equal(2, itens.Count);
            Assert.All(itens, item =>
            {
                Assert.Equal("Alimentação", item.Descricao);
                Assert.Equal(40m, item.Valor);
                Assert.Equal(2, item.CategoriaId);
                Assert.False(item.EhEstorno);
            });
            Assert.Equal(["McDonald's", "Big Steak"], itens.Select(i => i.Descritivo));

            itens[1].EhEstorno = true;
            atual.SaveChanges();
        }

        using var conferido = new AppDbContext(options);
        var gravados = conferido.ItensLancamento.OrderBy(i => i.Ordem).ToList();
        Assert.False(gravados[0].EhEstorno);
        Assert.True(gravados[1].EhEstorno);
        Assert.Equal(80m, conferido.Transacoes.Single().Valor);
        Assert.Equal(["McDonald's", "Big Steak"], gravados.Select(i => i.Descritivo));
    }

    [Fact]
    public void Down_DevolveAMagnitudeDoEstornoAoTotalERemoveAColuna()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var atual = new AppDbContext(options))
        {
            atual.Database.Migrate();
            atual.Database.ExecuteSqlRaw(
                """
                INSERT INTO Categorias (Nome, Icone, CorHex, TipoPadrao, EhPadraoDoSistema, PermiteItensFatura, Ativo)
                VALUES ('Cartão de Crédito', '💳', '#B2CFE6', 1, 1, 1, 1),
                       ('Alimentação', '🛒', '#B2CFE6', 1, 0, 0, 1);
                INSERT INTO MembrosFamilia (Nome, Avatar, Ativo, CriadoEm, Role)
                VALUES ('Daniel', '👤', 1, '2026-10-01 00:00:00', 0);
                INSERT INTO Transacoes (Descricao, Valor, Data, Tipo, Recorrencia, Pago, CategoriaId, MembroFamiliaId, CriadoEm)
                VALUES ('Fatura Nubank', '120', '2026-10-18 00:00:00', 1, 0, 0, 1, 1, '2026-10-18 00:00:00');
                INSERT INTO ItensLancamento (Descricao, Descritivo, Valor, Natureza, Ordem, TransacaoId, CategoriaId, EhEstorno)
                VALUES ('Alimentação', 'Mercado', '50', 0, 0, 1, 2, 0),
                       ('Estorno', 'Devolução', '80', 0, 1, 1, 2, 1);
                """);
        }

        using (var voltar = new AppDbContext(options))
            voltar.Database.Migrate("20261016011256_AddDescritivoItemFatura");

        Assert.Equal(200m, LerDecimal(connection, "SELECT Valor FROM Transacoes"));
        Assert.Equal(0, LerInteiro(connection, "SELECT COUNT(*) FROM pragma_table_info('ItensLancamento') WHERE name = 'EhEstorno'"));
        Assert.Equal(2, LerInteiro(connection, "SELECT COUNT(*) FROM ItensLancamento"));
        Assert.Equal(50m, LerDecimal(connection, "SELECT Valor FROM ItensLancamento WHERE Ordem = 0"));
        Assert.Equal(80m, LerDecimal(connection, "SELECT Valor FROM ItensLancamento WHERE Ordem = 1"));
    }

    private static decimal LerDecimal(SqliteConnection connection, string sql)
    {
        using var comando = connection.CreateCommand();
        comando.CommandText = sql;
        return Convert.ToDecimal(comando.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int LerInteiro(SqliteConnection connection, string sql)
    {
        using var comando = connection.CreateCommand();
        comando.CommandText = sql;
        return Convert.ToInt32(comando.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
