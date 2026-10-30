using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Dirpf;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Tests.Dirpf;

public class AddCodigoEtfRendaFixaDirpfMigrationTests
{
    private const string Anterior = "20261022145822_AddCodigosDirpfLciLca";

    [Fact]
    public void BancoNaMigrationAnterior_Ganha0708EExclusivo06_EVoltaSemEles()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;

        using (var anterior = new AppDbContext(options))
        {
            anterior.Database.Migrate(Anterior);
            Assert.Equal(13, anterior.CodigosDirpf.Count());
            Assert.DoesNotContain(anterior.CodigosDirpf, codigo => codigo.Variante == CatalogoCodigosDirpf.VarianteEtfRendaFixa);
        }

        using (var atual = new AppDbContext(options))
        {
            atual.Database.Migrate();
            Assert.Equal(CatalogoCodigosDirpf.Linhas().Count, atual.CodigosDirpf.Count());
            var bem = atual.CodigosDirpf.Single(codigo =>
                codigo.Ficha == TipoFichaDirpf.Bem && codigo.Variante == CatalogoCodigosDirpf.VarianteEtfRendaFixa);
            Assert.Equal("07", bem.Grupo);
            Assert.Equal("08", bem.Codigo);
            Assert.Equal(CatalogoCodigosDirpf.DescricaoBemEtfRendaFixa, bem.Descricao);
            Assert.Equal(TipoAtivo.Etf, bem.TipoAtivo);
            Assert.True(bem.EhSistema);
            Assert.Equal(new DateTime(2021, 1, 1), bem.VigenciaInicio);

            var rendimento = atual.CodigosDirpf.Single(codigo =>
                codigo.Ficha == TipoFichaDirpf.RendimentoExclusivo
                && codigo.Variante == CatalogoCodigosDirpf.VarianteEtfRendaFixa);
            Assert.Equal("06", rendimento.Codigo);
            Assert.Equal("", rendimento.Grupo);
            Assert.Equal(CatalogoCodigosDirpf.DescricaoRendimentoRendaFixa, rendimento.Descricao);
        }

        using (var voltar = new AppDbContext(options))
        {
            voltar.Database.Migrate(Anterior);
            Assert.Equal(13, voltar.CodigosDirpf.Count());
            Assert.DoesNotContain(voltar.CodigosDirpf, codigo => codigo.Variante == CatalogoCodigosDirpf.VarianteEtfRendaFixa);
        }
    }
}
