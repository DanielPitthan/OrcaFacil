using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Tests.Investimentos;

public class AddResultadoRealizadoMigrationTests
{
    private const string Anterior = "20261020135920_AddRegraCatalogoComprovante";

    [Fact]
    public void BancoComOperacao_SobeAMigration_HashNuloNaoColide_HashRepetidoRecusa_EVolta()
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
                INSERT INTO OperacoesAtivo (AtivoId, MembroFamiliaId, Tipo, Data, Quantidade, PrecoUnitario, Custos, Corretora, CriadoEm)
                VALUES (1, 1, 0, '2026-03-10 00:00:00', '100', '10.5', '1.25', 'XP', '2026-03-10 00:00:00');
                INSERT INTO Transacoes (Descricao, Valor, Data, Tipo, Recorrencia, Pago, CategoriaId, MembroFamiliaId, CriadoEm)
                VALUES ('Mercado', '42.50', '2026-02-03 00:00:00', 1, 0, 1, 1, 1, '2026-02-03 00:00:00');
                """);
            Assert.Equal(0L, Escalar(connection, "SELECT COUNT(*) FROM pragma_table_info('OperacoesAtivo') WHERE name = 'HashImportacao'"));
            Assert.Equal(0L, Escalar(connection, "SELECT COUNT(*) FROM pragma_table_info('OperacoesAtivo') WHERE name = 'Origem'"));
            Assert.Equal(0L, Escalar(connection, "SELECT COUNT(*) FROM pragma_table_info('Transacoes') WHERE name = 'OperacaoAtivoId'"));
        }

        using (var atual = new AppDbContext(options))
        {
            atual.Database.Migrate();

            var operacao = Assert.Single(atual.OperacoesAtivo);
            Assert.Equal(OrigemOperacaoAtivo.Manual, operacao.Origem);
            Assert.Null(operacao.HashImportacao);
            Assert.Equal(100m, operacao.Quantidade);
            Assert.Equal(10.5m, operacao.PrecoUnitario);
            Assert.Equal(1050m, operacao.ValorOperacao);
            Assert.Equal("XP", operacao.Corretora);

            var mercado = atual.Transacoes.Single(t => t.Descricao == "Mercado");
            Assert.Null(mercado.OperacaoAtivoId);
            Assert.Equal(42.50m, mercado.Valor);
            Assert.Equal(6, atual.RegrasFiscais.Count());
            Assert.All(atual.RegrasFiscais, regra => Assert.True(regra.EhSistema));
            Assert.Equal(1L, Escalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_OperacoesAtivo_HashImportacao'"));
            Assert.Equal(1L, Escalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_Transacoes_OperacaoAtivoId'"));

            atual.OperacoesAtivo.Add(new OperacaoAtivo
            {
                AtivoId = operacao.AtivoId,
                MembroFamiliaId = operacao.MembroFamiliaId,
                Tipo = TipoOperacao.Compra,
                Data = new DateTime(2026, 4, 1),
                Quantidade = 10m,
                PrecoUnitario = 11m,
                Origem = OrigemOperacaoAtivo.Manual
            });
            atual.SaveChanges();
            Assert.Equal(2, atual.OperacoesAtivo.Count(o => o.HashImportacao == null));

            var importada = new OperacaoAtivo
            {
                AtivoId = operacao.AtivoId,
                MembroFamiliaId = operacao.MembroFamiliaId,
                Tipo = TipoOperacao.Compra,
                Data = new DateTime(2026, 4, 2),
                Quantidade = 1m,
                PrecoUnitario = 11m,
                Origem = OrigemOperacaoAtivo.ExtratoB3,
                HashImportacao = new string('B', 64)
            };
            atual.OperacoesAtivo.Add(importada);
            atual.SaveChanges();

            atual.OperacoesAtivo.Add(new OperacaoAtivo
            {
                AtivoId = operacao.AtivoId,
                MembroFamiliaId = operacao.MembroFamiliaId,
                Tipo = TipoOperacao.Compra,
                Data = new DateTime(2026, 4, 3),
                Quantidade = 1m,
                PrecoUnitario = 11m,
                Origem = OrigemOperacaoAtivo.ExtratoB3,
                HashImportacao = new string('B', 64)
            });
            Assert.Throws<DbUpdateException>(() => atual.SaveChanges());
            atual.ChangeTracker.Clear();

            var categoria = atual.Categorias.Single();
            var membro = atual.MembrosFamilia.Single();
            var importadaId = atual.OperacoesAtivo.Single(o => o.HashImportacao != null).Id;
            atual.Transacoes.Add(new Transacao
            {
                Descricao = "Ganho",
                Valor = 10m,
                Data = new DateTime(2026, 4, 2),
                Tipo = TipoTransacao.Receita,
                CategoriaId = categoria.Id,
                MembroFamiliaId = membro.Id,
                OperacaoAtivoId = importadaId
            });
            atual.SaveChanges();
            atual.Transacoes.Add(new Transacao
            {
                Descricao = "Outro ganho",
                Valor = 1m,
                Data = new DateTime(2026, 4, 2),
                Tipo = TipoTransacao.Receita,
                CategoriaId = categoria.Id,
                MembroFamiliaId = membro.Id,
                OperacaoAtivoId = importadaId
            });
            Assert.Throws<DbUpdateException>(() => atual.SaveChanges());
            atual.ChangeTracker.Clear();
            Assert.Equal(1, atual.Transacoes.Count(t => t.OperacaoAtivoId == importadaId));
        }

        using (var voltar = new AppDbContext(options))
        {
            voltar.Database.Migrate(Anterior);
            Assert.Equal(0L, Escalar(connection, "SELECT COUNT(*) FROM pragma_table_info('OperacoesAtivo') WHERE name = 'HashImportacao'"));
            Assert.Equal(0L, Escalar(connection, "SELECT COUNT(*) FROM pragma_table_info('OperacoesAtivo') WHERE name = 'Origem'"));
            Assert.Equal(0L, Escalar(connection, "SELECT COUNT(*) FROM pragma_table_info('OperacoesAtivo') WHERE name = 'ValorOperacao'"));
            Assert.Equal(0L, Escalar(connection, "SELECT COUNT(*) FROM pragma_table_info('Transacoes') WHERE name = 'OperacaoAtivoId'"));
            Assert.Equal(0L, Escalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'RegrasFiscais'"));
            Assert.Equal("100", EscalarTexto(connection, "SELECT Quantidade FROM OperacoesAtivo WHERE Corretora = 'XP'"));
            Assert.Equal("Mercado", EscalarTexto(connection, "SELECT Descricao FROM Transacoes"));
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
