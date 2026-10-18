namespace OrcaFacil.Core.Armazenamento.Espelho.Pasta;

/// <summary>
/// <see cref="IDestinoEspelho"/> da pasta do usuário. O protocolo é o mesmo do arquivo
/// local: temporário primeiro, nome definitivo só depois. Falha vira <see cref="IOException"/>
/// com a mensagem do código, sem URI e sem caminho, para o gerador de snapshot não publicar
/// por cima de um erro mudo.
/// </summary>
public sealed class DestinoEspelhoPasta : IDestinoEspelho
{
    private readonly ProtocoloPublicacaoEspelho _protocolo;

    public DestinoEspelhoPasta(ProtocoloPublicacaoEspelho protocolo)
    {
        _protocolo = protocolo ?? throw new ArgumentNullException(nameof(protocolo));
    }

    public Task<ResultadoOperacaoPasta> PublicarComResultadoAsync(
        string arquivoOrigem,
        CancellationToken cancellationToken = default)
        => PublicarInternoAsync(arquivoOrigem, cancellationToken);

    public async Task PublicarAsync(string arquivoOrigem, CancellationToken cancellationToken = default)
    {
        var resultado = await PublicarInternoAsync(arquivoOrigem, cancellationToken);
        if (!resultado.Sucesso)
            throw new IOException(resultado.Mensagem);
    }

    public override string ToString() => "DestinoEspelhoPasta";

    private async Task<ResultadoOperacaoPasta> PublicarInternoAsync(
        string arquivoOrigem,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _protocolo.PublicarAsync(arquivoOrigem, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var codigo = ClassificadorFalhaPastaEspelho.Classificar(SinalFalhaPasta.De(ex));
            return ResultadoOperacaoPasta.Falha(codigo);
        }
    }
}
