namespace OrcaFacil.Core.Armazenamento.Espelho;

/// <summary>
/// Publica o espelho já cifrado. A implementação grava num nome temporário e só então troca.
/// Se a troca não acontecer, o espelho anterior permanece intacto.
/// </summary>
public interface IDestinoEspelho
{
    Task PublicarAsync(string arquivoOrigem, CancellationToken cancellationToken = default);
}

/// <summary>
/// Troca atômica no sistema de arquivos local. A pasta do usuário no Android (SAF) entra noutro PR;
/// esta classe é a implementação de Core usada nos testes e em destinos que já tenham um caminho.
/// </summary>
/// <remarks>
/// O temporário fica na mesma pasta do destino, então o rename é atômico no mesmo volume.
/// Falha na cópia ou na troca apaga o temporário e não substitui o arquivo anterior.
/// A mensagem de erro não inclui o caminho.
/// </remarks>
public sealed class DestinoEspelhoArquivo : IDestinoEspelho
{
    private readonly string _caminho;

    public DestinoEspelhoArquivo(string caminhoEspelho)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminhoEspelho);
        _caminho = caminhoEspelho;
    }

    public async Task PublicarAsync(string arquivoOrigem, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arquivoOrigem);
        cancellationToken.ThrowIfCancellationRequested();

        var pasta = Path.GetDirectoryName(Path.GetFullPath(_caminho));
        if (string.IsNullOrEmpty(pasta))
            throw new IOException("Não foi possível publicar o espelho. O arquivo anterior foi mantido.");

        Directory.CreateDirectory(pasta);
        var temporario = Path.Combine(
            pasta,
            "." + Path.GetFileName(_caminho) + "." + Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            await CopiarAsync(arquivoOrigem, temporario, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporario, _caminho, overwrite: true);
        }
        catch (OperationCanceledException)
        {
            Apagar(temporario);
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Apagar(temporario);
            throw new IOException("Não foi possível publicar o espelho. O arquivo anterior foi mantido.");
        }
    }

    public override string ToString() => "DestinoEspelhoArquivo";

    private static async Task CopiarAsync(string origem, string destino, CancellationToken cancellationToken)
    {
        await using var entrada = new FileStream(
            origem,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var saida = new FileStream(
            destino,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await entrada.CopyToAsync(saida, cancellationToken);
        await saida.FlushAsync(cancellationToken);
    }

    private static void Apagar(string caminho)
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
}
