using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Dirpf;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Tests.Dirpf;

public class AddCodigoDirpfMigrationTests
{
    private const string Anterior = "20261020175956_AddProvento";

    [Fact]
    public void BancoComLancamento_SobeASemente_RecusaVigenciaRepetida_EVolta()
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
                VALUES ('Daniel', 'D', 0, 1, '2026-01-15 00:00:00');
                INSERT INTO Categorias (Nome, Icone, CorHex, TipoPadrao, EhPadraoDoSistema, PermiteItensFatura, Ativo)
                VALUES ('Alimentação', 'A', '#9fb4c7', 1, 1, 0, 1);
                INSERT INTO Transacoes (Descricao, Valor, Data, Tipo, Recorrencia, Pago, CategoriaId, MembroFamiliaId, CriadoEm)
                VALUES ('Mercado', '42.50', '2026-02-03 00:00:00', 1, 0, 1, 1, 1, '2026-02-03 00:00:00');
                """);
            Assert.Equal(0L, Escalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'CodigosDirpf'"));
        }

        using (var atual = new AppDbContext(options))
        {
            atual.Database.Migrate();

            Assert.Equal(CatalogoCodigosDirpf.Linhas().Count, atual.CodigosDirpf.Count());
            Assert.All(atual.CodigosDirpf, codigo => Assert.True(codigo.EhSistema));
            Assert.Equal(0L, Escalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_CodigosDirpf_Ficha_TipoAtivo_VigenciaInicio'"));
            Assert.Equal(1L, Escalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_CodigosDirpf_Ficha_TipoAtivo_Variante_VigenciaInicio'"));
            Assert.Equal("42.50", EscalarTexto(connection, "SELECT Valor FROM Transacoes WHERE Descricao = 'Mercado'"));

            var semente = atual.CodigosDirpf.Single(codigo =>
                codigo.Ficha == TipoFichaDirpf.Bem && codigo.TipoAtivo == TipoAtivo.Acao && codigo.Variante == "");
            atual.CodigosDirpf.Add(new CodigoDirpf
            {
                Ficha = semente.Ficha,
                TipoAtivo = semente.TipoAtivo,
                Grupo = semente.Grupo,
                Codigo = "99",
                Descricao = "repetida",
                VigenciaInicio = semente.VigenciaInicio,
                EhSistema = false
            });
            Assert.Throws<DbUpdateException>(() => atual.SaveChanges());
            atual.ChangeTracker.Clear();

            atual.CodigosDirpf.Add(new CodigoDirpf
            {
                Ficha = TipoFichaDirpf.Bem,
                TipoAtivo = TipoAtivo.Acao,
                Grupo = "03",
                Codigo = "99",
                Descricao = "vigência nova",
                VigenciaInicio = new DateTime(2026, 1, 1),
                EhSistema = false
            });
            atual.SaveChanges();
            Assert.Equal(CatalogoCodigosDirpf.Linhas().Count + 1, atual.CodigosDirpf.Count());
        }

        using (var voltar = new AppDbContext(options))
        {
            voltar.Database.Migrate(Anterior);
            Assert.Equal(0L, Escalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'CodigosDirpf'"));
            Assert.Equal("42.50", EscalarTexto(connection, "SELECT Valor FROM Transacoes WHERE Descricao = 'Mercado'"));
        }
    }

    private static long Escalar(SqliteConnection connection, string sql)
    {
        using var comando = connection.CreateCommand();
        comando.CommandText = sql;
        return Convert.ToInt64(comando.ExecuteScalar());
    }

    private static string EscalarTexto(SqliteConnection connection, string sql)
    {
        using var comando = connection.CreateCommand();
        comando.CommandText = sql;
        return Convert.ToString(comando.ExecuteScalar()) ?? string.Empty;
    }
}
