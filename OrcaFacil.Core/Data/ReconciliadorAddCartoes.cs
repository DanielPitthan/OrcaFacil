using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace OrcaFacil.Core.Data;

/// <summary>
/// A migration AddCartoes faz COMMIT antes de <c>PRAGMA foreign_keys = 0</c>.
/// Se o processo morre nesse intervalo, o histórico não tem a migration e o
/// retry quebra no ADD de CartaoId. Aqui o estado parcial é reconhecido:
/// coluna vazia é desfeita para a migration rodar de novo; coluna com valor
/// não é apagada e a migração não é repetida.
/// </summary>
public static class ReconciliadorAddCartoes
{
    public const string MigrationId = "20261015194450_AddCartoes";

    private static readonly (string Tabela, string Coluna)[] ColunasNovas =
    [
        ("Transacoes", "CartaoId"),
        ("ComprasCesta", "CartaoBeneficioId"),
        ("ComprasCesta", "CartaoCreditoId"),
        ("ComprasCesta", "ItemFaturaId")
    ];

    private static readonly string[] IndicesNovos =
    [
        "IX_Transacoes_CartaoId",
        "IX_ComprasCesta_CartaoBeneficioId",
        "IX_ComprasCesta_CartaoCreditoId",
        "IX_ComprasCesta_ItemFaturaId",
        "IX_Cartoes_Nome",
        "IX_RecargasCartao_CartaoId_Data"
    ];

