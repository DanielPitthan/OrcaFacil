using System.Data;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace OrcaFacil.Core.Data;

/// <summary>
/// Compara o model do EF e as migrations conhecidas com o SQLite já existente.
/// Só lê. Não migra, não grava e não apaga.
/// </summary>
public static class VerificadorIntegridadeBanco
{
    public const string TabelaHistorico = "__EFMigrationsHistory";

    public static async Task<RelatorioIntegridadeBanco> VerificarAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var real = await LerEsquemaAsync(context, cancellationToken);
        var esperado = EsperadoPeloModelo(context);
        var conhecidas = context.Database.GetMigrations().ToList();
        var objetosPorMigration = EfeitosPorMigration();

        var tabelasFaltando = esperado.Tabelas.Except(real.Tabelas, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var colunasFaltando = esperado.Colunas.Except(real.Colunas, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var indicesFaltando = esperado.Indices.Except(real.Indices, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        var noHistorico = real.Migrations.ToHashSet(StringComparer.Ordinal);
        var conhecidasSet = conhecidas.ToHashSet(StringComparer.Ordinal);
        var ausentes = conhecidas.Where(id => !noHistorico.Contains(id)).ToList();
        var desconhecidas = real.Migrations.Where(id => !conhecidasSet.Contains(id)).Order(StringComparer.Ordinal).ToList();

        var pelaMetade = new List<string>();
        var historicoIncompleto = new List<string>();
        var posicao = new Dictionary<string, int>(conhecidas.Count, StringComparer.Ordinal);
        for (var i = 0; i < conhecidas.Count; i++)
            posicao[conhecidas[i]] = i;

        foreach (var id in conhecidas)
        {
            if (!objetosPorMigration.TryGetValue(id, out var efeitos) || efeitos.Criados.Count == 0)
                continue;

            // Índice ou coluna trocados por uma migration posterior deixam de ser exigidos
            // quando essa migration já está no histórico. Sem isso, AddItensFatura pareceria
            // pela metade depois de AddIndiceHashImportacaoItemFatura dropar IX_ItensLancamento_TransacaoId.
            var exigidos = efeitos.Criados
                .Where(objeto => !RemovidoPorMigrationPosterior(id, objeto, conhecidas, posicao, objetosPorMigration, noHistorico))
                .ToList();
            if (exigidos.Count == 0)
                continue;

            var presentes = exigidos.Count(objeto => real.Contem(objeto));
            var faltando = exigidos.Count - presentes;
            if (presentes > 0 && faltando > 0)
                pelaMetade.Add(id);

            if (noHistorico.Contains(id) && faltando > 0)
                historicoIncompleto.Add(id);
        }

        return new RelatorioIntegridadeBanco
        {
            TabelasExistentes = real.Tabelas.Order(StringComparer.Ordinal).ToList(),
            TabelasFaltando = tabelasFaltando,
            ColunasFaltando = colunasFaltando,
            IndicesFaltando = indicesFaltando,
            MigrationsNoHistorico = real.Migrations,
            MigrationsAusentesNoHistorico = ausentes,
            MigrationsDesconhecidasNoHistorico = desconhecidas,
            MigrationsPelaMetade = pelaMetade,
            MigrationsNoHistoricoComObjetoFaltando = historicoIncompleto
        };
    }

    /// <summary>
    /// Relatório quando o arquivo não pôde ser lido. Não abre o SQLite.
    /// Lista o que o model espera como falta, porque nada pôde ser confirmado.
    /// </summary>
    public static RelatorioIntegridadeBanco RelatorioIlegivel(AppDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var esperado = EsperadoPeloModelo(context);
            var migrations = context.Database.GetMigrations().ToList();
            return new RelatorioIntegridadeBanco
            {
                TabelasFaltando = esperado.Tabelas.Order(StringComparer.Ordinal).ToList(),
                ColunasFaltando = esperado.Colunas.Order(StringComparer.Ordinal).ToList(),
                IndicesFaltando = esperado.Indices.Order(StringComparer.Ordinal).ToList(),
                MigrationsAusentesNoHistorico = migrations
            };
        }
        catch (Exception)
        {
            return RelatorioIntegridadeBanco.IlegivelMinimo;
        }
    }

    private static Esperado EsperadoPeloModelo(AppDbContext context)
    {
        var esperado = new Esperado();
        esperado.Tabelas.Add(TabelaHistorico);
        esperado.Colunas.Add($"{TabelaHistorico}.MigrationId");
        esperado.Colunas.Add($"{TabelaHistorico}.ProductVersion");

        foreach (var entidade in context.Model.GetEntityTypes())
        {
            var tabela = entidade.GetTableName();
            if (tabela is null)
                continue;

            esperado.Tabelas.Add(tabela);
            var store = StoreObjectIdentifier.Table(tabela, entidade.GetSchema());
            foreach (var propriedade in entidade.GetProperties())
            {
                var coluna = propriedade.GetColumnName(store);
                if (coluna is not null)
                    esperado.Colunas.Add($"{tabela}.{coluna}");
            }

            foreach (var indice in entidade.GetIndexes())
            {
                var nome = indice.GetDatabaseName();
                if (nome is not null)
                    esperado.Indices.Add(nome);
            }
        }

        return esperado;
    }

    private static async Task<EsquemaReal> LerEsquemaAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var conexao = context.Database.GetDbConnection();
        var abriuAqui = conexao.State != ConnectionState.Open;
        if (abriuAqui)
            await conexao.OpenAsync(cancellationToken);

        try
        {
            var tabelas = await LerTextoAsync(
                conexao,
                "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name",
                cancellationToken);
            var indices = await LerTextoAsync(
                conexao,
                "SELECT name FROM sqlite_master WHERE type = 'index' AND name NOT LIKE 'sqlite_%' ORDER BY name",
                cancellationToken);

            var colunas = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tabela in tabelas)
            {
                foreach (var coluna in await LerColunasAsync(conexao, tabela, cancellationToken))
                    colunas.Add($"{tabela}.{coluna}");
            }

            var migrations = new List<string>();
            if (tabelas.Contains(TabelaHistorico, StringComparer.Ordinal))
            {
                migrations = await LerTextoAsync(
                    conexao,
                    $"SELECT \"MigrationId\" FROM \"{TabelaHistorico}\" ORDER BY \"MigrationId\"",
                    cancellationToken);
            }

            return new EsquemaReal(tabelas.ToHashSet(StringComparer.Ordinal), colunas, indices.ToHashSet(StringComparer.Ordinal), migrations);
        }
        finally
        {
            if (abriuAqui)
                await conexao.CloseAsync();
        }
    }

    private static async Task<List<string>> LerColunasAsync(System.Data.Common.DbConnection conexao, string tabela, CancellationToken cancellationToken)
    {
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT name FROM pragma_table_info($tabela) ORDER BY cid";
        var parametro = comando.CreateParameter();
        parametro.ParameterName = "$tabela";
        parametro.Value = tabela;
        comando.Parameters.Add(parametro);
        return await LerAsync(comando, cancellationToken);
    }

    private static async Task<List<string>> LerTextoAsync(System.Data.Common.DbConnection conexao, string sql, CancellationToken cancellationToken)
    {
        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        return await LerAsync(comando, cancellationToken);
    }

    private static async Task<List<string>> LerAsync(System.Data.Common.DbCommand comando, CancellationToken cancellationToken)
    {
        var itens = new List<string>();
        await using var leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            if (!leitor.IsDBNull(0))
                itens.Add(leitor.GetString(0));
        }

        return itens;
    }

    private static bool RemovidoPorMigrationPosterior(
        string id,
        ObjetoSchema objeto,
        List<string> conhecidas,
        Dictionary<string, int> posicao,
        Dictionary<string, EfeitosMigration> efeitos,
        HashSet<string> noHistorico)
    {
        var inicio = posicao[id];
        for (var i = inicio + 1; i < conhecidas.Count; i++)
        {
            var posterior = conhecidas[i];
            if (!noHistorico.Contains(posterior))
                continue;

            if (efeitos.TryGetValue(posterior, out var efeito) && efeito.Remove(objeto))
                return true;
        }

        return false;
    }

    private static Dictionary<string, EfeitosMigration> EfeitosPorMigration()
    {
        var metodoUp = typeof(Migration).GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic);
        var mapa = new Dictionary<string, EfeitosMigration>(StringComparer.Ordinal);
        if (metodoUp is null)
            return mapa;

        foreach (var tipo in typeof(AppDbContext).Assembly.GetTypes()
                     .Where(tipo => tipo.IsSubclassOf(typeof(Migration)) && !tipo.IsAbstract))
        {
            var id = tipo.GetCustomAttribute<MigrationAttribute>()?.Id;
            if (id is null)
                continue;

            var migration = (Migration)Activator.CreateInstance(tipo)!;
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.Sqlite");
            metodoUp.Invoke(migration, [builder]);

            var efeitos = new EfeitosMigration();
            foreach (var operacao in builder.Operations)
            {
                switch (operacao)
                {
                    case CreateTableOperation criar:
                        efeitos.Criados.Add(new ObjetoSchema(TipoObjeto.Tabela, criar.Name, null));
                        foreach (var coluna in criar.Columns)
                            efeitos.Criados.Add(new ObjetoSchema(TipoObjeto.Coluna, criar.Name, coluna.Name));
                        break;
                    case AddColumnOperation coluna:
                        efeitos.Criados.Add(new ObjetoSchema(TipoObjeto.Coluna, coluna.Table, coluna.Name));
                        break;
                    case CreateIndexOperation indice when indice.Name is not null && indice.Table is not null:
                        efeitos.Criados.Add(new ObjetoSchema(TipoObjeto.Indice, indice.Table, indice.Name));
                        break;
                    case DropTableOperation tabela:
                        efeitos.RegistrarRemocao(new ObjetoSchema(TipoObjeto.Tabela, tabela.Name, null));
                        break;
                    case DropColumnOperation coluna:
                        efeitos.RegistrarRemocao(new ObjetoSchema(TipoObjeto.Coluna, coluna.Table, coluna.Name));
                        break;
                    case DropIndexOperation indice when indice.Name is not null && indice.Table is not null:
                        efeitos.RegistrarRemocao(new ObjetoSchema(TipoObjeto.Indice, indice.Table, indice.Name));
                        break;
                }
            }

            efeitos.Criados.RemoveAll(efeitos.Remove);
            mapa[id] = efeitos;
        }

        return mapa;
    }

    private sealed class EfeitosMigration
    {
        public List<ObjetoSchema> Criados { get; } = [];

        private readonly HashSet<ObjetoSchema> _removidos = [];

        private readonly HashSet<string> _tabelasRemovidas = new(StringComparer.Ordinal);

        public void RegistrarRemocao(ObjetoSchema objeto)
        {
            _removidos.Add(objeto);
            if (objeto.Tipo == TipoObjeto.Tabela)
                _tabelasRemovidas.Add(objeto.Tabela);
        }

        public bool Remove(ObjetoSchema objeto) =>
            _removidos.Contains(objeto)
            || (objeto.Tipo != TipoObjeto.Tabela && _tabelasRemovidas.Contains(objeto.Tabela));
    }

    private enum TipoObjeto
    {
        Tabela,
        Coluna,
        Indice
    }

    private readonly record struct ObjetoSchema(TipoObjeto Tipo, string Tabela, string? Nome);

    private sealed class Esperado
    {
        public HashSet<string> Tabelas { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Colunas { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Indices { get; } = new(StringComparer.Ordinal);
    }

    private sealed class EsquemaReal
    {
        public EsquemaReal(HashSet<string> tabelas, HashSet<string> colunas, HashSet<string> indices, List<string> migrations)
        {
            Tabelas = tabelas;
            Colunas = colunas;
            Indices = indices;
            Migrations = migrations;
        }

        public HashSet<string> Tabelas { get; }
        public HashSet<string> Colunas { get; }
        public HashSet<string> Indices { get; }
        public List<string> Migrations { get; }

        public bool Contem(ObjetoSchema objeto) => objeto.Tipo switch
        {
            TipoObjeto.Tabela => Tabelas.Contains(objeto.Tabela),
            TipoObjeto.Coluna => Colunas.Contains($"{objeto.Tabela}.{objeto.Nome}"),
            TipoObjeto.Indice => objeto.Nome is not null && Indices.Contains(objeto.Nome),
            _ => false
        };
    }
}

public sealed class RelatorioIntegridadeBanco
{
    public static RelatorioIntegridadeBanco IlegivelMinimo { get; } = new()
    {
        TabelasFaltando = ["(arquivo ilegível)"],
        ColunasFaltando = ["(arquivo ilegível)"],
        IndicesFaltando = ["(arquivo ilegível)"],
        MigrationsAusentesNoHistorico = ["(histórico ilegível)"]
    };

    public IReadOnlyList<string> TabelasExistentes { get; init; } = [];
    public IReadOnlyList<string> TabelasFaltando { get; init; } = [];
    public IReadOnlyList<string> ColunasFaltando { get; init; } = [];
    public IReadOnlyList<string> IndicesFaltando { get; init; } = [];
    public IReadOnlyList<string> MigrationsNoHistorico { get; init; } = [];
    public IReadOnlyList<string> MigrationsAusentesNoHistorico { get; init; } = [];
    public IReadOnlyList<string> MigrationsDesconhecidasNoHistorico { get; init; } = [];
    public IReadOnlyList<string> MigrationsPelaMetade { get; init; } = [];
    public IReadOnlyList<string> MigrationsNoHistoricoComObjetoFaltando { get; init; } = [];

    public bool Integro =>
        TabelasFaltando.Count == 0
        && ColunasFaltando.Count == 0
        && IndicesFaltando.Count == 0
        && MigrationsAusentesNoHistorico.Count == 0
        && MigrationsDesconhecidasNoHistorico.Count == 0
        && MigrationsPelaMetade.Count == 0
        && MigrationsNoHistoricoComObjetoFaltando.Count == 0;

    public string? PrimeiraMigrationNaoRegistrada => MigrationsAusentesNoHistorico.Count == 0
        ? null
        : MigrationsAusentesNoHistorico[0];

    public string Resumo
    {
        get
        {
            if (Integro)
                return "Banco íntegro.";

            var partes = new List<string>();
            Acrescentar(partes, "Tabelas faltando", TabelasFaltando);
            Acrescentar(partes, "Colunas faltando", ColunasFaltando);
            Acrescentar(partes, "Índices faltando", IndicesFaltando);
            Acrescentar(partes, "Histórico sem", MigrationsAusentesNoHistorico);
            Acrescentar(partes, "Histórico desconhecido", MigrationsDesconhecidasNoHistorico);
            Acrescentar(partes, "Migration pela metade", MigrationsPelaMetade);
            Acrescentar(partes, "Histórico aplicado com objeto faltando", MigrationsNoHistoricoComObjetoFaltando);
            return string.Join(" ", partes);
        }
    }

    private static void Acrescentar(List<string> partes, string rotulo, IReadOnlyList<string> itens)
    {
        if (itens.Count == 0)
            return;

        const int limite = 6;
        var visiveis = itens.Count <= limite ? itens : itens.Take(limite).ToList();
        var texto = string.Join(", ", visiveis);
        if (itens.Count > limite)
            texto += $" e mais {itens.Count - limite}";

        partes.Add($"{rotulo}: {texto}.");
    }
}
