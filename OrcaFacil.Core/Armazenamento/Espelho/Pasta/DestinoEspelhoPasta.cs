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

    /// <summary>
    /// Passo seguinte ao retorno do protocolo. Produção deixa nulo.
    /// Se lança depois de sucesso ou de promoção já efetuada, a falha carrega essa marca.
    /// </summary>
    internal Func<ResultadoOperacaoPasta, CancellationToken, Task>? DepoisDoProtocolo { get; set; }

    public Task<ResultadoOperacaoPasta> PublicarComResultadoAsync(
        string arquivoOrigem,
        CancellationToken cancellationToken = default)
        => PublicarInternoAsync(arquivoOrigem, cancellationToken);

    public async Task PublicarAsync(string arquivoOrigem, CancellationToken cancellationToken = default)
    {
        var resultado = await PublicarInternoAsync(arquivoOrigem, cancellationToken);
        if (!resultado.Sucesso)
        {
            if (resultado.PromocaoEfetuada)
                throw new PublicacaoEspelhoException(resultado.Mensagem, promocaoEfetuada: true);

            throw new IOException(resultado.Mensagem);
        }
    }

    public override string ToString() => "DestinoEspelhoPasta";

    private async Task<ResultadoOperacaoPasta> PublicarInternoAsync(
        string arquivoOrigem,
        CancellationToken cancellationToken)
    {
        ResultadoOperacaoPasta? resultado = null;
        try
        {
            resultado = await _protocolo.PublicarAsync(arquivoOrigem, cancellationToken);
            if (DepoisDoProtocolo is not null)
                await DepoisDoProtocolo(resultado, cancellationToken);

            return resultado;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var codigo = ClassificadorFalhaPastaEspelho.Classificar(SinalFalhaPasta.De(ex));
            var promocaoEfetuada = resultado is not null && (resultado.Sucesso || resultado.PromocaoEfetuada);
            return ResultadoOperacaoPasta.Falha(codigo, promocaoEfetuada);
        }
    }
}

/// <summary>
/// Falha da publicação na pasta. <see cref="PromocaoEfetuada"/> fica ligado quando o
/// temporário já assumiu o nome definitivo, mesmo que a chamada tenha falhado.
/// </summary>
public sealed class PublicacaoEspelhoException : IOException
{
    public PublicacaoEspelhoException(string mensagem, bool promocaoEfetuada)
        : base(mensagem)
    {
        PromocaoEfetuada = promocaoEfetuada;
    }

    public bool PromocaoEfetuada { get; }
}
