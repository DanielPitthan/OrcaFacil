using System.Data;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Dirpf;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Tests.Data;

/// <summary>
/// Cobre o upgrade do orcafacil.db: banco no schema antigo até o modelo atual,
/// ordem das migrations e ausência de mudança pendente no snapshot.
/// </summary>
public class MigracaoUpgradeTests
{
    private const string Inicial = "20260826112311_InitialCreate";
    private const string Cesta = "20261013193337_AddCestaBasica";
    private const string Cartoes = "20261015194450_AddCartoes";
    private const string Descritivo = "20261016011256_AddDescritivoItemFatura";
    private const string Estorno = "20261016182243_AddEhEstornoItemFatura";
    private const string HashImportacao = "20261017164213_AddHashImportacaoItemFatura";
    private const string IndiceHashImportacao = "20261017172616_AddIndiceHashImportacaoItemFatura";
    private const string RendaFixa = "20261020032353_AddRendaFixa";
    private const string RegraCatalogo = "20261020135920_AddRegraCatalogoComprovante";
    private const string ResultadoRealizado = "20261020172344_AddResultadoRealizadoVariavel";
    private const string Provento = "20261020175956_AddProvento";
    private const string CodigoDirpf = "20261020225810_AddCodigoDirpf";
    private const string ApuracaoIr = "20261021101911_AddApuracaoIrMensal";
    private const string DataPregao = "20261021115053_AddDataPregaoOperacaoAtivo";
    private const string IrrfInformado = "20261021222000_AddIrrfInformado";
    private const string DirpfRendaFixaJcp = "20261022113200_AddDirpfRendaFixaJcp";
    private const string DirpfLciLca = "20261022145822_AddCodigosDirpfLciLca";

