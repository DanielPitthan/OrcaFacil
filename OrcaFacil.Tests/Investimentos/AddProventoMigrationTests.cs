using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Tests.Investimentos;

public class AddProventoMigrationTests
{
    private const string Anterior = "20261020172344_AddResultadoRealizadoVariavel";

    [Fact]
    public void BancoComLancamento_SobeAMigration_HashNuloNaoColide_HashRepetidoRecusa_EVolta()
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
                INSERT INTO AtivosFinanceiros (Ticker, Nome, Tipo, Ativo, CriadoEm)
                VALUES ('PETR4', 'Petrobras', 0, 1, '2026-01-15 00:00:00');
                INSERT INTO Transacoes (Descricao, Valor, Data, Tipo, Recorrencia, Pago, CategoriaId, MembroFamiliaId, CriadoEm)
                VALUES ('Mercado', '42.50', '2026-02-03 00:00:00', 1, 0, 1, 1, 1, '2026-02-03 00:00:00');
                """);
            Assert.Equal(0L, Escalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Proventos'"));
            Assert.Equal(0L, Escalar(connection, "SELECT COUNT(*) FROM pragma_table_info('Transacoes') WHERE name = 'ProventoId'"));
        }

        using (var atual = new AppDbContext(options))
        {
            atual.Database.Migrate();

            var mercado = atual.Transacoes.Single(t => t.Descricao == "Mercado");
            Assert.Null(mercado.ProventoId);
            Assert.Equal(42.50m, mercado.Valor);
            Assert.Empty(atual.Proventos);
            Assert.Equal(1L, Escalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_Proventos_HashImportacao'"));
            Assert.Equal(1L, Escalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_Transacoes_ProventoId'"));

            var ativo = atual.AtivosFinanceiros.Single();
            var membro = atual.MembrosFamilia.Single();
            atual.Proventos.Add(new Provento
            {
                AtivoId = ativo.Id,
                MembroFamiliaId = membro.Id,
                Tipo = TipoProvento.Rendimento,
                DataPagamento = new DateTime(2026, 3, 15),
                ValorLiquido = 10m,
                Origem = OrigemProvento.Manual
            });
            atual.SaveChanges();
            atual.Proventos.Add(new Provento
            {
                AtivoId = ativo.Id,
                MembroFamiliaId = membro.Id,
                Tipo = TipoProvento.Rendimento,
                DataPagamento = new DateTime(2026, 3, 15),
                ValorLiquido = 10m,
                Origem = OrigemProvento.Manual
            });
            atual.SaveChanges();
            Assert.Equal(2, atual.Proventos.Count(p => p.HashImportacao == null));

            var importado = new Provento
            {
                AtivoId = ativo.Id,
                MembroFamiliaId = membro.Id,
                Tipo = TipoProvento.Jcp,
                DataPagamento = new DateTime(2026, 4, 2),
                ValorLiquido = 4m,
                Origem = OrigemProvento.ExtratoB3,
                HashImportacao = new string('C', 64)
            };
            atual.Proventos.Add(importado);
            atual.SaveChanges();

            atual.Proventos.Add(new Provento
            {
                AtivoId = ativo.Id,
                MembroFamiliaId = membro.Id,
                Tipo = TipoProvento.Jcp,
                DataPagamento = new DateTime(2026, 4, 3),
                ValorLiquido = 4m,
                Origem = OrigemProvento.ExtratoB3,
                HashImportacao = new string('C', 64)
            });
            Assert.Throws<DbUpdateException>(() => atual.SaveChanges());
            atual.ChangeTracker.Clear();

            var categoria = atual.Categorias.Single();
            var proventoId = atual.Proventos.Single(p => p.HashImportacao != null).Id;
            atual.Transacoes.Add(new Transacao
            {
                Descricao = "JCP",
                Valor = 4m,
                Data = new DateTime(2026, 4, 2),
                Tipo = TipoTransacao.Receita,
                CategoriaId = categoria.Id,
                MembroFamiliaId = membro.Id,
                ProventoId = proventoId
            });
            atual.SaveChanges();
            atual.Transacoes.Add(new Transacao
            {
                Descricao = "Outro JCP",
                Valor = 1m,
                Data = new DateTime(2026, 4, 2),
                Tipo = TipoTransacao.Receita,
                CategoriaId = categoria.Id,
                MembroFamiliaId = membro.Id,
                ProventoId = proventoId
            });
            Assert.Throws<DbUpdateException>(() => atual.SaveChanges());
            atual.ChangeTracker.Clear();
            Assert.Equal(1, atual.Transacoes.Count(t => t.ProventoId == proventoId));
            Assert.Equal("Mercado", atual.Transacoes.Single(t => t.ProventoId == null).Descricao);
        }

        using (var voltar = new AppDbContext(options))
        {
            voltar.Database.Migrate(Anterior);
            Assert.Equal(0L, Escalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Proventos'"));
            Assert.Equal(0L, Escalar(connection, "SELECT COUNT(*) FROM pragma_table_info('Transacoes') WHERE name = 'ProventoId'"));
            Assert.Equal("42.50", EscalarTexto(connection, "SELECT Valor FROM Transacoes WHERE Descricao = 'Mercado'"));
            Assert.Equal(1L, Escalar(connection, "SELECT COUNT(*) FROM Transacoes WHERE Descricao = 'Mercado'"));
            Assert.Equal(4m, DecimalDe(connection, "SELECT Valor FROM Transacoes WHERE Descricao = 'JCP'"));
            Assert.Equal(1L, Escalar(connection, "SELECT COUNT(*) FROM Transacoes WHERE Descricao = 'JCP'"));
            Assert.Equal(0L, Escalar(connection, "SELECT COUNT(*) FROM Transacoes WHERE Descricao = 'Outro JCP'"));
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

    private static decimal DecimalDe(SqliteConnection connection, string sql)
        => decimal.Parse(EscalarTexto(connection, sql), CultureInfo.InvariantCulture);
}
