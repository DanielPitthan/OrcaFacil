using Microsoft.Data.Sqlite;
using OrcaFacil.Core.Armazenamento;

namespace OrcaFacil.Core.Armazenamento.Partida;

/// <summary>
/// No SQLite já descriptografado, grava só o nome da foto.
/// Caminho que não vira nome permanece como está. Não apaga linha e não abre o banco vivo.
/// </summary>
internal static class NormalizacaoFotoEspelho
{
    public static async Task AplicarAsync(string caminhoSqlite, CancellationToken cancellationToken)
    {
        await using var conexao = await AbrirAsync(caminhoSqlite, cancellationToken);
        if (!await TemColunaAsync(conexao, cancellationToken))
            return;

        var pendentes = new List<(long Id, string Nome)>();
        await using (var leitura = conexao.CreateCommand())
        {
            leitura.CommandText = """
                SELECT Id, FotoPath
                FROM MembrosFamilia
                WHERE FotoPath IS NOT NULL
                """;
            await using var leitor = await leitura.ExecuteReaderAsync(cancellationToken);
            while (await leitor.ReadAsync(cancellationToken))
            {
                if (leitor.IsDBNull(1))
                    continue;

                var atual = leitor.GetString(1);
                if (FotoPath.EhSomenteNomeArquivo(atual))
                    continue;

                string nome;
                try
                {
                    nome = FotoPath.ExtrairNomeArquivo(atual);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (!string.Equals(nome, atual, StringComparison.Ordinal))
                    pendentes.Add((leitor.GetInt64(0), nome));
            }
        }

        foreach (var (id, nome) in pendentes)
        {
            await using var gravar = conexao.CreateCommand();
            gravar.CommandText = """
                UPDATE MembrosFamilia
                SET FotoPath = $nome
                WHERE Id = $id
                """;
            gravar.Parameters.AddWithValue("$nome", nome);
            gravar.Parameters.AddWithValue("$id", id);
            await gravar.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<bool> TemColunaAsync(SqliteConnection conexao, CancellationToken cancellationToken)
    {
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            SELECT 1
            FROM pragma_table_info('MembrosFamilia')
            WHERE name = 'FotoPath'
            LIMIT 1
            """;
        return await comando.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<SqliteConnection> AbrirAsync(string caminho, CancellationToken cancellationToken)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = caminho,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        };
        var conexao = new SqliteConnection(builder.ConnectionString);
        await conexao.OpenAsync(cancellationToken);
        return conexao;
    }
}
