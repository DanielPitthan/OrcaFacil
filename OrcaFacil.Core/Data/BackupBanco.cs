using System.Data;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace OrcaFacil.Core.Data;

/// <summary>
/// Cópia consistente do SQLite, inclusive o que ainda está no WAL.
/// Não apaga backups antigos. Não faz nada se não houver migration pendente.
/// </summary>
public static class BackupBanco
{
    public static async Task<string?> CriarSeMigrationPendenteAsync(
        AppDbContext context,
        string versaoAplicativo,
        CancellationToken cancellationToken = default,
        DateTime? agoraUtc = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var pendentes = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pendentes.Count == 0)
            return null;

        var origem = Origem(context);
        if (origem is null)
            return null;

        if (!await TemTabelaDeUsuarioAsync(context, cancellationToken))
            return null;

        var pasta = Path.Combine(Path.GetDirectoryName(origem)! , "backups");
        Directory.CreateDirectory(pasta);
        var nome = MontarNome(versaoAplicativo, pendentes[0], agoraUtc ?? DateTime.UtcNow);
        var destino = CaminhoLivre(Path.Combine(pasta, nome));

        var conexao = (SqliteConnection)context.Database.GetDbConnection();
        var abriuAqui = conexao.State != ConnectionState.Open;
        if (abriuAqui)
            await conexao.OpenAsync(cancellationToken);

        try
        {
            await using var alvo = new SqliteConnection($"Data Source={destino}");
            await alvo.OpenAsync(cancellationToken);
            conexao.BackupDatabase(alvo);
        }
        catch
        {
            ApagarTentativaIncompleta(destino);
            throw;
        }
        finally
        {
            if (abriuAqui)
                await conexao.CloseAsync();
        }

        return destino;
    }

    private static string? Origem(AppDbContext context)
    {
        var texto = context.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        var fonte = new SqliteConnectionStringBuilder(texto).DataSource;
        if (string.IsNullOrWhiteSpace(fonte) || fonte == ":memory:" || !File.Exists(fonte))
            return null;

        return Path.GetFullPath(fonte);
    }

    private static async Task<bool> TemTabelaDeUsuarioAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var conexao = context.Database.GetDbConnection();
        var abriuAqui = conexao.State != ConnectionState.Open;
        if (abriuAqui)
            await conexao.OpenAsync(cancellationToken);

        try
        {
            await using var comando = conexao.CreateCommand();
            comando.CommandText = """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'table' AND name NOT LIKE 'sqlite_%' AND name <> '__EFMigrationsHistory'
                """;
            var total = Convert.ToInt64(await comando.ExecuteScalarAsync(cancellationToken));
            return total > 0;
        }
        finally
        {
            if (abriuAqui)
                await conexao.CloseAsync();
        }
    }

    internal static string MontarNome(string versaoAplicativo, string primeiraPendente, DateTime agoraUtc)
    {
        var carimbo = agoraUtc.ToUniversalTime().ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
        var versao = Limpar(string.IsNullOrWhiteSpace(versaoAplicativo) ? "desconhecida" : versaoAplicativo.Trim());
        var migration = Limpar(primeiraPendente);
        return $"orcafacil-backup-{carimbo}-{versao}-antes-{migration}.db";
    }

    private static string Limpar(string texto)
    {
        var chars = texto.Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '_').ToArray();
        var limpo = new string(chars).Trim('_');
        return limpo.Length == 0 ? "x" : limpo;
    }

    private static void ApagarTentativaIncompleta(string destino)
    {
        foreach (var caminho in new[] { destino, destino + "-wal", destino + "-shm" })
        {
            if (File.Exists(caminho))
                File.Delete(caminho);
        }
    }

    private static string CaminhoLivre(string caminho)
    {
        if (!File.Exists(caminho))
            return caminho;

        var pasta = Path.GetDirectoryName(caminho)!;
        var nome = Path.GetFileNameWithoutExtension(caminho);
        var extensao = Path.GetExtension(caminho);
        for (var i = 2; i < 1000; i++)
        {
            var candidato = Path.Combine(pasta, $"{nome}-{i}{extensao}");
            if (!File.Exists(candidato))
                return candidato;
        }

        throw new IOException($"Já existe um backup com o nome {Path.GetFileName(caminho)}.");
    }
}
