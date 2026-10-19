using System.Globalization;
using System.Security.Cryptography;

namespace OrcaFacil.Core.Armazenamento.Partida;

/// <summary>
/// Copia o banco e os laterais e só aceita o resultado se cada arquivo
/// existir no destino, tiver o mesmo tamanho e o mesmo SHA-256 da origem.
/// </summary>
internal static class CopiaConfirmada
{
    public static string Copiar(
        InventarioBanco inventario,
        string pastaBackups,
        string prefixo,
        List<string> criados,
        CancellationToken cancellationToken,
        Action<string>? depoisDeCopiar = null)
    {
        Directory.CreateDirectory(pastaBackups);
        var carimbo = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
        var destino = CaminhoLivre(Path.Combine(pastaBackups, $"{prefixo}-{carimbo}.db"));
        foreach (var arquivo in inventario.Arquivos)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var alvo = destino + arquivo.Sufixo;
            criados.Add(alvo);
            File.Copy(arquivo.Caminho, alvo, overwrite: false);
            depoisDeCopiar?.Invoke(alvo);
            cancellationToken.ThrowIfCancellationRequested();
            if (!Confere(arquivo.Caminho, alvo))
                throw new IOException("A cópia não conferiu em tamanho ou conteúdo. Nada foi apagado.");
        }

        return destino;
    }

    public static string GarantirPasta(string caminhoBanco, Func<string, bool>? sonda)
    {
        var pastaBanco = Path.GetDirectoryName(Path.GetFullPath(caminhoBanco));
        if (string.IsNullOrEmpty(pastaBanco))
            throw new IOException("Não foi possível preservar o banco vivo. O arquivo original não foi apagado.");

        var backups = Path.Combine(pastaBanco, "backups");
        bool ocupadoPorArquivo;
        try
        {
            ocupadoPorArquivo = (sonda ?? File.Exists)(backups) && !Directory.Exists(backups);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException("Não foi possível confirmar a pasta de backups. Nada foi apagado.", ex);
        }

        if (ocupadoPorArquivo)
            throw new IOException("Não foi possível preservar o banco vivo. O arquivo original não foi apagado.");

        return backups;
    }

    public static bool Confere(string origem, string destino)
    {
        var infoOrigem = new FileInfo(origem);
        var infoDestino = new FileInfo(destino);
        infoOrigem.Refresh();
        infoDestino.Refresh();
        if (!infoOrigem.Exists || !infoDestino.Exists || infoDestino.Length != infoOrigem.Length)
            return false;

        try
        {
            using var origemFluxo = new FileStream(origem, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var destinoFluxo = new FileStream(destino, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (origemFluxo.Length != destinoFluxo.Length)
                return false;

            var hashOrigem = SHA256.HashData(origemFluxo);
            var hashDestino = SHA256.HashData(destinoFluxo);
            return CryptographicOperations.FixedTimeEquals(hashOrigem, hashDestino);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static void ApagarIncompletos(List<string> criados)
    {
        foreach (var criado in criados)
        {
            try
            {
                if (File.Exists(criado))
                    File.Delete(criado);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static string CaminhoLivre(string caminho)
    {
        if (!ExisteComCerteza(caminho))
            return caminho;

        var pasta = Path.GetDirectoryName(caminho)!;
        var nome = Path.GetFileNameWithoutExtension(caminho);
        var extensao = Path.GetExtension(caminho);
        for (var i = 2; i < 1000; i++)
        {
            var candidato = Path.Combine(pasta, $"{nome}-{i}{extensao}");
            if (!ExisteComCerteza(candidato))
                return candidato;
        }

        throw new IOException("Não foi possível preservar o banco vivo. O arquivo original não foi apagado.");
    }

    private static bool ExisteComCerteza(string caminho)
    {
        try
        {
            return File.Exists(caminho);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException("Não foi possível confirmar se o destino da cópia está livre. Nada foi apagado.", ex);
        }
    }
}
