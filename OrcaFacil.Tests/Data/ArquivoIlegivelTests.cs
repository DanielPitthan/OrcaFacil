using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;

namespace OrcaFacil.Tests.Data;

public class ArquivoIlegivelTests
{
    [Fact]
    public async Task ArquivoIlegivel_ViraRelatorio_ELogaTabelasFaltasEMigration()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "orcafacil-ilegivel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        var caminho = Path.Combine(pasta, "orcafacil.db");
        await File.WriteAllTextAsync(caminho, "isto nao e sqlite");

        try
        {
            var opcoes = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={caminho}")
                .Options;
            await using var ctx = new AppDbContext(opcoes);

            var resultado = await PreparoBanco.MigrarEVerificarAsync(
                ctx,
                new OpcoesPreparoBanco { VersaoAplicativo = "1.0.0" });

            Assert.True(resultado.ExigeRecuperacao);
            Assert.False(resultado.Relatorio.Integro);
            Assert.Equal(PreparoBanco.AvisoArquivoIlegivel, resultado.Aviso);
            Assert.NotNull(resultado.FalhaMigrate);
            Assert.Contains("MembrosFamilia", resultado.Relatorio.TabelasFaltando);
            Assert.Contains("MembrosFamilia.Nome", resultado.Relatorio.ColunasFaltando);
            Assert.Contains("20260826112311_InitialCreate", resultado.Relatorio.MigrationsAusentesNoHistorico);
            Assert.Equal("20260826112311_InitialCreate", resultado.Relatorio.PrimeiraMigrationNaoRegistrada);
            Assert.Null(resultado.CaminhoBackup);

            var log = new ColetorLog();
            RegistroPreparoBanco.Registrar(resultado, log);

            var erro = Assert.Single(log.Erros);
            Assert.Contains("Faltam tabelas", erro.Mensagem);
            Assert.Contains("Faltam colunas", erro.Mensagem);
            Assert.Contains("Faltam índices", erro.Mensagem);
            Assert.Contains("Histórico sem", erro.Mensagem);
            Assert.Contains("Próxima migration não registrada", erro.Mensagem);
            Assert.Contains(erro.Argumentos, argumento => Texto(argumento).Contains("MembrosFamilia"));
            Assert.Contains(erro.Argumentos, argumento => Texto(argumento).Contains("MembrosFamilia.Nome"));
            Assert.Contains(erro.Argumentos, argumento => Texto(argumento).Contains("20260826112311_InitialCreate"));
            Assert.Same(resultado.FalhaMigrate, erro.Excecao);
            Assert.DoesNotContain("duplicate column", erro.Excecao!.Message);

            var excecao = await Assert.ThrowsAsync<IntegridadeBancoException>(() => PreparoBanco.ExigirIntegroAsync(ctx));
            Assert.Equal(PreparoBanco.AvisoArquivoIlegivel, excecao.Message);
            Assert.IsAssignableFrom<IntegridadeBancoException>(excecao);
            Assert.Equal("isto nao e sqlite", await File.ReadAllTextAsync(caminho));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(pasta))
                Directory.Delete(pasta, recursive: true);
        }
    }

    private static string Texto(object? valor) => valor?.ToString() ?? "";

    private sealed class ColetorLog : IRegistroLog
    {
        public List<Entrada> Erros { get; } = [];

        public void Informacao(string mensagem, object?[] argumentos)
        {
        }

        public void Erro(Exception? excecao, string mensagem, object?[] argumentos) =>
            Erros.Add(new Entrada(excecao, mensagem, argumentos));
    }

    private sealed record Entrada(Exception? Excecao, string Mensagem, object?[] Argumentos);
}