    public static async Task<ResultadoReconciliacao> ReconciliarSeParcialAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await HistoricoContemAsync(context, cancellationToken))
            return ResultadoReconciliacao.Nada;

        var tabelas = await TabelasAsync(context, cancellationToken);
        var cartaoId = await ColunaExisteAsync(context, "Transacoes", "CartaoId", cancellationToken);
        var cartoes = tabelas.Contains("Cartoes");
        var temp = tabelas.Contains("ef_temp_Transacoes") || tabelas.Contains("ef_temp_ComprasCesta");
        if (!cartaoId && !cartoes && !temp)
            return ResultadoReconciliacao.Nada;

        if (await SchemaDaMigrationEstaCompletoAsync(context, tabelas, cancellationToken) && !temp)
        {
            await InserirHistoricoAsync(context, cancellationToken);
            return new ResultadoReconciliacao(true, false, null);
        }

        if (tabelas.Contains("ef_temp_Transacoes") && !tabelas.Contains("Transacoes"))
            return Bloqueio("A migration AddCartoes parou no meio da troca de Transacoes. A migração não foi repetida para não perder lançamentos. O banco não foi apagado.");

        if (tabelas.Contains("ef_temp_ComprasCesta") && !tabelas.Contains("ComprasCesta"))
            return Bloqueio("A migration AddCartoes parou no meio da troca de ComprasCesta. A migração não foi repetida para não perder lançamentos. O banco não foi apagado.");

        if (cartoes && await ContagemTabelaAsync(context, "Cartoes", cancellationToken) > 0)
            return Bloqueio("A migration AddCartoes parou no meio e a tabela Cartoes já tem linhas. A migração não foi repetida para não apagar esses cartões. O banco não foi apagado.");

        var ocupada = await ColunaNovaOcupadaAsync(context, cancellationToken);
        if (ocupada is not null)
        {
            var estadoTabela = cartoes ? "a tabela Cartoes não está pronta" : "a tabela Cartoes não existe";
            return Bloqueio($"A migration AddCartoes parou no meio: a coluna {ocupada} existe e {estadoTabela}. Há valores nessa coluna, então ela não foi removida e a migração não foi repetida, para não perder o vínculo nem falhar de novo com coluna duplicada. O banco não foi apagado.");
        }

        await DesfazerObjetosParciaisAsync(context, tabelas, cancellationToken);
        return new ResultadoReconciliacao(true, false, null);
    }

    private static ResultadoReconciliacao Bloqueio(string motivo) => new(false, true, motivo);

    private static async Task<bool> SchemaDaMigrationEstaCompletoAsync(
        AppDbContext context,
        HashSet<string> tabelas,
        CancellationToken cancellationToken)
    {
        if (!tabelas.Contains("Cartoes") || !tabelas.Contains("RecargasCartao"))
            return false;

        foreach (var (tabela, coluna) in ColunasNovas)
        {
            if (!await ColunaExisteAsync(context, tabela, coluna, cancellationToken))
                return false;
        }

        var indices = await NomesAsync(context, "SELECT name FROM sqlite_master WHERE type = 'index'", cancellationToken);
        return IndicesNovos.All(indices.Contains);
    }

    private static async Task DesfazerObjetosParciaisAsync(
        AppDbContext context,
        HashSet<string> tabelas,
        CancellationToken cancellationToken)
    {
        var conexao = context.Database.GetDbConnection();
        var abriuAqui = conexao.State != ConnectionState.Open;
        if (abriuAqui)
            await conexao.OpenAsync(cancellationToken);

        try
        {
            await ExecutarAsync(conexao, null, "PRAGMA foreign_keys = OFF", cancellationToken);
            await using var transacao = await conexao.BeginTransactionAsync(cancellationToken);
            try
            {
                foreach (var indice in IndicesNovos)
                {
                    if (await ExisteAsync(conexao, transacao, "SELECT 1 FROM sqlite_master WHERE type = 'index' AND name = $n", indice, cancellationToken))
                        await ExecutarAsync(conexao, transacao, $"DROP INDEX \"{indice}\"", cancellationToken);
                }

                foreach (var temp in new[] { "ef_temp_RecargasCartao", "ef_temp_Cartoes", "ef_temp_ComprasCesta", "ef_temp_Transacoes" })
                {
                    if (tabelas.Contains(temp))
                        await ExecutarAsync(conexao, transacao, $"DROP TABLE \"{temp}\"", cancellationToken);
                }

                if (tabelas.Contains("RecargasCartao") && await ContagemAsync(conexao, transacao, "RecargasCartao", cancellationToken) == 0)
                    await ExecutarAsync(conexao, transacao, "DROP TABLE \"RecargasCartao\"", cancellationToken);

                if (tabelas.Contains("Cartoes") && await ContagemAsync(conexao, transacao, "Cartoes", cancellationToken) == 0)
                    await ExecutarAsync(conexao, transacao, "DROP TABLE \"Cartoes\"", cancellationToken);

                foreach (var (tabela, coluna) in ColunasNovas.Reverse())
                {
                    if (await ColunaExisteAsync(conexao, transacao, tabela, coluna, cancellationToken))
                        await ExecutarAsync(conexao, transacao, $"ALTER TABLE \"{tabela}\" DROP COLUMN \"{coluna}\"", cancellationToken);
                }

                await transacao.CommitAsync(cancellationToken);
            }
            catch
            {
                await transacao.RollbackAsync(cancellationToken);
                throw;
            }
            finally
            {
                await ExecutarAsync(conexao, null, "PRAGMA foreign_keys = ON", cancellationToken);
            }
        }
        finally
        {
            if (abriuAqui)
                await conexao.CloseAsync();
        }
    }

    private static async Task<string?> ColunaNovaOcupadaAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        foreach (var (tabela, coluna) in ColunasNovas)
        {
            if (!await ColunaExisteAsync(context, tabela, coluna, cancellationToken))
                continue;

            var total = await EscalarAsync(context, $"SELECT COUNT(*) FROM \"{tabela}\" WHERE \"{coluna}\" IS NOT NULL", cancellationToken);
            if (total > 0)
                return $"{tabela}.{coluna}";
        }

        return null;
    }

    private static async Task InserirHistoricoAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var versao = await EscalarTextoAsync(
            context,
            "SELECT ProductVersion FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 1",
            cancellationToken);
        if (string.IsNullOrWhiteSpace(versao))
            versao = "10.0.11";

        var conexao = context.Database.GetDbConnection();
        var abriuAqui = conexao.State != ConnectionState.Open;
        if (abriuAqui)
            await conexao.OpenAsync(cancellationToken);

        try
        {
            await using var comando = conexao.CreateCommand();
            comando.CommandText = """
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ($id, $versao)
                """;
            var id = comando.CreateParameter();
            id.ParameterName = "$id";
            id.Value = MigrationId;
            comando.Parameters.Add(id);
            var produto = comando.CreateParameter();
            produto.ParameterName = "$versao";
            produto.Value = versao;
            comando.Parameters.Add(produto);
            await comando.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (abriuAqui)
                await conexao.CloseAsync();
        }
    }

    private static async Task<bool> HistoricoContemAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var total = await EscalarAsync(
            context,
            $"SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId = '{MigrationId}'",
            cancellationToken);
        return total > 0;
    }

    private static Task<long> ContagemTabelaAsync(AppDbContext context, string tabela, CancellationToken cancellationToken) =>
        EscalarAsync(context, $"SELECT COUNT(*) FROM \"{tabela}\"", cancellationToken);

    private static async Task<HashSet<string>> TabelasAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var nomes = await NomesAsync(
            context,
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'",
            cancellationToken);
        return nomes;
    }

    private static async Task<bool> ColunaExisteAsync(AppDbContext context, string tabela, string coluna, CancellationToken cancellationToken)
    {
        var conexao = context.Database.GetDbConnection();
        var abriuAqui = conexao.State != ConnectionState.Open;
        if (abriuAqui)
            await conexao.OpenAsync(cancellationToken);
        try
        {
            return await ColunaExisteAsync(conexao, null, tabela, coluna, cancellationToken);
        }
        finally
        {
            if (abriuAqui)
                await conexao.CloseAsync();
        }
    }

    private static async Task<bool> ColunaExisteAsync(DbConnection conexao, DbTransaction? transacao, string tabela, string coluna, CancellationToken cancellationToken)
    {
        await using var comando = conexao.CreateCommand();
        comando.Transaction = transacao;
        comando.CommandText = "SELECT 1 FROM pragma_table_info($tabela) WHERE name = $coluna";
        var tabelaParam = comando.CreateParameter();
        tabelaParam.ParameterName = "$tabela";
        tabelaParam.Value = tabela;
        comando.Parameters.Add(tabelaParam);
        var colunaParam = comando.CreateParameter();
        colunaParam.ParameterName = "$coluna";
        colunaParam.Value = coluna;
        comando.Parameters.Add(colunaParam);
        var valor = await comando.ExecuteScalarAsync(cancellationToken);
        return valor is not null and not DBNull;
    }

    private static Task<HashSet<string>> NomesAsync(AppDbContext context, string sql, CancellationToken cancellationToken) =>
        LerNomesAsync(context, sql, cancellationToken);

    private static async Task<HashSet<string>> LerNomesAsync(AppDbContext context, string sql, CancellationToken cancellationToken)
    {
        var conexao = context.Database.GetDbConnection();
        var abriuAqui = conexao.State != ConnectionState.Open;
        if (abriuAqui)
            await conexao.OpenAsync(cancellationToken);

        try
        {
            var nomes = new HashSet<string>(StringComparer.Ordinal);
            await using var comando = conexao.CreateCommand();
            comando.CommandText = sql;
            await using var leitor = await comando.ExecuteReaderAsync(cancellationToken);
            while (await leitor.ReadAsync(cancellationToken))
                nomes.Add(leitor.GetString(0));
            return nomes;
        }
        finally
        {
            if (abriuAqui)
                await conexao.CloseAsync();
        }
    }

    private static async Task<bool> ExisteAsync(DbConnection conexao, DbTransaction transacao, string sql, string nome, CancellationToken cancellationToken)
    {
        await using var comando = conexao.CreateCommand();
        comando.Transaction = transacao;
        comando.CommandText = sql;
        var parametro = comando.CreateParameter();
        parametro.ParameterName = "$n";
        parametro.Value = nome;
        comando.Parameters.Add(parametro);
        var valor = await comando.ExecuteScalarAsync(cancellationToken);
        return valor is not null and not DBNull;
    }

    private static async Task<long> ContagemAsync(DbConnection conexao, DbTransaction transacao, string tabela, CancellationToken cancellationToken)
    {
        await using var comando = conexao.CreateCommand();
        comando.Transaction = transacao;
        comando.CommandText = $"SELECT COUNT(*) FROM \"{tabela}\"";
        return Convert.ToInt64(await comando.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task ExecutarAsync(DbConnection conexao, DbTransaction? transacao, string sql, CancellationToken cancellationToken)
    {
        await using var comando = conexao.CreateCommand();
        comando.Transaction = transacao;
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> EscalarAsync(AppDbContext context, string sql, CancellationToken cancellationToken)
    {
        var texto = await EscalarTextoAsync(context, sql, cancellationToken);
        return long.TryParse(texto, out var valor) ? valor : 0;
    }

    private static async Task<string?> EscalarTextoAsync(AppDbContext context, string sql, CancellationToken cancellationToken)
    {
        var conexao = context.Database.GetDbConnection();
        var abriuAqui = conexao.State != ConnectionState.Open;
        if (abriuAqui)
            await conexao.OpenAsync(cancellationToken);

        try
        {
            await using var comando = conexao.CreateCommand();
            comando.CommandText = sql;
            var valor = await comando.ExecuteScalarAsync(cancellationToken);
            return valor is null or DBNull ? null : Convert.ToString(valor);
        }
        catch (DbException)
        {
            return null;
        }
        finally
        {
            if (abriuAqui)
                await conexao.CloseAsync();
        }
    }
}

public readonly record struct ResultadoReconciliacao(bool Alterou, bool Bloqueado, string? Motivo)
{
    public static ResultadoReconciliacao Nada { get; } = new(false, false, null);
}
