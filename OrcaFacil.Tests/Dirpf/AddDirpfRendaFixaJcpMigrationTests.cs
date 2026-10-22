using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Dirpf;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Tests.Dirpf;

public class AddDirpfRendaFixaJcpMigrationTests
{
    private const string Anterior = "20261021222000_AddIrrfInformado";

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
                INSERT INTO AtivosFinanceiros (Ticker, Nome, Tipo, Ativo, CriadoEm)
                VALUES ('PETR4', 'Petrobras', 0, 1, '2024-01-01 00:00:00');
                INSERT INTO OperacoesAtivo
                    (AtivoId, MembroFamiliaId, Tipo, Data, DataPregao, Quantidade, PrecoUnitario, ValorOperacao, Custos, CriadoEm, Origem)
                VALUES
                    (1, 1, 0, '2024-02-02 00:00:00', '2024-01-31 00:00:00', '1', '10', '10', '0', '2024-02-02 00:00:00', 0);
                INSERT INTO IrrfInformados (MembroFamiliaId, Ano, Mes, Cesta, Valor)
                VALUES (1, 2024, 1, 0, '3.50');
                """);
            Assert.Equal(3.50m, anterior.IrrfInformados.Single().Valor);
            Assert.Equal(new DateTime(2024, 1, 31), anterior.OperacoesAtivo.Single().DataPregao);
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
            Assert.Equal(3.50m, atual.IrrfInformados.Single().Valor);
            Assert.Equal(new DateTime(2024, 1, 31), atual.OperacoesAtivo.Single().DataPregao);
        }

        using (var voltar = new AppDbContext(options))
        {
            voltar.Database.Migrate(Anterior);
            Assert.Equal(8, voltar.CodigosDirpf.Count());
            Assert.DoesNotContain(voltar.RegrasFiscais, regra => regra.Codigo == CodigosRegraFiscal.JcpGrossUp);
            Assert.Equal("42.50", EscalarTexto(connection, "SELECT Valor FROM Transacoes WHERE Descricao = 'Mercado'"));
            Assert.Equal(3.50m, voltar.IrrfInformados.Single().Valor);
            Assert.Equal(new DateTime(2024, 1, 31), voltar.OperacoesAtivo.Single().DataPregao);
        }
    }

    private static string EscalarTexto(SqliteConnection connection, string sql)
    {
        using var comando = connection.CreateCommand();
        comando.CommandText = sql;
        return Convert.ToString(comando.ExecuteScalar()) ?? string.Empty;
    }
}
