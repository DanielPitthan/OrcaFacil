using OrcaFacil.Core.Armazenamento;

namespace OrcaFacil.Core.Armazenamento.Partida;

/// <summary>
/// Copia o banco vivo e os laterais para <c>backups</c> antes de uma substituição.
/// Sucesso só depois que o <c>orcafacil.db</c> e cada lateral que existia estão na pasta,
/// com o mesmo tamanho e legíveis. Falha, ou cancelamento, apaga a cópia incompleta
/// desta tentativa e não mexe no original, no espelho nem num backup que já existia.
/// </summary>
public sealed class PreservacaoBancoArquivo : IPreservacaoBancoVivo
{
    private readonly CaminhoBancoVivo _caminho;
    private readonly Func<string, bool>? _sonda;
    private readonly Action<string>? _depoisDeCopiar;

    public PreservacaoBancoArquivo(CaminhoBancoVivo caminho)
        : this(caminho, null, null)
    {
    }

    internal PreservacaoBancoArquivo(
        CaminhoBancoVivo caminho,
        Func<string, bool>? sonda,
        Action<string>? depoisDeCopiar = null)
    {
        _caminho = caminho ?? throw new ArgumentNullException(nameof(caminho));
        _sonda = sonda;
        _depoisDeCopiar = depoisDeCopiar;
    }

    public Task<ResultadoPreservacao> PreservarAsync(CancellationToken cancellationToken = default)
    {
        List<string> criados = [];
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var inventario = ArquivosBancoVivo.Inventariar(_caminho.Valor, _sonda);
            if (!inventario.TemPrincipal)
                return Task.FromResult(ResultadoPreservacao.Falha("Não há banco vivo para preservar."));

            var backups = CopiaConfirmada.GarantirPasta(_caminho.Valor, _sonda);
            var destino = CopiaConfirmada.Copiar(
                inventario,
                backups,
                "orcafacil-preservado",
                criados,
                cancellationToken,
                _depoisDeCopiar);
            return Task.FromResult(ResultadoPreservacao.Ok(destino));
        }
        catch (OperationCanceledException)
        {
            CopiaConfirmada.ApagarIncompletos(criados);
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CopiaConfirmada.ApagarIncompletos(criados);
            return Task.FromResult(ResultadoPreservacao.Falha("Não foi possível preservar o banco vivo. O arquivo original não foi apagado."));
        }
    }
}
