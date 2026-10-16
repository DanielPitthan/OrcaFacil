using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;

namespace OrcaFacil.Tests.Lancamentos;

public class AddDescritivoItemFaturaMigrationTests
{
    [Fact]
    public void Migracao_AdicionaDescritivoSemApagarItensRepetidos()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var anterior = new AppDbContext(options))
        {
            anterior.Database.Migrate("20261015194450_AddCartoes");
            anterior.Database.ExecuteSqlRaw(
                """
                INSERT INTO Categorias (Nome, Icone, CorHex, TipoPadrao, EhPadraoDoSistema, PermiteItensFatura, Ativo)
                VALUES ('Cartão de Crédito', '💳', '#B2CFE6', 1, 1, 1, 1),
                       ('Alimentação', '🛒', '#B2CFE6', 1, 0, 0, 1);
                INSERT INTO MembrosFamilia (Nome, Avatar, Ativo, CriadoEm, Role)
                VALUES ('Daniel', '👤', 1, '2026-10-01 00:00:00', 0);
                INSERT INTO Transacoes (Descricao, Valor, Data, Tipo, Recorrencia, Pago, CategoriaId, MembroFamiliaId, CriadoEm)
                VALUES ('Fatura Nubank', '80', '2026-10-18 00:00:00', 1, 0, 0, 1, 1, '2026-10-18 00:00:00');
                INSERT INTO ItensLancamento (Descricao, Valor, Natureza, Ordem, TransacaoId, CategoriaId)
                VALUES ('Alimentação', '40', 0, 0, 1, 2),
                       ('Alimentação', '40', 0, 1, 1, 2);
                """);
        }

        using (var atual = new AppDbContext(options))
        {
            atual.Database.Migrate();

            var itens = atual.ItensLancamento.OrderBy(i => i.Ordem).ToList();
            Assert.Equal(2, itens.Count);
            Assert.All(itens, item =>
            {
                Assert.Equal("Alimentação", item.Descricao);
                Assert.Equal(40m, item.Valor);
                Assert.Equal(2, item.CategoriaId);
                Assert.Null(item.Descritivo);
            });

            itens[0].Descritivo = "McDonald's";
            itens[1].Descritivo = "Big Steak";
            atual.SaveChanges();
        }

        using var conferido = new AppDbContext(options);
        var gravados = conferido.ItensLancamento.OrderBy(i => i.Ordem).ToList();
        Assert.Equal(["McDonald's", "Big Steak"], gravados.Select(i => i.Descritivo));
        Assert.Equal(80m, conferido.Transacoes.Single().Valor);
    }
}