    [Fact]
    public async Task BancoAntigo_MigraAteOAtual_PreservaDadosEAplicaDefaults()
    {
        using var banco = new BancoTemporario();

        await using (var ctx = banco.CriarContexto())
        {
            await ctx.Database.MigrateAsync(Inicial);
            await InserirAsync(ctx, """
                INSERT INTO Categorias (Nome, Icone, CorHex, TipoPadrao, EhPadraoDoSistema, Ativo)
                VALUES ('Alimentação', 'A', '#9fb4c7', 1, 1, 1)
                """);
            await InserirAsync(ctx, """
                INSERT INTO Categorias (Nome, Icone, CorHex, TipoPadrao, EhPadraoDoSistema, Ativo)
                VALUES ('Cartão de Crédito', 'C', '#B2CFE6', 1, 1, 1)
                """);
            await InserirAsync(ctx, """
                INSERT INTO MembrosFamilia (Nome, Avatar, Role, Ativo, CriadoEm)
                VALUES ('Daniel', 'D', 0, 1, '2026-01-15 00:00:00')
                """);
            var alimentacaoId = await EscalarAsync(ctx, "SELECT Id FROM Categorias WHERE Nome = 'Alimentação'");
            var cartaoId = await EscalarAsync(ctx, "SELECT Id FROM Categorias WHERE Nome = 'Cartão de Crédito'");
            var membroId = await EscalarAsync(ctx, "SELECT Id FROM MembrosFamilia WHERE Nome = 'Daniel'");
            await InserirAsync(ctx, $"""
                INSERT INTO Transacoes (Descricao, Valor, Data, Tipo, Recorrencia, CategoriaId, MembroFamiliaId, CriadoEm)
                VALUES ('Mercado', '42.50', '2026-02-03 00:00:00', 1, 0, {alimentacaoId}, {membroId}, '2026-02-03 00:00:00')
                """);
            await InserirAsync(ctx, $"""
                INSERT INTO Orcamentos (CategoriaId, ValorLimite, Mes, Ano, CriadoEm)
                VALUES ({alimentacaoId}, '800.00', 2, 2026, '2026-02-01 00:00:00')
                """);

            Assert.DoesNotContain("Pago", await ColunasAsync(ctx, "Transacoes"));
            Assert.DoesNotContain("PermiteItensFatura", await ColunasAsync(ctx, "Categorias"));
        }

        await using (var ctx = banco.CriarContexto())
        {
            await ctx.Database.MigrateAsync(Cesta);
            await InserirAsync(ctx, """
                INSERT INTO LocaisCompra (Nome, Ativo, CriadoEm)
                VALUES ('Mercado Central', 1, '2026-03-01 00:00:00')
                """);
            var localId = await EscalarAsync(ctx, "SELECT Id FROM LocaisCompra WHERE Nome = 'Mercado Central'");
            await InserirAsync(ctx, $"""
                INSERT INTO ComprasCesta (Mes, Ano, Data, LocalCompraId, ValorBeneficio, CriadoEm)
                VALUES (3, 2026, '2026-03-02 00:00:00', {localId}, '150.00', '2026-03-02 00:00:00')
                """);
            Assert.DoesNotContain("ValorDinheiro", await ColunasAsync(ctx, "ComprasCesta"));
            Assert.DoesNotContain("ValorCartaoCredito", await ColunasAsync(ctx, "ComprasCesta"));
        }

        await using (var ctx = banco.CriarContexto())
        {
            await ctx.Database.MigrateAsync(Cartoes);
            var cartaoCategoriaId = await EscalarAsync(ctx, "SELECT Id FROM Categorias WHERE Nome = 'Cartão de Crédito'");
            var transacaoId = await EscalarAsync(ctx, "SELECT Id FROM Transacoes WHERE Descricao = 'Mercado'");
            await InserirAsync(ctx, $"""
                INSERT INTO ItensLancamento (Descricao, Valor, Natureza, Ordem, TransacaoId, CategoriaId)
                VALUES ('Supermercado', '42.50', 0, 1, {transacaoId}, {cartaoCategoriaId})
                """);
            var colunasItem = await ColunasAsync(ctx, "ItensLancamento");
            Assert.DoesNotContain("Descritivo", colunasItem);
            Assert.DoesNotContain("EhEstorno", colunasItem);
            Assert.DoesNotContain("HashImportacao", colunasItem);
        }

        await using (var ctx = banco.CriarContexto())
        {
            await ctx.Database.MigrateAsync(Descritivo);
            Assert.Contains("Descritivo", await ColunasAsync(ctx, "ItensLancamento"));
            Assert.DoesNotContain("EhEstorno", await ColunasAsync(ctx, "ItensLancamento"));
            await InserirAsync(ctx, """
                UPDATE ItensLancamento SET Descritivo = 'Hortifruti' WHERE Descricao = 'Supermercado'
                """);
        }

        await using (var ctx = banco.CriarContexto())
        {
            await ctx.Database.MigrateAsync();

            var transacao = await ctx.Transacoes.SingleAsync(t => t.Descricao == "Mercado");
            Assert.Equal(42.50m, transacao.Valor);
            Assert.True(transacao.Pago);
            Assert.Null(transacao.CartaoId);

            var alimentacao = await ctx.Categorias.SingleAsync(c => c.Nome == "Alimentação");
            var cartao = await ctx.Categorias.SingleAsync(c => c.Nome == "Cartão de Crédito");
            Assert.False(alimentacao.PermiteItensFatura);
            Assert.True(cartao.PermiteItensFatura);

            var membro = await ctx.MembrosFamilia.SingleAsync();
            Assert.Equal("Daniel", membro.Nome);

            var orcamento = await ctx.Orcamentos.SingleAsync();
            Assert.Equal(800m, orcamento.ValorLimite);
            Assert.Null(orcamento.MetaReducaoPercentual);

            var compra = await ctx.ComprasCesta.Include(c => c.LocalCompra).SingleAsync();
            Assert.Equal("Mercado Central", compra.LocalCompra!.Nome);
            Assert.Equal(150m, compra.ValorBeneficio);
            Assert.Equal(0m, compra.ValorDinheiro);
            Assert.Equal(0m, compra.ValorCartaoCredito);

            var item = await ctx.ItensLancamento.SingleAsync();
            Assert.Equal("Supermercado", item.Descricao);
            Assert.Equal(42.50m, item.Valor);
            Assert.Equal("Hortifruti", item.Descritivo);
            Assert.False(item.EhEstorno);
            Assert.Null(item.HashImportacao);
            Assert.Null(transacao.OperacaoRendaFixaId);
            Assert.Null(transacao.OperacaoAtivoId);
            Assert.Null(transacao.ProventoId);
            Assert.Empty(ctx.Proventos);
            Assert.Empty(ctx.TitulosRendaFixa);
            Assert.Empty(ctx.OperacoesRendaFixa);
            Assert.Equal(CatalogoRegrasFiscais.Linhas().Count, await ctx.RegrasFiscais.CountAsync());
            Assert.Contains(await ctx.RegrasFiscais.ToListAsync(), regra => regra.Codigo == "RV.CustosReduzemResultado" && regra.EhSistema);
            Assert.Contains(await ctx.RegrasFiscais.ToListAsync(), regra => regra.Codigo == "RV.Darf.Codigo" && regra.Valor == 6015m && regra.EhSistema);
            Assert.Equal(CatalogoCodigosDirpf.Linhas().Count, await ctx.CodigosDirpf.CountAsync());
            Assert.Contains(await ctx.CodigosDirpf.ToListAsync(), codigo => codigo.Codigo == "01" && codigo.EhSistema);
            Assert.Contains(await ctx.CodigosDirpf.ToListAsync(), codigo => codigo.Codigo == "02" && codigo.Variante == "Fiagro" && codigo.EhSistema);
            Assert.Contains(await ctx.CodigosDirpf.ToListAsync(), codigo => codigo.Codigo == "06" && codigo.Descricao == "ETF - Entidade de investimento" && codigo.EhSistema);
            Assert.Contains(await ctx.CodigosDirpf.ToListAsync(), codigo => codigo.Codigo == "20" && codigo.Variante == "GanhoIsento" && codigo.EhSistema);
            Assert.Contains(await ctx.CodigosDirpf.ToListAsync(), codigo => codigo.Codigo == "10" && codigo.Variante == "Jcp" && codigo.EhSistema);
            Assert.Contains(await ctx.RegrasFiscais.ToListAsync(), regra => regra.Codigo == "RF.IrRegressivo.Automatico" && regra.Valor == 0m && regra.EhSistema);

            Assert.Equal(DirpfLciLca, (await ctx.Database.GetAppliedMigrationsAsync()).Last());
            Assert.True(File.Exists(banco.Caminho));
            Assert.True(new FileInfo(banco.Caminho).Length > 0);
        }
    }

