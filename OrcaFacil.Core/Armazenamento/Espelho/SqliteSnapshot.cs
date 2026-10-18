using Microsoft.Data.Sqlite;

namespace OrcaFacil.Core.Armazenamento.Espelho;

internal enum ResultadoPragmaSnapshot
{
    Ok,
    IntegrityCheck,
    ForeignKeyCheck
}

/// <summary>
/// SQLite do snapshot. Não abre o banco vivo para escrita e não apaga arquivo.
/// </summary>
internal static class SqliteSnapshot
{
    public static async Task VacuumIntoAsync(string origem, string destino, CancellationToken cancellationToken)
    {
        await using var conexao = await AbrirAsync(origem, SqliteOpenMode.ReadWrite, cancellationToken);
        await using var vacuum = conexao.CreateCommand();
        vacuum.CommandText = "VACUUM INTO $destino;";
        vacuum.Parameters.AddWithValue("$destino", destino);
        await vacuum.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task<ResultadoPragmaSnapshot> VerificarAsync(string caminho, CancellationToken cancellationToken)
    {
        await using var conexao = await AbrirAsync(caminho, SqliteOpenMode.ReadOnly, cancellationToken);
        if (!await IntegrityOkAsync(conexao, cancellationToken))
            return ResultadoPragmaSnapshot.IntegrityCheck;
        if (!await ForeignKeyOkAsync(conexao, cancellationToken))
            return ResultadoPragmaSnapshot.ForeignKeyCheck;
        return ResultadoPragmaSnapshot.Ok;
    }

    public static async Task<IReadOnlyList<string>> LerHistoricoAsync(string caminho, CancellationToken cancellationToken)
    {
        await using var conexao = await AbrirAsync(caminho, SqliteOpenMode.ReadOnly, cancellationToken);
        await using var existe = conexao.CreateCommand();
        existe.CommandText = """
            SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory' LIMIT 1
            """;
        if (await existe.ExecuteScalarAsync(cancellationToken) is null)
            return [];

        await using var historico = conexao.CreateCommand();
        historico.CommandText = """
            SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId"
            """;
        var ids = new List<string>();
        await using var leitor = await historico.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            if (!leitor.IsDBNull(0))
                ids.Add(leitor.GetString(0));
        }

        return ids;
    }

    public static async Task<bool> TemTabelaAsync(string caminho, string tabela, CancellationToken cancellationToken)
    {
        await using var conexao = await AbrirAsync(caminho, SqliteOpenMode.ReadOnly, cancellationToken);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $tabela LIMIT 1";
        var parametro = comando.CreateParameter();
        parametro.ParameterName = "$tabela";
        parametro.Value = tabela;
        comando.Parameters.Add(parametro);
        return await comando.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<SqliteConnection> AbrirAsync(
        string caminho,
        SqliteOpenMode modo,
        CancellationToken cancellationToken)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = caminho,
            Mode = modo,
            Pooling = false
        };
        var conexao = new SqliteConnection(builder.ConnectionString);
        try
        {
            await conexao.OpenAsync(cancellationToken);
            await using var busy = conexao.CreateCommand();
            busy.CommandText = "PRAGMA busy_timeout = 5000;";
            await busy.ExecuteNonQueryAsync(cancellationToken);
            return conexao;
        }
        catch
        {
            await conexao.DisposeAsync();
            throw;
        }
    }

    private static async Task<bool> IntegrityOkAsync(SqliteConnection conexao, CancellationToken cancellationToken)
    {
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "PRAGMA integrity_check;";
        await using var leitor = await comando.ExecuteReaderAsync(cancellationToken);
        var viuOk = false;
        while (await leitor.ReadAsync(cancellationToken))
        {
            var texto = leitor.IsDBNull(0) ? "" : leitor.GetString(0).Trim();
            if (!texto.Equals("ok", StringComparison.OrdinalIgnoreCase))
                return false;
            viuOk = true;
        }

        return viuOk;
    }

    private static async Task<bool> ForeignKeyOkAsync(SqliteConnection conexao, CancellationToken cancellationToken)
    {
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "PRAGMA foreign_key_check;";
        await using var leitor = await comando.ExecuteReaderAsync(cancellationToken);
        return !await leitor.ReadAsync(cancellationToken);
    }
}
