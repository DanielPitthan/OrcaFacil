using System.Globalization;
using OrcaFacil.Core.Armazenamento;

namespace OrcaFacil.Core.Armazenamento.Partida;

/// <summary>
/// Copia o banco vivo e os laterais para <c>backups</c> antes de uma substituição.
/// Falha não apaga o original, o espelho nem um backup que já existia.
/// A cópia incompleta desta tentativa é o único arquivo que pode sair.
/// </summary>
public sealed class PreservacaoBancoArquivo : IPreservacaoBancoVivo
{
    private readonly CaminhoBancoVivo _caminho;

    public PreservacaoBancoArquivo(CaminhoBancoVivo caminho)
    {
        _caminho = caminho ?? throw new ArgumentNullException(nameof(caminho));
    }

    public Task<ResultadoPreservacao> PreservarAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!ArquivosBancoVivo.ExisteArquivo(_caminho.Valor))
            return Task.FromResult(ResultadoPreservacao.Falha("Não há banco vivo para preservar."));

        var pastaBanco = Path.GetDirectoryName(Path.GetFullPath(_caminho.Valor));
        if (string.IsNullOrEmpty(pastaBanco))
            return Task.FromResult(ResultadoPreservacao.Falha("Não foi possível preservar o banco vivo. O arquivo original não foi apagado."));

        var backups = Path.Combine(pastaBanco, "backups");
        if (ArquivosBancoVivo.ExisteArquivo(backups))
            return Task.FromResult(ResultadoPreservacao.Falha("Não foi possível preservar o banco vivo. O arquivo original não foi apagado."));

        List<string> criados = [];
        try
        {
            Directory.CreateDirectory(backups);
            var carimbo = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
            var destino = CaminhoLivre(Path.Combine(backups, $"orcafacil-preservado-{carimbo}.db"));
            var baseBanco = _caminho.Valor;
            foreach (var origem in ArquivosBancoVivo.Existentes(baseBanco).ToList())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!origem.StartsWith(baseBanco, StringComparison.Ordinal))
                    throw new IOException("Não foi possível preservar o banco vivo. O arquivo original não foi apagado.");

                var alvo = destino + origem[baseBanco.Length..];
                File.Copy(origem, alvo, overwrite: false);
                criados.Add(alvo);
            }

            return Task.FromResult(ResultadoPreservacao.Ok(destino));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ApagarIncompletos(criados);
            return Task.FromResult(ResultadoPreservacao.Falha("Não foi possível preservar o banco vivo. O arquivo original não foi apagado."));
        }
    }

    private static string CaminhoLivre(string caminho)
    {
        if (!ArquivosBancoVivo.ExisteArquivo(caminho))
            return caminho;

        var pasta = Path.GetDirectoryName(caminho)!;
        var nome = Path.GetFileNameWithoutExtension(caminho);
        var extensao = Path.GetExtension(caminho);
        for (var i = 2; i < 1000; i++)
        {
            var candidato = Path.Combine(pasta, $"{nome}-{i}{extensao}");
            if (!ArquivosBancoVivo.ExisteArquivo(candidato))
                return candidato;
        }

        throw new IOException("Não foi possível preservar o banco vivo. O arquivo original não foi apagado.");
    }

    private static void ApagarIncompletos(List<string> criados)
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
}
