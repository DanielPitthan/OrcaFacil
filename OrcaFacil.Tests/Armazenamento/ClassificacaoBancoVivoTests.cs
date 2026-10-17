using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Armazenamento;
using OrcaFacil.Core.Data;

namespace OrcaFacil.Tests.Armazenamento;

public class ClassificacaoBancoVivoTests
{
    private const string Cartoes = "20261015194450_AddCartoes";

    [Fact]
    public void Ausente_NaoConsultaRelatorio()
    {
        Assert.Equal(SituacaoBancoVivo.Ausente, ClassificacaoBancoVivo.Classificar(LeituraBancoVivo.Ausente()));
    }

    [Fact]
    public void LeituraQueFalhou_EInconsistente()
    {
        Assert.Equal(SituacaoBancoVivo.Inconsistente, ClassificacaoBancoVivo.Classificar(LeituraBancoVivo.Ilegivel()));
    }

    [Fact]
    public void RelatorioIntegro_EIntegro()
    {
        var relatorio = new RelatorioIntegridadeBanco();
        Assert.True(relatorio.Integro);
        Assert.Equal(SituacaoBancoVivo.Integro, ClassificacaoBancoVivo.Classificar(LeituraBancoVivo.Lida(relatorio)));
    }

    [Fact]
    public void SoMigrationsConhecidasAusentes_EDesatualizado()
    {
        var relatorio = new RelatorioIntegridadeBanco
        {
            MigrationsAusentesNoHistorico = ["20261016011256_AddDescritivoItemFatura"],
            ColunasFaltando = ["ItensLancamento.Descritivo"]
        };

        Assert.False(relatorio.Integro);
        Assert.Equal(SituacaoBancoVivo.Desatualizado, ClassificacaoBancoVivo.Classificar(LeituraBancoVivo.Lida(relatorio)));
    }

    [Fact]
    public void MigrationPelaMetade_GanhaDeMigrationAusente()
    {
        var relatorio = new RelatorioIntegridadeBanco
        {
            MigrationsPelaMetade = ["20261015194450_AddCartoes"],
            MigrationsAusentesNoHistorico = ["20261015194450_AddCartoes", "20261016182243_AddEhEstornoItemFatura"]
        };

        Assert.Equal(SituacaoBancoVivo.Inconsistente, ClassificacaoBancoVivo.Classificar(LeituraBancoVivo.Lida(relatorio)));
    }

    [Fact]
    public void HistoricoComObjetoFaltando_EInconsistente()
    {
        var relatorio = new RelatorioIntegridadeBanco
        {
            MigrationsNoHistoricoComObjetoFaltando = ["20261008141717_PerfilFechamentoLogs"],
            TabelasFaltando = ["LogsEvento"]
        };

        Assert.Equal(SituacaoBancoVivo.Inconsistente, ClassificacaoBancoVivo.Classificar(LeituraBancoVivo.Lida(relatorio)));
    }

    [Fact]
    public void MigrationDesconhecida_GanhaDePendenteEDePelaMetade()
    {
        var relatorio = new RelatorioIntegridadeBanco
        {
            MigrationsDesconhecidasNoHistorico = ["20990101000000_Futura"],
            MigrationsAusentesNoHistorico = ["20261016182243_AddEhEstornoItemFatura"],
            MigrationsPelaMetade = ["20261015194450_AddCartoes"]
        };

        Assert.Equal(
            SituacaoBancoVivo.SchemaMaisNovoQueOApp,
            ClassificacaoBancoVivo.Classificar(LeituraBancoVivo.Lida(relatorio)));
    }

    [Fact]
    public void DivergenciaSemMigrationPendente_EInconsistente()
    {
        var relatorio = new RelatorioIntegridadeBanco
        {
            ColunasFaltando = ["MembrosFamilia.FotoPath"]
        };

        Assert.Equal(SituacaoBancoVivo.Inconsistente, ClassificacaoBancoVivo.Classificar(LeituraBancoVivo.Lida(relatorio)));
    }

    [Fact]
    public void LeituraIncoerente_Lanca()
    {
        Assert.Throws<ArgumentException>(() => new LeituraBancoVivo(false, new RelatorioIntegridadeBanco(), false));
        Assert.Throws<ArgumentException>(() => new LeituraBancoVivo(false, null, true));
        Assert.Throws<ArgumentException>(() => new LeituraBancoVivo(true, new RelatorioIntegridadeBanco(), true));
        Assert.Throws<ArgumentException>(() => new LeituraBancoVivo(true, null, false));
        Assert.Throws<ArgumentNullException>(() => ClassificacaoBancoVivo.Classificar(null!));
    }

