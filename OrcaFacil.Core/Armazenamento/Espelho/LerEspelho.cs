using System.Security.Cryptography;

namespace OrcaFacil.Core.Armazenamento.Espelho;

/// <summary>
/// Lê o espelho: descriptografa para um SQLite temporário, valida integridade e versão.
/// Não substitui, não apaga e não abre o banco vivo.
/// </summary>
public static class LerEspelho
{
    public static async Task<SnapshotEspelho> AbrirAsync(
        string caminhoEspelho,
        string senha,
        VersaoSchemaAplicacao versaoApp,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminhoEspelho);
        ArgumentNullException.ThrowIfNull(senha);
        ArgumentNullException.ThrowIfNull(versaoApp);
        cancellationToken.ThrowIfCancellationRequested();

        var (metadados, sqlite) = CofreEspelho.Ler(caminhoEspelho, senha);
        string? pasta = null;
        var entregue = false;
        try
        {
            pasta = Directory.CreateTempSubdirectory("orcafacil-leitura-").FullName;
            var caminhoSqlite = Path.Combine(pasta, "snapshot.db");
            await File.WriteAllBytesAsync(caminhoSqlite, sqlite, cancellationToken);

            ResultadoPragmaSnapshot pragma;
            try
            {
                pragma = await SqliteSnapshot.VerificarAsync(caminhoSqlite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                throw new ArquivoCorrompidoException();
            }

            if (pragma != ResultadoPragmaSnapshot.Ok)
                throw new ArquivoCorrompidoException();

            IReadOnlyList<string> historico;
            try
            {
                historico = await SqliteSnapshot.LerHistoricoAsync(caminhoSqlite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                throw new ArquivoCorrompidoException();
            }

            var ultima = historico.Count == 0 ? "" : historico[^1];
            if (versaoApp.Calcular(historico) != metadados.VersaoSchema
                || !string.Equals(ultima, metadados.UltimaMigration, StringComparison.Ordinal))
                throw new ArquivoCorrompidoException();

            if (metadados.VersaoSchema > versaoApp.Atual)
                throw new SchemaMaisNovoQueOAppException(metadados.VersaoSchema, versaoApp.Atual);

            entregue = true;
            return new SnapshotEspelho(pasta, caminhoSqlite, metadados);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sqlite);
            if (!entregue && pasta is not null)
                ApagarPasta(pasta);
        }
    }

    private static void ApagarPasta(string pasta)
    {
        try
        {
            if (Directory.Exists(pasta))
                Directory.Delete(pasta, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>SQLite temporário já conferido. Descartar apaga o temporário, não o espelho e não o banco vivo.</summary>
public sealed class SnapshotEspelho : IDisposable
{
    private readonly string _pasta;
    private bool _descartado;

    internal SnapshotEspelho(string pasta, string caminhoSqlite, MetadadosEspelho metadados)
    {
        _pasta = pasta;
        CaminhoSqlite = caminhoSqlite;
        Metadados = metadados;
    }

    public string CaminhoSqlite { get; }

    public MetadadosEspelho Metadados { get; }

    public void Dispose()
    {
        if (_descartado)
            return;

        _descartado = true;
        try
        {
            if (Directory.Exists(_pasta))
                Directory.Delete(_pasta, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public override string ToString() => "SnapshotEspelho";
}
