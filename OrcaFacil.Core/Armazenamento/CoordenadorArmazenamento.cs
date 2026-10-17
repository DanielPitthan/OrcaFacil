using OrcaFacil.Core.Data;

namespace OrcaFacil.Core.Armazenamento;

/// <summary>
/// Observa banco vivo e espelho pelas portas, decide com <see cref="MatrizDecisaoArmazenamento"/>
/// e executa um único passo. Não conhece arquivo, SQLite nem Android.
/// </summary>
/// <remarks>
/// Regra de ouro: restauração que falha vira <see cref="AcaoArmazenamento.TelaDeRecuperacao"/>.
/// Esse caminho não chama <see cref="ISemeaduraDados"/> e não chama <see cref="ILimpezaTotalDados"/>.
/// Falha de migração também não cai para o espelho nesta chamada — o banco vivo continua no lugar.
/// <see cref="LimparTudoAsync"/> é outro método e exige <see cref="PedidoLimparTudo"/>.
/// </remarks>
public sealed class CoordenadorArmazenamento : IArmazenamentoDados
{
    private readonly ISondaBancoVivo _banco;
    private readonly IEspelhoDados _espelho;
    private readonly IVersaoSchemaApp _versaoApp;
    private readonly IRestauracaoEspelho _restauracao;
    private readonly IMigracaoBancoVivo _migracao;
    private readonly ISemeaduraDados _semeadura;
    private readonly ILimpezaTotalDados _limpeza;

    public CoordenadorArmazenamento(
        ISondaBancoVivo banco,
        IEspelhoDados espelho,
        IVersaoSchemaApp versaoApp,
        IRestauracaoEspelho restauracao,
        IMigracaoBancoVivo migracao,
        ISemeaduraDados semeadura,
        ILimpezaTotalDados limpeza)
    {
        _banco = banco ?? throw new ArgumentNullException(nameof(banco));
        _espelho = espelho ?? throw new ArgumentNullException(nameof(espelho));
        _versaoApp = versaoApp ?? throw new ArgumentNullException(nameof(versaoApp));
        _restauracao = restauracao ?? throw new ArgumentNullException(nameof(restauracao));
        _migracao = migracao ?? throw new ArgumentNullException(nameof(migracao));
        _semeadura = semeadura ?? throw new ArgumentNullException(nameof(semeadura));
        _limpeza = limpeza ?? throw new ArgumentNullException(nameof(limpeza));
    }

    public async Task<ResultadoArmazenamento> PrepararAsync(CancellationToken cancellationToken = default)
    {
        Observacao observacao;
        try
        {
            observacao = await ObservarAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ResultadoArmazenamento(
                AcaoArmazenamento.TelaDeRecuperacao,
                AcaoArmazenamento.TelaDeRecuperacao,
                null,
                "Não foi possível observar o armazenamento. Nenhum arquivo foi apagado e nenhum dado foi semeado.",
                ex);
        }

        var decisao = MatrizDecisaoArmazenamento.Decidir(observacao.Estado);
        try
        {
            var resultado = await ExecutarAsync(decisao, observacao.Estado, cancellationToken);
            if (resultado.Falha is null && resultado.Acao == AcaoArmazenamento.TelaDeRecuperacao)
            {
                var falhaObservacao = FalhaDeObservacao(observacao);
                if (falhaObservacao is not null)
                    return resultado with { Falha = falhaObservacao };
            }

            return resultado;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return FalhaDeExecucao(decisao.Acao, observacao.Estado, ex);
        }
    }

    public async Task<ResultadoLimpezaArmazenamento> LimparTudoAsync(
        PedidoLimparTudo pedido,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        Exception? falhaBanco = null;
        Exception? falhaEspelho = null;

        try
        {
            await _limpeza.RemoverBancoVivoAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            falhaBanco = ex;
        }

        try
        {
            await _limpeza.RemoverEspelhoAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            falhaEspelho = ex;
        }

        if (falhaBanco is not null || falhaEspelho is not null)
            throw new LimpezaTotalException(falhaBanco, falhaEspelho);

        return new ResultadoLimpezaArmazenamento(BancoVivoRemovido: true, EspelhoRemovido: true);
    }

