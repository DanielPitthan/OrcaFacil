using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Tests.Investimentos;

public class AddRendaFixaMigrationTests
{
    private const string Anterior = "20261017172616_AddIndiceHashImportacaoItemFatura";

    [Fact]
    public void BancoComDados_SobeAMigration_PreservaLancamentosEAceitaDecimalComoTexto()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;

        using (var anterior = new AppDbContext(options))
        {
            anterior.Database.Migrate(Anterior);
            anterior.Database.ExecuteSqlRaw(
                """
                INSERT INTO Categorias (Nome, Icone, CorHex, TipoPadrao, EhPadraoDoSistema, PermiteItensFatura, Ativo)
                VALUES ('Alimentação', 'A', '#9fb4c7', 1, 1, 0, 1);
                INSERT INTO MembrosFamilia (Nome, Avatar, Ativo, CriadoEm, Role)
                VALUES ('Daniel', 'D', 1, '2026-02-01 00:00:00', 0);
                INSERT INTO Cartoes (Nome, Bandeira, UltimosDigitos, Tipo, Ativo, CriadoEm)
                VALUES ('Nubank', 'Mastercard', '0012', 0, 1, '2026-02-01 00:00:00');
                INSERT INTO Transacoes (Descricao, Valor, Data, Tipo, Recorrencia, Pago, Observacao, CategoriaId, MembroFamiliaId, CartaoId, CriadoEm)
                VALUES ('Mercado', '42.50', '2026-02-03 00:00:00', 1, 0, 1, 'na feira', 1, 1, 1, '2026-02-03 00:00:00');
                INSERT INTO ItensLancamento (Descricao, Descritivo, Valor, EhEstorno, Natureza, Ordem, TransacaoId, CategoriaId)
                VALUES ('Supermercado', 'Hortifruti', '42.50', 1, 0, 0, 1, 1);
                """);
            Assert.Equal(0L, Escalar(connection, "SELECT COUNT(*) FROM pragma_table_info('Transacoes') WHERE name = 'OperacaoRendaFixaId'"));
            Assert.Equal(0L, Escalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'TitulosRendaFixa'"));
        }

        using (var atual = new AppDbContext(options))
        {
            atual.Database.Migrate();

            var transacao = Assert.Single(atual.Transacoes);
            Assert.Equal("Mercado", transacao.Descricao);
            Assert.Equal(42.50m, transacao.Valor);
            Assert.True(transacao.Pago);
            Assert.Equal("na feira", transacao.Observacao);
            Assert.Equal(1, transacao.CartaoId);
            Assert.Null(transacao.OperacaoRendaFixaId);

            var item = Assert.Single(atual.ItensLancamento);
            Assert.Equal("Supermercado", item.Descricao);
            Assert.Equal("Hortifruti", item.Descritivo);
            Assert.True(item.EhEstorno);
            Assert.Equal(42.50m, item.Valor);
            Assert.Equal(transacao.Id, item.TransacaoId);

            Assert.Empty(atual.TitulosRendaFixa);
            Assert.Empty(atual.OperacoesRendaFixa);
            Assert.Equal("TEXT", EscalarTexto(connection, "SELECT type FROM pragma_table_info('OperacoesRendaFixa') WHERE name = 'Quantidade'"));
            Assert.Equal("TEXT", EscalarTexto(connection, "SELECT type FROM pragma_table_info('OperacoesRendaFixa') WHERE name = 'Valor'"));
            Assert.Equal(1L, Escalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_TitulosRendaFixa_Codigo_Corretora'"));
            Assert.Equal(1L, Escalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_OperacoesRendaFixa_HashImportacao'"));
            Assert.Equal(1L, Escalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_Transacoes_OperacaoRendaFixaId'"));

            var titulo = new TituloRendaFixa
            {
                Tipo = TipoTituloRendaFixa.CDB,
                Codigo = "CDB9268YCA7",
                Nome = "CDB Banco X",
                Emissor = "Banco X",
                AnoVencimento = 2029,
                Corretora = "XP"
            };
            atual.TitulosRendaFixa.Add(titulo);
            atual.SaveChanges();
            atual.OperacoesRendaFixa.Add(new OperacaoRendaFixa
            {
                TituloRendaFixaId = titulo.Id,
                Data = new DateTime(2026, 4, 10),
                Tipo = TipoOperacaoRendaFixa.Aplicacao,
                Quantidade = 0.06m,
                PrecoUnitario = 1.12345678m,
                Valor = 12.34m,
                Origem = OrigemOperacaoRendaFixa.Manual
            });
            atual.SaveChanges();
        }

        using (var conferir = new AppDbContext(options))
        {
            var gravada = Assert.Single(conferir.OperacoesRendaFixa);
            Assert.Equal(0.06m, gravada.Quantidade);
            Assert.Equal(1.12345678m, gravada.PrecoUnitario);
            Assert.Equal(12.34m, gravada.Valor);
            Assert.Equal(0m, gravada.Custos);
            Assert.Equal(0m, gravada.IrRetido);
        }

        Assert.Equal("text", EscalarTexto(connection, "SELECT typeof(Quantidade) FROM OperacoesRendaFixa"));
        Assert.Equal("0.06", EscalarTexto(connection, "SELECT Quantidade FROM OperacoesRendaFixa"));
        Assert.Equal("1.12345678", EscalarTexto(connection, "SELECT PrecoUnitario FROM OperacoesRendaFixa"));

        using (var voltar = new AppDbContext(options))
        {
            voltar.Database.Migrate(Anterior);
            Assert.Equal(0L, Escalar(connection, "SELECT COUNT(*) FROM pragma_table_info('Transacoes') WHERE name = 'OperacaoRendaFixaId'"));
            Assert.Equal(0L, Escalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'TitulosRendaFixa'"));
            Assert.Equal(0L, Escalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'OperacoesRendaFixa'"));
            Assert.Equal("Mercado", EscalarTexto(connection, "SELECT Descricao FROM Transacoes"));
            Assert.Equal("42.50", EscalarTexto(connection, "SELECT Valor FROM Transacoes"));
            Assert.Equal("Hortifruti", EscalarTexto(connection, "SELECT Descritivo FROM ItensLancamento"));
            Assert.Equal(1L, Escalar(connection, "SELECT CartaoId FROM Transacoes"));
        }

        using var deNovo = new AppDbContext(options);
        deNovo.Database.Migrate();
        var preservada = Assert.Single(deNovo.Transacoes);
        Assert.Equal("Mercado", preservada.Descricao);
        Assert.Equal(42.50m, preservada.Valor);
        Assert.Null(preservada.OperacaoRendaFixaId);
        Assert.Equal("Hortifruti", Assert.Single(deNovo.ItensLancamento).Descritivo);
        Assert.Empty(deNovo.OperacoesRendaFixa);
    }

    [Fact]
    public void Indices_Unicos_RecusamCodigoRepetidoEHashRepetido_EPermitemNulos()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;

        using var atual = new AppDbContext(options);
        atual.Database.Migrate();
        var membro = new MembroFamilia { Nome = "Daniel" };
        var categoria = new Categoria { Nome = "Mercado", Icone = "M", CorHex = "#112233", TipoPadrao = TipoTransacao.Despesa };
        atual.AddRange(membro, categoria);
        atual.SaveChanges();

        var titulo = new TituloRendaFixa
        {
            Tipo = TipoTituloRendaFixa.TesouroSelic,
            Codigo = "Tesouro Selic 2029",
            Nome = "Selic 2029",
            Corretora = "XP"
        };
        atual.TitulosRendaFixa.Add(titulo);
        atual.SaveChanges();

        atual.TitulosRendaFixa.Add(new TituloRendaFixa
        {
            Tipo = TipoTituloRendaFixa.TesouroSelic,
            Codigo = "tesouro selic 2029",
            Nome = "Outro",
            Corretora = "xp"
        });
        Assert.Throws<DbUpdateException>(() => atual.SaveChanges());
        atual.ChangeTracker.Clear();

        var mesmoCodigo = new TituloRendaFixa
        {
            Tipo = TipoTituloRendaFixa.TesouroSelic,
            Codigo = "Tesouro Selic 2029",
            Nome = "Na Clear",
            Corretora = "Clear"
        };
        atual.TitulosRendaFixa.Add(mesmoCodigo);
        atual.SaveChanges();

        var operacao = new OperacaoRendaFixa
        {
            TituloRendaFixaId = titulo.Id,
            Data = new DateTime(2026, 5, 2),
            Tipo = TipoOperacaoRendaFixa.Resgate,
            Quantidade = 0.06m,
            Valor = 10m,
            IrRetido = 1.50m,
            Origem = OrigemOperacaoRendaFixa.ExtratoB3,
            HashImportacao = new string('A', 64)
        };
        var manual = new OperacaoRendaFixa
        {
            TituloRendaFixaId = mesmoCodigo.Id,
            Data = new DateTime(2026, 5, 2),
            Tipo = TipoOperacaoRendaFixa.Resgate,
            Quantidade = 0.06m,
            Valor = 10m,
            Origem = OrigemOperacaoRendaFixa.Manual
        };
        atual.OperacoesRendaFixa.AddRange(operacao, manual);
        atual.SaveChanges();

        atual.OperacoesRendaFixa.Add(new OperacaoRendaFixa
        {
            TituloRendaFixaId = titulo.Id,
            Data = new DateTime(2026, 5, 3),
            Tipo = TipoOperacaoRendaFixa.Aplicacao,
            Quantidade = 1m,
            Valor = 10m,
            Origem = OrigemOperacaoRendaFixa.Manual
        });
        atual.SaveChanges();
        Assert.Equal(2, atual.OperacoesRendaFixa.Count(o => o.HashImportacao == null));

        atual.ChangeTracker.Clear();
        var repetida = new OperacaoRendaFixa
        {
            TituloRendaFixaId = titulo.Id,
            Data = new DateTime(2026, 5, 4),
            Tipo = TipoOperacaoRendaFixa.Aplicacao,
            Quantidade = 1m,
            Valor = 10m,
            Origem = OrigemOperacaoRendaFixa.ExtratoB3,
            HashImportacao = new string('A', 64)
        };
        atual.OperacoesRendaFixa.Add(repetida);
        Assert.Throws<DbUpdateException>(() => atual.SaveChanges());
        atual.ChangeTracker.Clear();

        var primeira = atual.Transacoes.Add(new Transacao
        {
            Descricao = "Resgate",
            Valor = 8.50m,
            Data = new DateTime(2026, 5, 2),
            Tipo = TipoTransacao.Receita,
            CategoriaId = categoria.Id,
            MembroFamiliaId = membro.Id,
            OperacaoRendaFixaId = operacao.Id
        });
        atual.SaveChanges();
        atual.Transacoes.Add(new Transacao
        {
            Descricao = "Outro",
            Valor = 1m,
            Data = new DateTime(2026, 5, 2),
            Tipo = TipoTransacao.Receita,
            CategoriaId = categoria.Id,
            MembroFamiliaId = membro.Id,
            OperacaoRendaFixaId = operacao.Id
        });
        Assert.Throws<DbUpdateException>(() => atual.SaveChanges());
        atual.ChangeTracker.Clear();

        atual.Transacoes.Add(new Transacao
        {
            Descricao = "Sem vínculo",
            Valor = 3m,
            Data = new DateTime(2026, 5, 2),
            Tipo = TipoTransacao.Despesa,
            CategoriaId = categoria.Id,
            MembroFamiliaId = membro.Id
        });
        atual.SaveChanges();
        Assert.Equal(1, atual.Transacoes.Count(t => t.OperacaoRendaFixaId == null));
        Assert.Equal(operacao.Id, atual.Transacoes.Single(t => t.Id == primeira.Entity.Id).OperacaoRendaFixaId);
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
