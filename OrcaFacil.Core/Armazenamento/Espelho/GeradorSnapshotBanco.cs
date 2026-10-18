using System.Security.Cryptography;

namespace OrcaFacil.Core.Armazenamento.Espelho;

/// <summary>
/// Gera um snapshot consistente do SQLite vivo (incluindo WAL) com <c>VACUUM INTO</c>,
/// confere <c>integrity_check</c> e <c>foreign_key_check</c> no arquivo novo e só então
/// cifra e publica. Falha de integridade não chama <see cref="IDestinoEspelho"/>.
/// </summary>
/// <remarks>
/// O banco vivo não é apagado nem substituído. <c>VACUUM INTO</c> grava outro arquivo.
/// A senha, a chave, o conteúdo e o caminho completo não entram em mensagem de erro.
/// Senha esquecida: <see cref="RegraSenhaEspelho.Esquecimento"/>.
/// </remarks>
public sealed class GeradorSnapshotBanco
{
    private readonly VersaoSchemaAplicacao _versao;
    private readonly int _iteracoes;

    public GeradorSnapshotBanco(VersaoSchemaAplicacao versao, int? iteracoesPbkdf2 = null)
    {
        _versao = versao ?? throw new ArgumentNullException(nameof(versao));
        _iteracoes = iteracoesPbkdf2 ?? FormatoEspelho.IteracoesPadrao;
        if (_iteracoes < FormatoEspelho.IteracoesMinimas || _iteracoes > FormatoEspelho.IteracoesMaximas)
            throw new ArgumentOutOfRangeException(nameof(iteracoesPbkdf2));
    }

    public async Task<MetadadosEspelho> GerarAsync(
        string caminhoBancoVivo,
        string senha,
        IDestinoEspelho destino,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminhoBancoVivo);
        ArgumentNullException.ThrowIfNull(senha);
        ArgumentNullException.ThrowIfNull(destino);
        RegraSenhaEspelho.Exigir(senha);
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(caminhoBancoVivo))
            throw new IntegridadeSnapshotException(IntegridadeSnapshotException.NadaPublicado);

        try
        {
            if (new FileInfo(caminhoBancoVivo).Length <= 0)
                throw new IntegridadeSnapshotException(IntegridadeSnapshotException.BancoSemHistorico);
        }
        catch (IntegridadeSnapshotException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IntegridadeSnapshotException(IntegridadeSnapshotException.NadaPublicado);
        }

        var pasta = Directory.CreateTempSubdirectory("orcafacil-snapshot-").FullName;
        try
        {
            var snapshot = Path.Combine(pasta, "snapshot.db");
            var cifrado = Path.Combine(pasta, "espelho.bin");
            try
            {
                await SqliteSnapshot.VacuumIntoAsync(caminhoBancoVivo, snapshot, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                throw new IntegridadeSnapshotException(IntegridadeSnapshotException.NadaPublicado);
            }

            if (!File.Exists(snapshot))
                throw new IntegridadeSnapshotException(IntegridadeSnapshotException.NadaPublicado);

            ResultadoPragmaSnapshot pragma;
            try
            {
                pragma = await SqliteSnapshot.VerificarAsync(snapshot, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                throw new IntegridadeSnapshotException(IntegridadeSnapshotException.NadaPublicado);
            }

            if (pragma == ResultadoPragmaSnapshot.IntegrityCheck)
                throw new IntegridadeSnapshotException(IntegridadeSnapshotException.IntegrityCheck);
            if (pragma == ResultadoPragmaSnapshot.ForeignKeyCheck)
                throw new IntegridadeSnapshotException(IntegridadeSnapshotException.ForeignKeyCheck);

            IReadOnlyList<string> historico;
            try
            {
                historico = await SqliteSnapshot.LerHistoricoAsync(snapshot, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                throw new IntegridadeSnapshotException(IntegridadeSnapshotException.NadaPublicado);
            }

            if (_versao.Calcular(historico) is not int versaoSchema || versaoSchema < 1)
                throw new IntegridadeSnapshotException(IntegridadeSnapshotException.BancoSemHistorico);

            bool temDadosDoApp;
            try
            {
                temDadosDoApp = await SqliteSnapshot.TemTabelaAsync(snapshot, TabelaDoApp, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                throw new IntegridadeSnapshotException(IntegridadeSnapshotException.NadaPublicado);
            }

            if (!temDadosDoApp)
                throw new IntegridadeSnapshotException(IntegridadeSnapshotException.BancoSemHistorico);

            var info = new FileInfo(snapshot);
            if (info.Length <= 0 || info.Length > FormatoEspelho.TamanhoMaximoConteudo)
                throw new IntegridadeSnapshotException(IntegridadeSnapshotException.NadaPublicado);

            byte[] bytes;
            try
            {
                bytes = await File.ReadAllBytesAsync(snapshot, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                throw new IntegridadeSnapshotException(IntegridadeSnapshotException.NadaPublicado);
            }

            MetadadosEspelho metadados;
            try
            {
                try
                {
                    var ultima = historico[^1];
                    metadados = CofreEspelho.Gravar(
                        cifrado,
                        bytes,
                        senha,
                        versaoSchema,
                        ultima,
                        DateTimeOffset.UtcNow,
                        _iteracoes);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (ArgumentException)
                {
                    throw;
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(bytes);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (IntegridadeSnapshotException)
            {
                throw;
            }
            catch
            {
                throw new IntegridadeSnapshotException(IntegridadeSnapshotException.NadaPublicado);
            }

            ApagarArquivo(snapshot);
            await destino.PublicarAsync(cifrado, cancellationToken);
            return metadados;
        }
        finally
        {
            ApagarPasta(pasta);
        }
    }

    private const string TabelaDoApp = "MembrosFamilia";

    private static void ApagarArquivo(string caminho)
    {
        try
        {
            if (File.Exists(caminho))
                File.Delete(caminho);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
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
