using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Tests.Data;

public class AddRegraCatalogoComprovanteMigrationTests
{
    private const string Anterior = "20261020032353_AddRendaFixa";

    [Fact]
    public void Down_RemoveATabelaEPreservaORestante()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;

        using (var atual = new AppDbContext(options))
        {
            atual.Database.Migrate();
            atual.Categorias.Add(new Categoria
            {
                Nome = "Alimentação",
                Icone = "A",
                CorHex = "#112233"
            });
            atual.SaveChanges();
            var regra = Assert.Single(atual.RegrasCatalogoComprovante);
            Assert.Equal(300, regra.LimiteProdutos);
            regra.LimiteProdutos = 40;
            atual.SaveChanges();
            Assert.Equal(40L, Escalar(connection, "SELECT LimiteProdutos FROM RegrasCatalogoComprovante"));
        }

        using (var voltar = new AppDbContext(options))
            voltar.Database.Migrate(Anterior);

        Assert.Equal(0L, Escalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'RegrasCatalogoComprovante'"));
        Assert.Equal(1L, Escalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'TitulosRendaFixa'"));
        Assert.Equal(1L, Escalar(connection, "SELECT COUNT(*) FROM Categorias WHERE Nome = 'Alimentação'"));
    }

    private static long Escalar(SqliteConnection connection, string sql)
    {
        using var comando = connection.CreateCommand();
        comando.CommandText = sql;
        return (long)comando.ExecuteScalar()!;
    }
}
