namespace OrcaFacil.Core.Armazenamento.Espelho.Pasta;

/// <summary>
/// Publica o espelho na pasta: grava um temporário e só então troca o nome.
/// Se a troca não fecha, o espelho anterior permanece. O único descarte no meio
/// do caminho é o temporário desta publicação.
/// </summary>
/// <remarks>
/// Quando o nome definitivo já existe, ele é renomeado para uma reserva, o
/// temporário assume o nome definitivo e a reserva só é removida nessa ordem.
/// Se a segunda troca falha, a reserva volta ao nome definitivo. Se essa volta
/// também falha, a reserva fica na pasta e não é apagada.
/// Provedor sem rename (alguns da nuvem) não entra em cópia por cima: a operação
/// para e o arquivo anterior continua no nome definitivo.
/// </remarks>
public sealed class ProtocoloPublicacaoEspelho
{
    private readonly IVolumePastaEspelho _volume;
    private readonly Func<Guid> _identificadores;

    public ProtocoloPublicacaoEspelho(IVolumePastaEspelho volume)
        : this(volume, Guid.NewGuid)
    {
    }

    public ProtocoloPublicacaoEspelho(IVolumePastaEspelho volume, Func<Guid> identificadores)
    {
        _volume = volume ?? throw new ArgumentNullException(nameof(volume));
        _identificadores = identificadores ?? throw new ArgumentNullException(nameof(identificadores));
    }

    public async Task<ResultadoOperacaoPasta> PublicarAsync(
        string arquivoOrigem,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arquivoOrigem);
        cancellationToken.ThrowIfCancellationRequested();

        var diagnostico = _volume.Consultar();
        if (!diagnostico.AcessoLivre)
            return ResultadoOperacaoPasta.Falha(diagnostico.Codigo);

        var id = _identificadores();
        var temporario = NomeTemporarioEspelho.Criar(_volume.NomeArquivo, id);
        var reserva = NomeTemporarioEspelho.CriarReserva(_volume.NomeArquivo, id);

        var criada = await _volume.CriarTemporarioAsync(temporario, arquivoOrigem, cancellationToken);
        if (!criada.Sucesso)
        {
            await DescartarTemporarioAsync(temporario);
            return criada;
        }

        var parqueOk = false;
        var promoveOk = false;
        try
        {
            if (!diagnostico.ArquivoNoNomeFinal)
                return await PromoverTemporarioAsync(temporario);

            var parque = await _volume.RenomearAsync(_volume.NomeArquivo, reserva, cancellationToken);
            if (!parque.Sucesso)
                return FalhaDeRenome(parque);

            parqueOk = true;
            var promove = await _volume.RenomearAsync(temporario, _volume.NomeArquivo, CancellationToken.None);
            if (!promove.Sucesso)
            {
                if (!await VoltarReservaAsync(reserva))
                    return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.EspelhoPreservadoForaDoNome);

                return FalhaDeRenome(promove);
            }

            promoveOk = true;
            var limpeza = await _volume.DescartarReservaAposTrocaAsync(reserva, CancellationToken.None);
            if (limpeza.Codigo == CodigoFalhaPastaEspelho.EspelhoPreservadoForaDoNome)
                return limpeza;

            return ResultadoOperacaoPasta.Ok();
        }
        catch (Exception)
        {
            if (parqueOk && !promoveOk)
                await VoltarReservaAsync(reserva);

            throw;
        }
        finally
        {
            if (!promoveOk)
                await DescartarTemporarioAsync(temporario);
        }
    }

    private async Task<bool> VoltarReservaAsync(string reserva)
    {
        try
        {
            var volta = await _volume.RenomearAsync(reserva, _volume.NomeArquivo, CancellationToken.None);
            return volta.Sucesso;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<ResultadoOperacaoPasta> PromoverTemporarioAsync(string temporario)
    {
        var renome = await _volume.RenomearAsync(temporario, _volume.NomeArquivo, CancellationToken.None);
        if (renome.Sucesso)
            return ResultadoOperacaoPasta.Ok();

        return FalhaDeRenome(renome);
    }

    private async Task DescartarTemporarioAsync(string temporario)
    {
        try
        {
            await _volume.DescartarTemporarioAsync(temporario, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // A falha original da publicação é a que volta. O espelho anterior não é apagado aqui.
        }
    }

    private static ResultadoOperacaoPasta FalhaDeRenome(ResultadoRenomeacaoVolume renome)
    {
        if (renome.Codigo != CodigoFalhaPastaEspelho.Nenhuma)
            return ResultadoOperacaoPasta.Falha(renome.Codigo);

        return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.ProvedorSemTroca);
    }
}
