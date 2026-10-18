namespace OrcaFacil.Core.Armazenamento.Espelho.Pasta;

/// <summary>
/// Publica o espelho na pasta: grava um temporário e só então troca o nome.
/// Se a troca não fecha, o espelho anterior permanece. O único descarte no meio
/// do caminho é o temporário desta publicação, e só quando o rename não deixou
/// os dois nomes visíveis nem já tinha movido a origem.
/// </summary>
/// <remarks>
/// Uma publicação de cada vez. Cancelamento interrompe a etapa seguinte e não
/// apaga o que já foi parar na pasta: reserva ou temporário sem o definitivo
/// continuam visíveis para o coordenador.
/// Quando o nome definitivo já existe, ele é renomeado para uma reserva, o
/// temporário assume o nome definitivo e a reserva só é removida nessa ordem,
/// com a origem do temporário já ausente. Se a segunda troca falha de verdade
/// e o arquivo ainda não saiu do temporário, a reserva volta ao nome definitivo.
/// Se essa volta também falha, ou o rename já tinha ocorrido, a reserva fica
/// e não é apagada.
/// Se o rename vira cópia (definitivo novo e temporário continuam visíveis),
/// nada é apagado: o resultado é <see cref="CodigoFalhaPastaEspelho.PublicadoComResiduo"/>.
/// </remarks>
public sealed class ProtocoloPublicacaoEspelho
{
    private readonly IVolumePastaEspelho _volume;
    private readonly Func<Guid> _identificadores;
    private readonly SemaphoreSlim _exclusao = new(1, 1);

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
        await _exclusao.WaitAsync(cancellationToken);
        try
        {
            return await PublicarDentroAsync(arquivoOrigem, cancellationToken);
        }
        finally
        {
            _exclusao.Release();
        }
    }

    private async Task<ResultadoOperacaoPasta> PublicarDentroAsync(
        string arquivoOrigem,
        CancellationToken cancellationToken)
    {
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
            if (!cancellationToken.IsCancellationRequested
                && criada.Codigo != CodigoFalhaPastaEspelho.Cancelado)
            {
                await DescartarTemporarioAsync(temporario);
            }

            return criada;
        }

        var parqueOk = false;
        var promoveOk = false;
        var manterTemporario = false;
        var cancelado = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!diagnostico.ArquivoNoNomeFinal)
            {
                var primeiro = await PromoverTemporarioAsync(temporario, cancellationToken);
                if (primeiro.ManterTemporario)
                    manterTemporario = true;

                return primeiro.Resultado;
            }

            var parque = await _volume.RenomearAsync(_volume.NomeArquivo, reserva, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (DecidirResiduo(parque, publicado: false) is ResultadoOperacaoPasta residuoParque)
            {
                manterTemporario = true;
                return residuoParque;
            }

            if (!parque.Sucesso)
                return FalhaDeRenome(parque);

            parqueOk = true;
            var promove = await _volume.RenomearAsync(temporario, _volume.NomeArquivo, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (DecidirResiduo(promove, publicado: true) is ResultadoOperacaoPasta residuoPromove)
            {
                manterTemporario = true;
                return residuoPromove;
            }

            if (!promove.Sucesso)
            {
                if (!await VoltarReservaAsync(reserva, cancellationToken))
                    return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.EspelhoPreservadoForaDoNome);

                return FalhaDeRenome(promove);
            }

            promoveOk = true;
            var limpeza = await _volume.DescartarReservaAposTrocaAsync(reserva, cancellationToken);
            if (limpeza.Codigo == CodigoFalhaPastaEspelho.EspelhoPreservadoForaDoNome)
                return limpeza;

            return ResultadoOperacaoPasta.Ok();
        }
        catch (OperationCanceledException)
        {
            cancelado = true;
            throw;
        }
        catch (Exception)
        {
            if (parqueOk && !promoveOk && !cancellationToken.IsCancellationRequested)
                await VoltarReservaAsync(reserva, CancellationToken.None);

            throw;
        }
        finally
        {
            if (!cancelado && !promoveOk && !manterTemporario && !cancellationToken.IsCancellationRequested)
                await DescartarTemporarioAsync(temporario);
        }
    }

    private async Task<Promocao> PromoverTemporarioAsync(string temporario, CancellationToken cancellationToken)
    {
        var renome = await _volume.RenomearAsync(temporario, _volume.NomeArquivo, cancellationToken);
        if (DecidirResiduo(renome, publicado: true) is ResultadoOperacaoPasta residuo)
            return new Promocao(residuo, true);

        if (renome.Sucesso)
            return new Promocao(ResultadoOperacaoPasta.Ok(), false);

        return new Promocao(FalhaDeRenome(renome), false);
    }

    private static ResultadoOperacaoPasta? DecidirResiduo(ResultadoRenomeacaoVolume renome, bool publicado)
    {
        if (renome.AmbosNomesVisiveis)
            return publicado
                ? ResultadoOperacaoPasta.PublicadoComResiduo()
                : FalhaDeRenome(renome);

        if (renome.TrocaEfetuada)
            return FalhaDeRenome(renome);

        return null;
    }

    private async Task<bool> VoltarReservaAsync(string reserva, CancellationToken cancellationToken)
    {
        try
        {
            var volta = await _volume.RenomearAsync(reserva, _volume.NomeArquivo, cancellationToken);
            return volta.Sucesso || volta.TrocaEfetuada;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
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

    private readonly record struct Promocao(ResultadoOperacaoPasta Resultado, bool ManterTemporario);
}