    [Fact]
    public void Migrations_EstaoEmOrdem_E_ModeloNaoTemMudancasPendentes()
    {
        using var banco = new BancoTemporario();
        using var ctx = banco.CriarContexto();

        var ids = ctx.Database.GetMigrations().ToList();
        Assert.Equal(ids.OrderBy(id => id, StringComparer.Ordinal).ToList(), ids);
        Assert.Equal(Inicial, ids[0]);
        Assert.Contains(HashImportacao, ids);
        Assert.Contains(IndiceHashImportacao, ids);
        Assert.Contains(RendaFixa, ids);
        Assert.Contains(RegraCatalogo, ids);
        Assert.Equal(DataPregao, ids[^4]);
        Assert.Equal(IrrfInformado, ids[^3]);
        Assert.Equal(DirpfRendaFixaJcp, ids[^2]);
        Assert.Equal(DirpfLciLca, ids[^1]);

        for (var i = 1; i < ids.Count; i++)
        {
            var anterior = ids[i - 1][..14];
            var atual = ids[i][..14];
            Assert.True(string.CompareOrdinal(anterior, atual) < 0, $"{ids[i - 1]} deveria vir antes de {ids[i]}.");
        }

        var porAtributo = typeof(AppDbContext).Assembly
            .GetTypes()
            .Where(tipo => tipo.IsSubclassOf(typeof(Migration)) && !tipo.IsAbstract)
            .Select(tipo => tipo.GetCustomAttribute<MigrationAttribute>()?.Id)
            .ToList();

        Assert.DoesNotContain(porAtributo, id => id is null);
        Assert.Equal(ids, porAtributo.OrderBy(id => id, StringComparer.Ordinal).Select(id => id!).ToList());

        Assert.False(ctx.Database.HasPendingModelChanges());

        var item = ctx.Model.FindEntityType(typeof(ItemLancamento));
        Assert.NotNull(item);
        Assert.NotNull(item!.FindProperty(nameof(ItemLancamento.Descritivo)));
        Assert.NotNull(item.FindProperty(nameof(ItemLancamento.EhEstorno)));
        Assert.NotNull(item.FindProperty(nameof(ItemLancamento.HashImportacao)));
    }

