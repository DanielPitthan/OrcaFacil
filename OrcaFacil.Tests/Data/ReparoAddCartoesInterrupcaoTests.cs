using System.Data;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OrcaFacil.Core.Data;

namespace OrcaFacil.Tests.Data;

/// <summary>
/// Cada comando da migration AddCartoes é um ponto em que o processo pode morrer.
/// O EF confirma o lote antes do DROP/RENAME e de novo antes do histórico; os
/// testes também param entre um comando e o outro, com cada um já gravado.
/// O reparo em si faz DROP e RENAME na mesma transação: se ele morre no DROP,
/// a troca desfaz. Cópias com outro conteúdo não são apagadas.
/// </summary>
public class ReparoAddCartoesInterrupcaoTests : IClassFixture<ReparoAddCartoesInterrupcaoTests.BancoAntesDeAddCartoes>
{
    private const string PagamentoCesta = "20261013195343_AddPagamentoCompraCesta";
    private const string Cartoes = "20261015194450_AddCartoes";
    private const string IndiceHash = "20261017172616_AddIndiceHashImportacaoItemFatura";

    private readonly BancoAntesDeAddCartoes _origem;

    public ReparoAddCartoesInterrupcaoTests(BancoAntesDeAddCartoes origem) => _origem = origem;

    public static IEnumerable<object[]> PontosDeInterrupcao()
    {
        var comandos = ComandosAddCartoes();
        Assert.Contains(comandos, comando => comando.Contains("DROP TABLE \"ComprasCesta\"", StringComparison.Ordinal));
        Assert.Contains(comandos, comando => comando.Contains("DROP TABLE \"Transacoes\"", StringComparison.Ordinal));
        Assert.Contains(comandos, comando => comando.Contains("RENAME TO \"ComprasCesta\"", StringComparison.Ordinal));
        Assert.Contains(comandos, comando => comando.Contains("RENAME TO \"Transacoes\"", StringComparison.Ordinal));

        yield return [0, "antes do primeiro comando"];
        for (var i = 0; i < comandos.Count; i++)
            yield return [i + 1, PrimeiraLinha(comandos[i])];
    }

