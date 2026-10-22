using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Dirpf;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Tests.Dirpf;

public class AddCodigosDirpfLciLcaMigrationTests
{
    private const string Anterior = "20261022113200_AddDirpfRendaFixaJcp";

    [Fact]
    public void BancoComTituloOutro_GanhaCodigosIsentos_EVoltaComOOutroIntacto()
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
                INSERT INTO TitulosRendaFixa (Tipo, Codigo, Nome, Emissor, AnoVencimento, Corretora, Ativo, CriadoEm)
                VALUES (4, 'CRI1', 'CRI antigo', 'Banco', 2028, 'XP', 1, '2024-01-01 00:00:00');
                """);
            var outro = anterior.TitulosRendaFixa.Single();
            Assert.Equal(TipoTituloRendaFixa.Outro, outro.Tipo);
            Assert.Equal(4, (int)outro.Tipo);
            Assert.Equal("CRI1", outro.Codigo);
            Assert.Equal("42.50", EscalarTexto(connection, "SELECT Valor FROM Transacoes WHERE Descricao = 'Mercado'"));
            Assert.Equal(11, anterior.CodigosDirpf.Count());
            Assert.DoesNotContain(anterior.CodigosDirpf, codigo => codigo.Variante == CatalogoCodigosDirpf.VarianteRendaFixaIsenta);
        }

        using (var atual = new AppDbContext(options))
        {
            atual.Database.Migrate();

            Assert.Equal(CatalogoCodigosDirpf.Linhas().Count, atual.CodigosDirpf.Count());
            var rendimento = atual.CodigosDirpf.Single(codigo =>
                codigo.Ficha == TipoFichaDirpf.RendimentoIsento
                && codigo.Variante == CatalogoCodigosDirpf.VarianteRendaFixaIsenta);
            Assert.Equal("12", rendimento.Codigo);
            Assert.Equal(CatalogoCodigosDirpf.DescricaoRendimentoRendaFixaIsenta, rendimento.Descricao);
            Assert.Equal(new DateTime(2021, 1, 1), rendimento.VigenciaInicio);
            Assert.Null(rendimento.VigenciaFim);
            Assert.True(rendimento.EhSistema);
            var bem = atual.CodigosDirpf.Single(codigo =>
                codigo.Ficha == TipoFichaDirpf.Bem
                && codigo.Variante == CatalogoCodigosDirpf.VarianteRendaFixaIsenta);
            Assert.Equal("04", bem.Grupo);
            Assert.Equal("03", bem.Codigo);
            Assert.Equal(CatalogoCodigosDirpf.DescricaoBemRendaFixaIsenta, bem.Descricao);
            var tributado = atual.CodigosDirpf.Single(codigo =>
                codigo.Ficha == TipoFichaDirpf.Bem && codigo.Variante == CatalogoCodigosDirpf.VarianteRendaFixa);
            Assert.Equal("02", tributado.Codigo);

            var outro = atual.TitulosRendaFixa.Single();
            Assert.Equal(TipoTituloRendaFixa.Outro, outro.Tipo);
            Assert.Equal(4, (int)outro.Tipo);
            Assert.Equal("CRI antigo", outro.Nome);
            Assert.Equal("Banco", outro.Emissor);
            Assert.Equal(2028, outro.AnoVencimento);
            Assert.Equal("XP", outro.Corretora);
            Assert.True(outro.Ativo);
            Assert.Equal("42.50", EscalarTexto(connection, "SELECT Valor FROM Transacoes WHERE Descricao = 'Mercado'"));
            Assert.Equal(4L, Escalar(connection, "SELECT Tipo FROM TitulosRendaFixa WHERE Codigo = 'CRI1'"));
        }

        using (var voltar = new AppDbContext(options))
        {
            voltar.Database.Migrate(Anterior);
            Assert.Equal(11, voltar.CodigosDirpf.Count());
            Assert.DoesNotContain(voltar.CodigosDirpf, codigo => codigo.Variante == CatalogoCodigosDirpf.VarianteRendaFixaIsenta);
            var outro = voltar.TitulosRendaFixa.Single();
            Assert.Equal(TipoTituloRendaFixa.Outro, outro.Tipo);
            Assert.Equal("CRI1", outro.Codigo);
            Assert.Equal("CRI antigo", outro.Nome);
            Assert.Equal(4L, Escalar(connection, "SELECT Tipo FROM TitulosRendaFixa WHERE Codigo = 'CRI1'"));
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