    private async Task<Observacao> ObservarAsync(CancellationToken cancellationToken)
    {
        var versaoApp = _versaoApp.Atual;
        if (versaoApp < 0)
            throw new InvalidOperationException("A versão de schema do app não pode ser negativa.");

        RelatorioIntegridadeBanco? relatorio = null;
        var leituraFalhou = false;
        Exception? falhaBanco = null;
        var bancoExiste = _banco.Existe();
        if (bancoExiste)
        {
            try
            {
                relatorio = await _banco.VerificarAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                leituraFalhou = true;
                falhaBanco = ex;
            }
        }

        var banco = ClassificacaoBancoVivo.Classificar(new LeituraBancoVivo(bancoExiste, relatorio, leituraFalhou));

        Exception? falhaEspelho = null;
        var espelhoExiste = _espelho.Existe();
        SituacaoEspelho espelho;
        ComparacaoSchemaEspelho schema;
        if (!espelhoExiste)
        {
            espelho = SituacaoEspelho.Ausente;
            schema = ComparacaoSchemaEspelho.SemEspelho;
        }
        else
        {
            try
            {
                var leitura = await _espelho.LerAsync(cancellationToken);
                if (leitura.Legivel && leitura.VersaoSchema is int versaoEspelho)
                {
                    espelho = SituacaoEspelho.Legivel;
                    schema = ComparadorSchemaEspelho.Comparar(versaoApp, versaoEspelho);
                }
                else
                {
                    // Ilegível é estado observado, não exceção. O motivo fica na leitura.
                    espelho = SituacaoEspelho.Corrompido;
                    schema = ComparacaoSchemaEspelho.NaoAplicavel;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                espelho = SituacaoEspelho.Corrompido;
                schema = ComparacaoSchemaEspelho.NaoAplicavel;
                falhaEspelho = ex;
            }
        }

        return new Observacao(
            new EstadoObservadoArmazenamento(banco, espelho, schema),
            falhaBanco,
            falhaEspelho);
    }

    private async Task<ResultadoArmazenamento> ExecutarAsync(
        DecisaoArmazenamento decisao,
        EstadoObservadoArmazenamento estado,
        CancellationToken cancellationToken)
    {
        switch (decisao.Acao)
        {
            case AcaoArmazenamento.UsarBancoVivo:
            case AcaoArmazenamento.PedirAtualizacaoDoApp:
            case AcaoArmazenamento.TelaDeRecuperacao:
                return Concluido(decisao, estado);

            case AcaoArmazenamento.MigrarBancoVivo:
                var migracao = await _migracao.MigrarEVerificarAsync(cancellationToken);
                if (!migracao.ExigeRecuperacao)
                    return Concluido(decisao, estado);

                var falhaMigracao = migracao.FalhaMigrate ?? new IntegridadeBancoException(migracao);
                return FalhaDeExecucao(decisao.Acao, estado, falhaMigracao);

            case AcaoArmazenamento.RestaurarDoEspelho:
                var restauracao = await _restauracao.RestaurarAsync(cancellationToken);
                if (restauracao.Sucesso)
                    return Concluido(decisao, estado);

                return FalhaDeExecucao(
                    decisao.Acao,
                    estado,
                    new InvalidOperationException(restauracao.MotivoFalha ?? "A restauração não concluiu."));

            case AcaoArmazenamento.Semear:
                await _semeadura.SemearAsync(cancellationToken);
                return Concluido(decisao, estado);

            default:
                throw new ArgumentOutOfRangeException(nameof(decisao), decisao.Acao, "Ação de armazenamento desconhecida.");
        }
    }

    private static ResultadoArmazenamento Concluido(DecisaoArmazenamento decisao, EstadoObservadoArmazenamento estado)
        => new(decisao.Acao, decisao.Acao, estado, decisao.Motivo, null);

    private static ResultadoArmazenamento FalhaDeExecucao(
        AcaoArmazenamento planejada,
        EstadoObservadoArmazenamento estado,
        Exception falha)
        => new(
            AcaoArmazenamento.TelaDeRecuperacao,
            planejada,
            estado,
            MensagemDeFalha(planejada),
            falha);

    private static string MensagemDeFalha(AcaoArmazenamento planejada) => planejada switch
    {
        AcaoArmazenamento.RestaurarDoEspelho =>
            "A restauração do espelho falhou. Nenhum arquivo foi apagado e nenhum dado foi semeado.",
        AcaoArmazenamento.MigrarBancoVivo =>
            "A migração do banco vivo falhou. Nenhum arquivo foi apagado e nenhum dado foi semeado.",
        AcaoArmazenamento.Semear =>
            "A semeadura da instalação nova falhou. Nenhum arquivo foi apagado.",
        _ => "A preparação do armazenamento falhou. Nenhum arquivo foi apagado e nenhum dado foi semeado."
    };

    private static Exception? FalhaDeObservacao(Observacao observacao)
    {
        if (observacao.FalhaBanco is not null && observacao.FalhaEspelho is not null)
            return new AggregateException(observacao.FalhaBanco, observacao.FalhaEspelho);
        return observacao.FalhaBanco ?? observacao.FalhaEspelho;
    }

    private sealed record Observacao(
        EstadoObservadoArmazenamento Estado,
        Exception? FalhaBanco,
        Exception? FalhaEspelho);
}