    [Theory]
    [MemberData(nameof(PontosDeInterrupcao))]
    public async Task MorteNoComando_RetryConclui_EPreservaDados(int comandosAplicados, string etapa)
    {
        using var banco = _origem.Copiar();
        await using (var ctx = banco.CriarContexto())
        {
            await AplicarComandosAsync(ctx, comandosAplicados);
            if (await TabelaExisteAsync(ctx, "Cartoes"))
            {
                await InserirAsync(ctx, """
                    INSERT INTO Cartoes (Nome, Bandeira, UltimosDigitos, Tipo, TipoBeneficio, Ativo, CriadoEm)
                    VALUES ('Vale', 'Alelo', '1234', 1, 0, 1, '2026-03-01 00:00:00')
                    """);
            }

            if (await TabelaExisteAsync(ctx, "RecargasCartao") && await TabelaExisteAsync(ctx, "Cartoes"))
            {
                var cartaoId = await EscalarAsync(ctx, "SELECT Id FROM Cartoes WHERE Nome = 'Vale'");
                await InserirAsync(ctx, $"""
                    INSERT INTO RecargasCartao (CartaoId, Data, Valor, CriadoEm)
                    VALUES ({cartaoId}, '2026-03-04 00:00:00', '80.00', '2026-03-04 00:00:00')
                    """);
            }
        }

        var antes = await FotografarAsync(banco);
        await using (var ctx = banco.CriarContexto())
        {
            await PreparoBanco.ExigirIntegroAsync(ctx);
            await PreparoBanco.ExigirIntegroAsync(ctx);
        }

        var depois = await FotografarAsync(banco);
        Assert.Equal(antes.Lancamentos, depois.Lancamentos);
        if (antes.Cartoes != "ausente")
            Assert.Equal(antes.Cartoes, depois.Cartoes);
        if (antes.Recargas != "ausente")
            Assert.Equal(antes.Recargas, depois.Recargas);

        await using var conferir = banco.CriarContexto();
        var relatorio = await VerificadorIntegridadeBanco.VerificarAsync(conferir);
        Assert.True(relatorio.Integro, etapa + " → " + relatorio.Resumo);
        Assert.Contains(Cartoes, relatorio.MigrationsNoHistorico);
        Assert.Contains(IndiceHash, relatorio.MigrationsNoHistorico);
        Assert.Equal(0L, await EscalarAsync(conferir, "SELECT COUNT(*) FROM sqlite_master WHERE name LIKE 'ef_temp_%'"));
        Assert.Equal("", await EscalarTextoAsync(conferir, "SELECT ifnull(group_concat(\"table\" || '.' || rowid), '') FROM pragma_foreign_key_check"));
        Assert.Contains("FK_Transacoes_Cartoes_CartaoId", await SqlTabelaAsync(conferir, "Transacoes"));
        Assert.Contains("FK_ComprasCesta_Cartoes_CartaoBeneficioId", await SqlTabelaAsync(conferir, "ComprasCesta"));
        Assert.Contains("FK_ComprasCesta_ItensLancamento_ItemFaturaId", await SqlTabelaAsync(conferir, "ComprasCesta"));
        Assert.Equal(1L, await EscalarAsync(conferir, "SELECT COUNT(*) FROM MembrosFamilia"));
        Assert.Equal(2L, await EscalarAsync(conferir, "SELECT COUNT(*) FROM Categorias"));
        Assert.Equal("Daniel", await EscalarTextoAsync(conferir, "SELECT Nome FROM MembrosFamilia"));
        Assert.Equal("42.50", await EscalarTextoAsync(conferir, "SELECT Valor FROM Transacoes WHERE Descricao = 'Mercado'"));
        Assert.Equal(1L, await EscalarAsync(conferir, "SELECT Pago FROM Transacoes WHERE Descricao = 'Mercado'"));
        Assert.Equal(
            await EscalarAsync(conferir, "SELECT Id FROM Transacoes WHERE Descricao = 'Mercado'"),
            await EscalarAsync(conferir, "SELECT TransacaoOrigemId FROM Transacoes WHERE Descricao = 'Ônibus'"));
        Assert.Equal("Hortifruti", await EscalarTextoAsync(conferir, "SELECT Observacao FROM Transacoes WHERE Descricao = 'Mercado'"));
        Assert.Equal("Supermercado", await EscalarTextoAsync(conferir, "SELECT Descricao FROM ItensLancamento"));
        Assert.Equal(
            await EscalarAsync(conferir, "SELECT Id FROM Transacoes WHERE Descricao = 'Mercado'"),
            await EscalarAsync(conferir, "SELECT TransacaoId FROM ItensLancamento"));
        Assert.Equal("Arroz", await EscalarTextoAsync(conferir, "SELECT p.Nome FROM ItensCompraCesta i JOIN ProdutosCesta p ON p.Id = i.ProdutoCestaId"));
        Assert.Equal("Mercado Central", await EscalarTextoAsync(conferir, "SELECT l.Nome FROM ComprasCesta c JOIN LocaisCompra l ON l.Id = c.LocalCompraId"));
        Assert.Equal(150m, Convert.ToDecimal(await EscalarTextoAsync(conferir, "SELECT ValorBeneficio FROM ComprasCesta"), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(800m, Convert.ToDecimal(await EscalarTextoAsync(conferir, "SELECT ValorLimite FROM Orcamentos"), System.Globalization.CultureInfo.InvariantCulture));

        if (antes.Cartoes == "ausente")
            Assert.Equal(0L, await EscalarAsync(conferir, "SELECT COUNT(*) FROM Cartoes"));
        else
            Assert.Equal("Vale", await EscalarTextoAsync(conferir, "SELECT Nome FROM Cartoes"));

        if (antes.Recargas == "ausente")
            Assert.Equal(0L, await EscalarAsync(conferir, "SELECT COUNT(*) FROM RecargasCartao"));
        else
            Assert.Equal("80.00", await EscalarTextoAsync(conferir, "SELECT Valor FROM RecargasCartao"));
    }

    [Fact]
    public async Task CopiaComMenosLinhas_NaoApagaNenhumLado()
    {
        using var banco = _origem.Copiar();
        await using (var ctx = banco.CriarContexto())
        {
            await AplicarComandosAsync(ctx, IndiceApos("INSERT INTO \"ef_temp_Transacoes\""));
            await ExecutarAsync(ctx, "DELETE FROM ef_temp_Transacoes WHERE Descricao = 'Ônibus'");
        }

        await ExigirQueBloqueiaAsync(banco, "Transacoes", "Nenhuma das duas foi apagada");

        await using var conferir = banco.CriarContexto();
        Assert.Equal(2L, await EscalarAsync(conferir, "SELECT COUNT(*) FROM Transacoes"));
        Assert.Equal(1L, await EscalarAsync(conferir, "SELECT COUNT(*) FROM ef_temp_Transacoes"));
        Assert.Equal(1L, await EscalarAsync(conferir, "SELECT COUNT(*) FROM ComprasCesta"));
        Assert.Equal(1L, await EscalarAsync(conferir, "SELECT COUNT(*) FROM ef_temp_ComprasCesta"));
        Assert.Equal("Hortifruti", await EscalarTextoAsync(conferir, "SELECT Observacao FROM Transacoes WHERE Descricao = 'Mercado'"));
        Assert.Equal("Ônibus", await EscalarTextoAsync(conferir, "SELECT Descricao FROM Transacoes WHERE Descricao = 'Ônibus'"));
        Assert.Equal(0L, await HistoricoAddCartoesAsync(conferir));
    }

    [Fact]
    public async Task MesmaContagemComConteudoDiferente_NaoApagaNenhumLado()
    {
        using var banco = _origem.Copiar();
        await using (var ctx = banco.CriarContexto())
        {
            await AplicarComandosAsync(ctx, IndiceApos("INSERT INTO \"ef_temp_Transacoes\""));
            await ExecutarAsync(ctx, "UPDATE ef_temp_Transacoes SET Observacao = 'Trocado' WHERE Descricao = 'Mercado'");
        }

        await ExigirQueBloqueiaAsync(banco, "Transacoes", "Nenhuma das duas foi apagada");

        await using var conferir = banco.CriarContexto();
        Assert.Equal(
            await EscalarAsync(conferir, "SELECT COUNT(*) FROM Transacoes"),
            await EscalarAsync(conferir, "SELECT COUNT(*) FROM ef_temp_Transacoes"));
        Assert.Equal("Hortifruti", await EscalarTextoAsync(conferir, "SELECT Observacao FROM Transacoes WHERE Descricao = 'Mercado'"));
        Assert.Equal("Trocado", await EscalarTextoAsync(conferir, "SELECT Observacao FROM ef_temp_Transacoes WHERE Descricao = 'Mercado'"));
        Assert.Equal(1L, await EscalarAsync(conferir, "SELECT COUNT(*) FROM ComprasCesta"));
        Assert.Equal(1L, await EscalarAsync(conferir, "SELECT COUNT(*) FROM ef_temp_ComprasCesta"));
        Assert.Equal(0L, await HistoricoAddCartoesAsync(conferir));
    }

    [Fact]
    public async Task OriginalReconstruida_CopiaComMesmoTamanhoEOutroConteudo_NaoApagaACopia()
    {
        using var banco = _origem.Copiar();
        await using (var ctx = banco.CriarContexto())
        {
            await AplicarComandosAsync(ctx, IndiceApos("RENAME TO \"Transacoes\""));
            await ExecutarAsync(ctx, "CREATE TABLE ef_temp_Transacoes AS SELECT * FROM Transacoes");
            await ExecutarAsync(ctx, "UPDATE ef_temp_Transacoes SET Observacao = 'Outro' WHERE Descricao = 'Mercado'");
        }

        await ExigirQueBloqueiaAsync(banco, "Transacoes", "A cópia não foi apagada");

        await using var conferir = banco.CriarContexto();
        Assert.Equal("Hortifruti", await EscalarTextoAsync(conferir, "SELECT Observacao FROM Transacoes WHERE Descricao = 'Mercado'"));
        Assert.Equal("Outro", await EscalarTextoAsync(conferir, "SELECT Observacao FROM ef_temp_Transacoes WHERE Descricao = 'Mercado'"));
        Assert.Equal(
            await EscalarAsync(conferir, "SELECT COUNT(*) FROM Transacoes"),
            await EscalarAsync(conferir, "SELECT COUNT(*) FROM ef_temp_Transacoes"));
        Assert.Contains("FK_Transacoes_Cartoes_CartaoId", await SqlTabelaAsync(conferir, "Transacoes"));
        Assert.Equal(0L, await HistoricoAddCartoesAsync(conferir));
    }

    [Fact]
    public async Task OriginalReconstruida_CopiaRedundante_ApagaSoACopiaEConclui()
    {
        using var banco = _origem.Copiar();
        await using (var ctx = banco.CriarContexto())
        {
            await AplicarComandosAsync(ctx, IndiceApos("RENAME TO \"Transacoes\""));
            await ExecutarAsync(ctx, "CREATE TABLE ef_temp_Transacoes AS SELECT * FROM Transacoes");
            await ExecutarAsync(ctx, "DELETE FROM ef_temp_Transacoes WHERE Descricao = 'Ônibus'");
        }

        await using (var ctx = banco.CriarContexto())
        {
            await PreparoBanco.ExigirIntegroAsync(ctx);
            await PreparoBanco.ExigirIntegroAsync(ctx);
        }

        await using var conferir = banco.CriarContexto();
        Assert.Equal(0L, await EscalarAsync(conferir, "SELECT COUNT(*) FROM sqlite_master WHERE name LIKE 'ef_temp_%'"));
        Assert.Equal(2L, await EscalarAsync(conferir, "SELECT COUNT(*) FROM Transacoes"));
        Assert.Equal("Hortifruti", await EscalarTextoAsync(conferir, "SELECT Observacao FROM Transacoes WHERE Descricao = 'Mercado'"));
        Assert.Equal("Ônibus", await EscalarTextoAsync(conferir, "SELECT Descricao FROM Transacoes WHERE Descricao = 'Ônibus'"));
        Assert.Contains(Cartoes, (await VerificadorIntegridadeBanco.VerificarAsync(conferir)).MigrationsNoHistorico);
        Assert.True((await VerificadorIntegridadeBanco.VerificarAsync(conferir)).Integro);
    }

    [Fact]
    public async Task ReparoMorreNoDrop_DesfazATroca_ERetryConcluiComBackup()
    {
        var pasta = Path.Combine(Path.GetTempPath(), $"orcafacil-morte-drop-{Guid.NewGuid():N}");
        var caminho = PrepararCopia(pasta);
        try
        {
            await using (var ctx = Abrir(caminho))
                await AplicarComandosAsync(ctx, IndiceApos("INSERT INTO \"ef_temp_Transacoes\""));

            string? derrubada = null;
            ResultadoPreparoBanco resultado;
            try
            {
                ReconciliadorAddCartoes.DepoisDoDropAntesDoRename = (tabela, _) =>
                {
                    derrubada = tabela;
                    return ValueTask.FromException(new InvalidOperationException("morte no DROP"));
                };

                await using var ctx = Abrir(caminho);
                resultado = await PreparoBanco.MigrarEVerificarAsync(
                    ctx,
                    new OpcoesPreparoBanco { VersaoAplicativo = "9.9.9-teste" });
            }
            finally
            {
                ReconciliadorAddCartoes.DepoisDoDropAntesDoRename = null;
            }

            Assert.Equal("ComprasCesta", derrubada);
            Assert.True(resultado.ExigeRecuperacao);
            Assert.NotNull(resultado.Aviso);
            Assert.Contains("arquivo do banco não foi substituído", resultado.Aviso);
            Assert.DoesNotContain("banco não foi apagado", resultado.Aviso);
            Assert.NotNull(resultado.CaminhoBackup);
            Assert.True(File.Exists(resultado.CaminhoBackup));

            await using (var ctx = Abrir(caminho))
            {
                Assert.Equal(1L, await EscalarAsync(ctx, "SELECT COUNT(*) FROM ComprasCesta"));
                Assert.Equal(1L, await EscalarAsync(ctx, "SELECT COUNT(*) FROM ef_temp_ComprasCesta"));
                Assert.Equal(2L, await EscalarAsync(ctx, "SELECT COUNT(*) FROM Transacoes"));
                Assert.Equal(2L, await EscalarAsync(ctx, "SELECT COUNT(*) FROM ef_temp_Transacoes"));
                Assert.Equal("42.50", await EscalarTextoAsync(ctx, "SELECT Valor FROM Transacoes WHERE Descricao = 'Mercado'"));
                Assert.Equal(0L, await HistoricoAddCartoesAsync(ctx));
            }

            await using (var backup = new SqliteConnection($"Data Source={resultado.CaminhoBackup}"))
            {
                await backup.OpenAsync();
                Assert.Equal("Daniel", await EscalarConexaoAsync(backup, "SELECT Nome FROM MembrosFamilia"));
                Assert.Equal("42.50", await EscalarConexaoAsync(backup, "SELECT Valor FROM Transacoes WHERE Descricao = 'Mercado'"));
                Assert.Equal("1", await EscalarConexaoAsync(backup, "SELECT COUNT(*) FROM ComprasCesta"));
            }

            await using (var ctx = Abrir(caminho))
                await PreparoBanco.ExigirIntegroAsync(ctx);

            await using var conferir = Abrir(caminho);
            var relatorio = await VerificadorIntegridadeBanco.VerificarAsync(conferir);
            Assert.True(relatorio.Integro, relatorio.Resumo);
            Assert.Contains(Cartoes, relatorio.MigrationsNoHistorico);
            Assert.Equal(0L, await EscalarAsync(conferir, "SELECT COUNT(*) FROM sqlite_master WHERE name LIKE 'ef_temp_%'"));
            Assert.Equal("Daniel", await EscalarTextoAsync(conferir, "SELECT Nome FROM MembrosFamilia"));
            Assert.Equal("42.50", await EscalarTextoAsync(conferir, "SELECT Valor FROM Transacoes WHERE Descricao = 'Mercado'"));
            Assert.Equal("150.00", await EscalarTextoAsync(conferir, "SELECT ValorBeneficio FROM ComprasCesta"));
        }
        finally
        {
            ApagarPasta(pasta);
        }
    }

    [Fact]
    public async Task MorteEntreDropERename_EmModoWal_RetryConclui_EBackupLeWal()
    {
        var pasta = Path.Combine(Path.GetTempPath(), $"orcafacil-wal-{Guid.NewGuid():N}");
        var caminho = PrepararCopia(pasta);
        try
        {
            await using var segurando = new SqliteConnection($"Data Source={caminho}");
            await segurando.OpenAsync();
            await ExecutarConexaoAsync(segurando, "PRAGMA journal_mode = WAL");
            await ExecutarConexaoAsync(segurando, "PRAGMA wal_autocheckpoint = 0");
            var comandos = ComandosAddCartoes();
            var ateODrop = IndiceApos("DROP TABLE \"ComprasCesta\"");
            for (var i = 0; i < ateODrop; i++)
                await ExecutarConexaoAsync(segurando, comandos[i]);

            await ExecutarConexaoAsync(segurando, "BEGIN");
            Assert.Equal("2", await EscalarConexaoAsync(segurando, "SELECT COUNT(*) FROM Transacoes"));
            Assert.Equal("wal", await EscalarConexaoAsync(segurando, "PRAGMA journal_mode"));
            Assert.True(File.Exists(caminho + "-wal") || File.Exists(caminho + "-shm"), "O journal WAL não deixou -wal nem -shm.");

            ResultadoPreparoBanco resultado;
            await using (var ctx = Abrir(caminho))
            {
                resultado = await PreparoBanco.MigrarEVerificarAsync(
                    ctx,
                    new OpcoesPreparoBanco { VersaoAplicativo = "9.9.9-teste" });
            }

            Assert.False(resultado.ExigeRecuperacao, resultado.Aviso ?? resultado.FalhaMigrate?.Message);
            Assert.NotNull(resultado.CaminhoBackup);
            Assert.True(File.Exists(resultado.CaminhoBackup));
            Assert.True(File.Exists(caminho + "-wal") || File.Exists(caminho + "-shm"));

            await using (var backup = new SqliteConnection($"Data Source={resultado.CaminhoBackup}"))
            {
                await backup.OpenAsync();
                Assert.Equal("Daniel", await EscalarConexaoAsync(backup, "SELECT Nome FROM MembrosFamilia"));
                Assert.Equal("42.50", await EscalarConexaoAsync(backup, "SELECT Valor FROM Transacoes WHERE Descricao = 'Mercado'"));
                Assert.Equal("0", await EscalarConexaoAsync(backup, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'ComprasCesta'"));
                Assert.Equal("150.00", await EscalarConexaoAsync(backup, "SELECT ValorBeneficio FROM ef_temp_ComprasCesta"));
            }

            await using var conferir = Abrir(caminho);
            var relatorio = await VerificadorIntegridadeBanco.VerificarAsync(conferir);
            Assert.True(relatorio.Integro, relatorio.Resumo);
            Assert.Contains(Cartoes, relatorio.MigrationsNoHistorico);
            Assert.Equal(0L, await EscalarAsync(conferir, "SELECT COUNT(*) FROM sqlite_master WHERE name LIKE 'ef_temp_%'"));
            Assert.Equal("150.00", await EscalarTextoAsync(conferir, "SELECT ValorBeneficio FROM ComprasCesta"));
            Assert.Equal("Daniel", await EscalarTextoAsync(conferir, "SELECT Nome FROM MembrosFamilia"));
            Assert.Equal("42.50", await EscalarTextoAsync(conferir, "SELECT Valor FROM Transacoes WHERE Descricao = 'Mercado'"));

            await ExecutarConexaoAsync(segurando, "ROLLBACK");
        }
        finally
        {
            ApagarPasta(pasta);
        }
    }

    [Fact]
    public async Task HistoricoSemProductVersion_GravaVersaoLidaDoEf()
    {
        using var banco = _origem.Copiar();
        await using (var ctx = banco.CriarContexto())
        {
            await AplicarComandosAsync(ctx, 1);
            await ExecutarAsync(ctx, "UPDATE __EFMigrationsHistory SET ProductVersion = ''");
        }

        await using (var ctx = banco.CriarContexto())
            await PreparoBanco.ExigirIntegroAsync(ctx);

        await using var conferir = banco.CriarContexto();
        var gravada = await EscalarTextoAsync(
            conferir,
            "SELECT ProductVersion FROM __EFMigrationsHistory WHERE MigrationId = $id",
            ("$id", Cartoes));
        Assert.Equal(VersaoEsperadaDoEf(), gravada);
        Assert.DoesNotContain("+", gravada);
        Assert.Matches(@"^\d+\.\d+\.\d+", gravada);
    }

    private string PrepararCopia(string pasta)
    {
        Directory.CreateDirectory(pasta);
        using var semente = _origem.Copiar();
        var caminho = Path.Combine(pasta, "orcafacil.db");
        File.Copy(semente.Caminho, caminho);
        return caminho;
    }

    private static AppDbContext Abrir(string caminho)
    {
        var opcoes = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={caminho}")
            .Options;
        return new AppDbContext(opcoes);
    }

    private static void ApagarPasta(string pasta)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(pasta))
            Directory.Delete(pasta, recursive: true);
    }

    private static async Task ExigirQueBloqueiaAsync(BancoCopiado banco, string tabela, string trecho)
    {
        await using var ctx = banco.CriarContexto();
        var excecao = await Assert.ThrowsAsync<IntegridadeBancoException>(() => PreparoBanco.ExigirIntegroAsync(ctx));
        var deNovo = await Assert.ThrowsAsync<IntegridadeBancoException>(() => PreparoBanco.ExigirIntegroAsync(ctx));
        Assert.Contains(tabela, excecao.Message);
        Assert.Contains(trecho, excecao.Message);
        Assert.DoesNotContain("banco não foi apagado", excecao.Message);
        Assert.Contains("arquivo do banco não foi substituído", excecao.Message);
        Assert.Equal(excecao.Message, deNovo.Message);
    }

    private static int IndiceApos(string trecho)
    {
        var comandos = ComandosAddCartoes();
        var indice = comandos.FindIndex(comando => comando.Contains(trecho, StringComparison.Ordinal));
        if (indice < 0)
            throw new InvalidOperationException("Comando não encontrado: " + trecho);

        return indice + 1;
    }

    private static Task ExecutarAsync(AppDbContext ctx, string sql) =>
        ctx.Database.ExecuteSqlRawAsync(sql);

    private static Task<long> HistoricoAddCartoesAsync(AppDbContext ctx) =>
        EscalarAsync(ctx, "SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId = $id", ("$id", Cartoes));

    private static async Task ExecutarConexaoAsync(SqliteConnection conexao, string sql)
    {
        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<string> EscalarConexaoAsync(SqliteConnection conexao, string sql)
    {
        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        var valor = await comando.ExecuteScalarAsync();
        return valor is null or DBNull ? "" : Convert.ToString(valor, System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }

    private static string VersaoEsperadaDoEf()
    {
        var informacional = typeof(DbContext).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informacional))
        {
            var mais = informacional.IndexOf('+', StringComparison.Ordinal);
            var versao = mais >= 0 ? informacional[..mais] : informacional;
            if (!string.IsNullOrWhiteSpace(versao))
                return versao;
        }

        var numero = typeof(DbContext).Assembly.GetName().Version;
        if (numero is not null)
            return $"{numero.Major}.{numero.Minor}.{numero.Build}";

        throw new InvalidOperationException("Não foi possível ler a versão do EF no teste.");
    }

    private static async Task AplicarComandosAsync(AppDbContext ctx, int quantidade)
    {
        var comandos = ComandosAddCartoes();
        var conexao = ctx.Database.GetDbConnection();
        if (conexao.State != ConnectionState.Open)
            await conexao.OpenAsync();

        for (var i = 0; i < quantidade; i++)
        {
            await using var comando = conexao.CreateCommand();
            comando.CommandText = comandos[i];
            await comando.ExecuteNonQueryAsync();
        }
    }

    private static async Task<Foto> FotografarAsync(BancoCopiado banco)
    {
        await using var ctx = banco.CriarContexto();
        var transacoes = await TabelaVivaAsync(ctx, "Transacoes");
        var compras = await TabelaVivaAsync(ctx, "ComprasCesta");
        var lancamentos = string.Join(
            "\n",
            await TextoAsync(ctx, $"""
                SELECT Id || '|' || Descricao || '|' || Valor || '|' || Pago || '|' || ifnull(TransacaoOrigemId, '') || '|' || ifnull(Observacao, '')
                FROM "{transacoes}" ORDER BY Id
                """),
            await TextoAsync(ctx, $"""
                SELECT Id || '|' || Mes || '|' || Ano || '|' || ValorBeneficio || '|' || ValorDinheiro || '|' || ValorCartaoCredito || '|' || LocalCompraId || '|' || ifnull(TransacaoId, '')
                FROM "{compras}" ORDER BY Id
                """),
            await TextoAsync(ctx, """
                SELECT Id || '|' || Descricao || '|' || Valor || '|' || TransacaoId
                FROM ItensLancamento ORDER BY Id
                """),
            await TextoAsync(ctx, """
                SELECT Id || '|' || CompraCestaId || '|' || ProdutoCestaId || '|' || Preco || '|' || Ordem
                FROM ItensCompraCesta ORDER BY Id
                """),
            await TextoAsync(ctx, "SELECT Id || '|' || Nome FROM MembrosFamilia ORDER BY Id"),
            await TextoAsync(ctx, "SELECT Id || '|' || Nome FROM Categorias ORDER BY Id"),
            await TextoAsync(ctx, "SELECT Id || '|' || Nome FROM LocaisCompra ORDER BY Id"),
            await TextoAsync(ctx, "SELECT Id || '|' || Nome FROM ProdutosCesta ORDER BY Id"),
            await TextoAsync(ctx, "SELECT Id || '|' || ValorLimite || '|' || Mes || '|' || Ano FROM Orcamentos ORDER BY Id"));

        var cartoes = await TabelaExisteAsync(ctx, "Cartoes")
            ? await TextoAsync(ctx, "SELECT Id || '|' || Nome || '|' || Bandeira || '|' || UltimosDigitos FROM Cartoes ORDER BY Id")
            : "ausente";
        var recargas = await TabelaExisteAsync(ctx, "RecargasCartao")
            ? await TextoAsync(ctx, "SELECT Id || '|' || CartaoId || '|' || Valor FROM RecargasCartao ORDER BY Id")
            : "ausente";

        return new Foto(lancamentos, cartoes, recargas);
    }

    private static async Task<string> TabelaVivaAsync(AppDbContext ctx, string nome)
    {
        if (await TabelaExisteAsync(ctx, nome))
            return nome;

        var temp = "ef_temp_" + nome;
        if (await TabelaExisteAsync(ctx, temp))
            return temp;

        throw new InvalidOperationException($"Nem {nome} nem {temp} existem para fotografar os lançamentos.");
    }

    private static List<string> ComandosAddCartoes()
    {
        var opcoes = new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options;
        using var ctx = new AppDbContext(opcoes);
        var assembly = ctx.GetService<IMigrationsAssembly>();
        var gerador = ctx.GetService<IMigrationsSqlGenerator>();
        var inicializador = ctx.GetService<IModelRuntimeInitializer>();
        var migration = assembly.CreateMigration(assembly.Migrations[Cartoes], "Microsoft.EntityFrameworkCore.Sqlite");
        var modelo = inicializador.Initialize(migration.TargetModel, designTime: true);
        var comandos = gerador.Generate(migration.UpOperations, modelo).Select(comando => comando.CommandText).ToList();
        if (comandos.Count < 20)
            throw new InvalidOperationException($"AddCartoes gerou só {comandos.Count} comandos.");

        return comandos;
    }

    private static string PrimeiraLinha(string sql)
    {
        var linha = sql.Trim();
        var quebra = linha.IndexOf('\n', StringComparison.Ordinal);
        if (quebra >= 0)
            linha = linha[..quebra].Trim();

        return linha.Length <= 90 ? linha : linha[..90];
    }

    private static async Task<string> SqlTabelaAsync(AppDbContext ctx, string tabela) =>
        await EscalarTextoAsync(ctx, "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = $n", ("$n", tabela));

    private static async Task<bool> TabelaExisteAsync(AppDbContext ctx, string tabela) =>
        await EscalarAsync(ctx, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $n", ("$n", tabela)) > 0;

    private static async Task InserirAsync(AppDbContext ctx, string sql)
    {
        var afetadas = await ctx.Database.ExecuteSqlRawAsync(sql);
        Assert.Equal(1, afetadas);
    }

    private static async Task<string> TextoAsync(AppDbContext ctx, string sql)
    {
        var conexao = ctx.Database.GetDbConnection();
        if (conexao.State != ConnectionState.Open)
            await conexao.OpenAsync();

        var linhas = new List<string>();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        await using var leitor = await comando.ExecuteReaderAsync();
        while (await leitor.ReadAsync())
            linhas.Add(leitor.IsDBNull(0) ? "" : leitor.GetString(0));

        return string.Join("\n", linhas);
    }

    private static Task<long> EscalarAsync(AppDbContext ctx, string sql, params (string Nome, object Valor)[] parametros) =>
        EscalarLongoAsync(ctx, sql, parametros);

    private static async Task<long> EscalarLongoAsync(AppDbContext ctx, string sql, (string Nome, object Valor)[] parametros)
    {
        var valor = await EscalarObjetoAsync(ctx, sql, parametros);
        return Convert.ToInt64(valor, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<string> EscalarTextoAsync(AppDbContext ctx, string sql, params (string Nome, object Valor)[] parametros)
    {
        var valor = await EscalarObjetoAsync(ctx, sql, parametros);
        return valor is null or DBNull ? "" : Convert.ToString(valor, System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }

    private static async Task<object?> EscalarObjetoAsync(AppDbContext ctx, string sql, (string Nome, object Valor)[] parametros)
    {
        var conexao = ctx.Database.GetDbConnection();
        if (conexao.State != ConnectionState.Open)
            await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        foreach (var (nome, valor) in parametros)
        {
            var parametro = comando.CreateParameter();
            parametro.ParameterName = nome;
            parametro.Value = valor;
            comando.Parameters.Add(parametro);
        }

        return await comando.ExecuteScalarAsync();
    }

    private readonly record struct Foto(string Lancamentos, string Cartoes, string Recargas);

    public sealed class BancoAntesDeAddCartoes : IDisposable
    {
        private readonly string _caminho;

        public BancoAntesDeAddCartoes()
        {
            _caminho = Path.Combine(Path.GetTempPath(), $"orcafacil-pre-cartoes-{Guid.NewGuid():N}.db");
            using var ctx = Criar(_caminho);
            ctx.Database.MigrateAsync(PagamentoCesta).GetAwaiter().GetResult();
            Inserir(ctx, """
                INSERT INTO Categorias (Nome, Icone, CorHex, TipoPadrao, EhPadraoDoSistema, PermiteItensFatura, Ativo)
                VALUES ('Alimentação', 'A', '#9fb4c7', 1, 1, 0, 1)
                """);
            Inserir(ctx, """
                INSERT INTO Categorias (Nome, Icone, CorHex, TipoPadrao, EhPadraoDoSistema, PermiteItensFatura, Ativo)
                VALUES ('Cartão de Crédito', 'C', '#B2CFE6', 1, 1, 1, 1)
                """);
            Inserir(ctx, """
                INSERT INTO MembrosFamilia (Nome, Avatar, Role, Ativo, CriadoEm)
                VALUES ('Daniel', 'D', 0, 1, '2026-01-15 00:00:00')
                """);
            var alimentacao = Escalar(ctx, "SELECT Id FROM Categorias WHERE Nome = 'Alimentação'");
            var cartao = Escalar(ctx, "SELECT Id FROM Categorias WHERE Nome = 'Cartão de Crédito'");
            var membro = Escalar(ctx, "SELECT Id FROM MembrosFamilia WHERE Nome = 'Daniel'");
            Inserir(ctx, $"""
                INSERT INTO Transacoes (Descricao, Valor, Data, Tipo, Recorrencia, Pago, CategoriaId, MembroFamiliaId, CriadoEm, Observacao)
                VALUES ('Mercado', '42.50', '2026-02-03 00:00:00', 1, 0, 1, {alimentacao}, {membro}, '2026-02-03 00:00:00', 'Hortifruti')
                """);
            var mercado = Escalar(ctx, "SELECT Id FROM Transacoes WHERE Descricao = 'Mercado'");
            Inserir(ctx, $"""
                INSERT INTO Transacoes (Descricao, Valor, Data, Tipo, Recorrencia, Pago, CategoriaId, MembroFamiliaId, CriadoEm, TransacaoOrigemId)
                VALUES ('Ônibus', '4.40', '2026-02-04 00:00:00', 1, 0, 1, {alimentacao}, {membro}, '2026-02-04 00:00:00', {mercado})
                """);
            Inserir(ctx, $"""
                INSERT INTO Orcamentos (CategoriaId, ValorLimite, Mes, Ano, CriadoEm)
                VALUES ({alimentacao}, '800.00', 2, 2026, '2026-02-01 00:00:00')
                """);
            Inserir(ctx, $"""
                INSERT INTO ItensLancamento (Descricao, Valor, Natureza, Ordem, TransacaoId, CategoriaId)
                VALUES ('Supermercado', '42.50', 0, 1, {mercado}, {cartao})
                """);
            Inserir(ctx, """
                INSERT INTO LocaisCompra (Nome, Ativo, CriadoEm)
                VALUES ('Mercado Central', 1, '2026-03-01 00:00:00')
                """);
            var local = Escalar(ctx, "SELECT Id FROM LocaisCompra WHERE Nome = 'Mercado Central'");
            Inserir(ctx, $"""
                INSERT INTO ComprasCesta (Mes, Ano, Data, LocalCompraId, ValorBeneficio, ValorDinheiro, ValorCartaoCredito, CriadoEm, TransacaoId)
                VALUES (3, 2026, '2026-03-02 00:00:00', {local}, '150.00', '20.00', '10.00', '2026-03-02 00:00:00', {mercado})
                """);
            var compra = Escalar(ctx, "SELECT Id FROM ComprasCesta");
            Inserir(ctx, $"""
                INSERT INTO ProdutosCesta (Nome, Ativo, CategoriaId, CriadoEm)
                VALUES ('Arroz', 1, {alimentacao}, '2026-03-01 00:00:00')
                """);
            var produto = Escalar(ctx, "SELECT Id FROM ProdutosCesta WHERE Nome = 'Arroz'");
            Inserir(ctx, $"""
                INSERT INTO ItensCompraCesta (CompraCestaId, ProdutoCestaId, Ordem, Preco)
                VALUES ({compra}, {produto}, 1, '8.90')
                """);
            ctx.Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(TRUNCATE)");
            SqliteConnection.ClearAllPools();
        }

        public BancoCopiado Copiar()
        {
            var destino = Path.Combine(Path.GetTempPath(), $"orcafacil-interrupcao-{Guid.NewGuid():N}.db");
            File.Copy(_caminho, destino);
            return new BancoCopiado(destino);
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(_caminho))
                File.Delete(_caminho);
        }

        private static AppDbContext Criar(string caminho)
        {
            var opcoes = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={caminho}")
                .Options;
            return new AppDbContext(opcoes);
        }

        private static void Inserir(AppDbContext ctx, string sql)
        {
            var afetadas = ctx.Database.ExecuteSqlRaw(sql);
            Assert.Equal(1, afetadas);
        }

        private static long Escalar(AppDbContext ctx, string sql)
        {
            var conexao = ctx.Database.GetDbConnection();
            if (conexao.State != ConnectionState.Open)
                conexao.Open();

            using var comando = conexao.CreateCommand();
            comando.CommandText = sql;
            return Convert.ToInt64(comando.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    public sealed class BancoCopiado : IDisposable
    {
        private readonly string _caminho;

        public BancoCopiado(string caminho) => _caminho = caminho;

        public string Caminho => _caminho;

        public AppDbContext CriarContexto()
        {
            var opcoes = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={_caminho}")
                .Options;
            return new AppDbContext(opcoes);
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(_caminho))
                File.Delete(_caminho);
            var wal = _caminho + "-wal";
            var shm = _caminho + "-shm";
            if (File.Exists(wal))
                File.Delete(wal);
            if (File.Exists(shm))
                File.Delete(shm);
        }
    }
}