    [Fact]
    public void Classe_UsaORelatorio_ENaoReleOSqlite()
    {
        var fonte = File.ReadAllText(Path.Combine(EncontrarRaiz(), "OrcaFacil.Core", "Armazenamento", "ClassificacaoBancoVivo.cs"));

        Assert.Contains("relatorio.Integro", fonte, StringComparison.Ordinal);
        Assert.Contains("MigrationsDesconhecidasNoHistorico", fonte, StringComparison.Ordinal);
        Assert.Contains("MigrationsPelaMetade", fonte, StringComparison.Ordinal);
        Assert.Contains("MigrationsNoHistoricoComObjetoFaltando", fonte, StringComparison.Ordinal);
        Assert.Contains("MigrationsAusentesNoHistorico", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("sqlite_master", fonte, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MigrateAsync", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteSql", fonte, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verificador_BancoMigrado_ClassificaComoIntegro()
    {
        using var banco = new BancoTemporario();
        await using var ctx = banco.CriarContexto();
        await ctx.Database.MigrateAsync();

        var relatorio = await VerificadorIntegridadeBanco.VerificarAsync(ctx);

        Assert.True(relatorio.Integro);
        Assert.Equal(SituacaoBancoVivo.Integro, ClassificacaoBancoVivo.Classificar(LeituraBancoVivo.Lida(relatorio)));
    }

    [Fact]
    public async Task Verificador_BancoAntigo_ClassificaComoDesatualizado_NaoComoInconsistente()
    {
        using var banco = new BancoTemporario();
        await using var ctx = banco.CriarContexto();
        await ctx.Database.MigrateAsync(Cartoes);

        var relatorio = await VerificadorIntegridadeBanco.VerificarAsync(ctx);

        Assert.False(relatorio.Integro);
        Assert.NotEmpty(relatorio.MigrationsAusentesNoHistorico);
        Assert.Equal(SituacaoBancoVivo.Desatualizado, ClassificacaoBancoVivo.Classificar(LeituraBancoVivo.Lida(relatorio)));
    }

    [Fact]
    public async Task Verificador_TabelaApagada_ClassificaComoInconsistente()
    {
        using var banco = new BancoTemporario();
        await using var ctx = banco.CriarContexto();
        await ctx.Database.MigrateAsync();
        await ctx.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");
        await ctx.Database.ExecuteSqlRawAsync("DROP TABLE LogsEvento");

        var relatorio = await VerificadorIntegridadeBanco.VerificarAsync(ctx);

        Assert.Contains("LogsEvento", relatorio.TabelasFaltando);
        Assert.Equal(SituacaoBancoVivo.Inconsistente, ClassificacaoBancoVivo.Classificar(LeituraBancoVivo.Lida(relatorio)));
    }

    [Fact]
    public async Task Verificador_MigrationDesconhecida_ClassificaComoSchemaMaisNovo()
    {
        using var banco = new BancoTemporario();
        await using var ctx = banco.CriarContexto();
        await ctx.Database.MigrateAsync();
        await ctx.Database.ExecuteSqlRawAsync(
            "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20990101000000_Futura', '10.0.0')");

        var relatorio = await VerificadorIntegridadeBanco.VerificarAsync(ctx);

        Assert.Contains("20990101000000_Futura", relatorio.MigrationsDesconhecidasNoHistorico);
        Assert.Equal(SituacaoBancoVivo.SchemaMaisNovoQueOApp, ClassificacaoBancoVivo.Classificar(LeituraBancoVivo.Lida(relatorio)));
    }

    private static string EncontrarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OrcaFacil.Core", "Armazenamento", "ClassificacaoBancoVivo.cs")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar ClassificacaoBancoVivo.cs a partir de " + AppContext.BaseDirectory);
    }

    private sealed class BancoTemporario : IDisposable
    {
        private readonly string _caminho;
        private readonly SqliteConnection _conexao;

        public BancoTemporario()
        {
            _caminho = Path.Combine(Path.GetTempPath(), $"orcafacil-arm-{Guid.NewGuid():N}.db");
            _conexao = new SqliteConnection($"Data Source={_caminho}");
            _conexao.Open();
        }

        public AppDbContext CriarContexto()
        {
            var opcoes = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_conexao)
                .Options;
            return new AppDbContext(opcoes);
        }

        public void Dispose()
        {
            _conexao.Dispose();
            SqliteConnection.ClearAllPools();
            if (File.Exists(_caminho))
                File.Delete(_caminho);
        }
    }
}
