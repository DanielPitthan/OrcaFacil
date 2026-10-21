using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Dirpf;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Tests.Dirpf;

public class AddDirpfRendaFixaJcpMigrationTests
{
    private const string Anterior = "20261021101911_AddApuracaoIrMensal";

    [Fact]
    public void BancoComLancamento_GanhaJcpERendaFixa_EVoltaSemApagarOLancamento()
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
            Assert.Equal(8, anterior.CodigosDirpf.Count());
            Assert.DoesNotContain(anterior.CodigosDirpf, codigo => codigo.Variante == CatalogoCodigosDirpf.VarianteJcp);
            Assert.DoesNotContain(anterior.RegrasFiscais, regra => regra.Codigo == CodigosRegraFiscal.IrRegressivoAutomatico);
        }

        using (var atual = new AppDbContext(options))
        {
            atual.Database.Migrate();

            Assert.Equal(CatalogoCodigosDirpf.Linhas().Count, atual.CodigosDirpf.Count());
            Assert.Equal(CatalogoRegrasFiscais.Linhas().Count, atual.RegrasFiscais.Count());
            var jcp = atual.CodigosDirpf.Single(codigo => codigo.Variante == CatalogoCodigosDirpf.VarianteJcp);
            Assert.Equal(TipoFichaDirpf.RendimentoExclusivo, jcp.Ficha);
            Assert.Equal("10", jcp.Codigo);
            Assert.True(jcp.EhSistema);
            var bem = atual.CodigosDirpf.Single(codigo =>
                codigo.Ficha == TipoFichaDirpf.Bem && codigo.Variante == CatalogoCodigosDirpf.VarianteRendaFixa);
            Assert.Equal("04", bem.Grupo);
            Assert.Equal("02", bem.Codigo);
            Assert.Equal(0m, atual.RegrasFiscais.Single(regra => regra.Codigo == CodigosRegraFiscal.JcpGrossUp).Valor);
            Assert.Equal("42.50", EscalarTexto(connection, "SELECT Valor FROM Transacoes WHERE Descricao = 'Mercado'"));
        }

        using (var voltar = new AppDbContext(options))
        {
            voltar.Database.Migrate(Anterior);
            Assert.Equal(8, voltar.CodigosDirpf.Count());
            Assert.DoesNotContain(voltar.RegrasFiscais, regra => regra.Codigo == CodigosRegraFiscal.JcpGrossUp);
            Assert.Equal("42.50", EscalarTexto(connection, "SELECT Valor FROM Transacoes WHERE Descricao = 'Mercado'"));
        }
    }

    private static string EscalarTexto(SqliteConnection connection, string sql)
    {
        using var comando = connection.CreateCommand();
        comando.CommandText = sql;
        return Convert.ToString(comando.ExecuteScalar()) ?? string.Empty;
    }
}