    [Fact]
    public void AddColumn_NotNull_EmTabelaExistente_TemDefault()
    {
        var metodoUp = typeof(Migration).GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(metodoUp);

        var semDefault = new List<string>();
        var vistas = new List<string>();
        foreach (var tipo in typeof(AppDbContext).Assembly.GetTypes()
                     .Where(tipo => tipo.IsSubclassOf(typeof(Migration)) && !tipo.IsAbstract))
        {
            var migration = (Migration)Activator.CreateInstance(tipo)!;
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.Sqlite");
            metodoUp!.Invoke(migration, [builder]);

            foreach (var coluna in builder.Operations.OfType<AddColumnOperation>())
            {
                var nome = $"{tipo.Name}.{coluna.Table}.{coluna.Name}";
                vistas.Add(nome);
                if (coluna.IsNullable || coluna.DefaultValue is not null || coluna.DefaultValueSql is not null)
                    continue;

                semDefault.Add(nome);
            }
        }

        Assert.Contains("AddEhEstornoItemFatura.ItensLancamento.EhEstorno", vistas);
        Assert.Contains("AddDescritivoItemFatura.ItensLancamento.Descritivo", vistas);
        Assert.Contains("AddPagoToTransacao.Transacoes.Pago", vistas);
        Assert.Contains("AddItensFatura.Categorias.PermiteItensFatura", vistas);
        Assert.Contains("AddPagamentoCompraCesta.ComprasCesta.ValorDinheiro", vistas);
        Assert.True(semDefault.Count == 0, "NOT NULL sem default no SQLite: " + string.Join(", ", semDefault));
    }

    private static async Task InserirAsync(AppDbContext ctx, string sql)
    {
        var afetadas = await ctx.Database.ExecuteSqlRawAsync(sql);
        Assert.Equal(1, afetadas);
    }

    private static async Task<long> EscalarAsync(AppDbContext ctx, string sql)
    {
        var conexao = ctx.Database.GetDbConnection();
        if (conexao.State != ConnectionState.Open)
            await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        var valor = await comando.ExecuteScalarAsync();
        return Convert.ToInt64(valor);
    }

    private static async Task<List<string>> ColunasAsync(AppDbContext ctx, string tabela)
    {
        var conexao = ctx.Database.GetDbConnection();
        if (conexao.State != ConnectionState.Open)
            await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT name FROM pragma_table_info($tabela) ORDER BY cid";
        var parametro = comando.CreateParameter();
        parametro.ParameterName = "$tabela";
        parametro.Value = tabela;
        comando.Parameters.Add(parametro);

        var colunas = new List<string>();
        await using var leitor = await comando.ExecuteReaderAsync();
        while (await leitor.ReadAsync())
            colunas.Add(leitor.GetString(0));

        return colunas;
    }

    private sealed class BancoTemporario : IDisposable
    {
        private readonly SqliteConnection _conexao;

        public BancoTemporario()
        {
            Caminho = Path.Combine(Path.GetTempPath(), $"orcafacil-mig-{Guid.NewGuid():N}.db");
            _conexao = new SqliteConnection($"Data Source={Caminho}");
            _conexao.Open();
        }

        public string Caminho { get; }

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
            if (File.Exists(Caminho))
                File.Delete(Caminho);
        }
    }
}
