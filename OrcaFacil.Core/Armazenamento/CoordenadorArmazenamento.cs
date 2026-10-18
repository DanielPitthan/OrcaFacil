using OrcaFacil.Core.Data;

namespace OrcaFacil.Core.Armazenamento;

/// <summary>
/// Observa banco vivo e espelho pelas portas, decide com <see cref="MatrizDecisaoArmazenamento"/>
/// e executa o passo seguro. Não conhece arquivo, SQLite nem Android.
/// </summary>
/// <remarks>
/// Regra de ouro: restauração que falha vira <see cref="AcaoArmazenamento.TelaDeRecuperacao"/>.
/// Esse caminho não chama <see cref="ISemeaduraDados"/> e não chama <see cref="ILimpezaTotalDados"/>.
/// Espelho mais antigo, depois de restaurado, é migrado com as <see cref="OpcoesPreparoBanco"/>.
/// Semear cria o schema por essa mesma migração e só então grava os dados iniciais.
/// Schema íntegro sem membro vira <see cref="SituacaoBancoVivo.SemPerfil"/> por <see cref="PoliticaSeed"/> e semeia de novo.
/// Banco ilegível só é substituído depois de <see cref="IPreservacaoBancoVivo"/>; se a cópia falha, a recuperação abre.
/// <see cref="PrepararAsync"/> e <see cref="LimparTudoAsync"/> não correm ao mesmo tempo.
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
    private readonly IPreservacaoBancoVivo _preservacao;
    private readonly IPresencaPerfil _perfil;
    private readonly OpcoesPreparoBanco _opcoes;
    private readonly SemaphoreSlim _exclusao = new(1, 1);

    public CoordenadorArmazenamento(
        ISondaBancoVivo banco,
        IEspelhoDados espelho,
        IVersaoSchemaApp versaoApp,
        IRestauracaoEspelho restauracao,
        IMigracaoBancoVivo migracao,
        ISemeaduraDados semeadura,
        ILimpezaTotalDados limpeza,
        OpcoesPreparoBanco opcoesPreparo,
        IPreservacaoBancoVivo preservacao,
        IPresencaPerfil perfil)
    {
        _banco = banco ?? throw new ArgumentNullException(nameof(banco));
        _espelho = espelho ?? throw new ArgumentNullException(nameof(espelho));
        _versaoApp = versaoApp ?? throw new ArgumentNullException(nameof(versaoApp));
        _restauracao = restauracao ?? throw new ArgumentNullException(nameof(restauracao));
        _migracao = migracao ?? throw new ArgumentNullException(nameof(migracao));
        _semeadura = semeadura ?? throw new ArgumentNullException(nameof(semeadura));
        _limpeza = limpeza ?? throw new ArgumentNullException(nameof(limpeza));
        _opcoes = opcoesPreparo ?? throw new ArgumentNullException(nameof(opcoesPreparo));
        _preservacao = preservacao ?? throw new ArgumentNullException(nameof(preservacao));
        _perfil = perfil ?? throw new ArgumentNullException(nameof(perfil));
    }

    public Task<ResultadoArmazenamento> PrepararAsync(CancellationToken cancellationToken = default)
        => EmExclusaoAsync(PrepararDentroDaExclusaoAsync, cancellationToken);

    public Task<ResultadoLimpezaArmazenamento> LimparTudoAsync(
        PedidoLimparTudo pedido,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        return EmExclusaoAsync(ct => LimparDentroDaExclusaoAsync(ct), cancellationToken);
    }

    private async Task<ResultadoArmazenamento> PrepararDentroDaExclusaoAsync(CancellationToken cancellationToken)
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
            var resultado = await ExecutarAsync(decisao, observacao, cancellationToken);
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

    private async Task<ResultadoLimpezaArmazenamento> LimparDentroDaExclusaoAsync(CancellationToken cancellationToken)
    {
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

    private async Task<T> EmExclusaoAsync<T>(Func<CancellationToken, Task<T>> acao, CancellationToken cancellationToken)
    {
        await _exclusao.WaitAsync(cancellationToken);
        try
        {
            return await acao(cancellationToken);
        }
        finally
        {
            _exclusao.Release();
        }
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

        var bancoIlegivel = leituraFalhou || (relatorio is not null && ClassificacaoBancoVivo.MarcaArquivoIlegivel(relatorio));
        var banco = ClassificacaoBancoVivo.Classificar(new LeituraBancoVivo(bancoExiste, relatorio, leituraFalhou));
        if (banco == SituacaoBancoVivo.Integro)
        {
            var haMembro = await _perfil.HaMembroAsync(cancellationToken);
            banco = ClassificacaoBancoVivo.AplicarPoliticaSeed(banco, relatorio, haMembro);
        }

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
            falhaEspelho,
            bancoIlegivel);
    }

    private async Task<ResultadoArmazenamento> ExecutarAsync(
        DecisaoArmazenamento decisao,
        Observacao observacao,
        CancellationToken cancellationToken)
    {
        var estado = observacao.Estado;
        switch (decisao.Acao)
        {
            case AcaoArmazenamento.UsarBancoVivo:
            case AcaoArmazenamento.PedirAtualizacaoDoApp:
            case AcaoArmazenamento.TelaDeRecuperacao:
                return Concluido(decisao, estado);

            case AcaoArmazenamento.MigrarBancoVivo:
                var falhaMigracao = await MigrarOuFalharAsync(decisao.Acao, estado, cancellationToken);
                return falhaMigracao ?? Concluido(decisao, estado);

            case AcaoArmazenamento.RestaurarDoEspelho:
                if (observacao.BancoIlegivel)
                {
                    var preservacao = await _preservacao.PreservarAsync(cancellationToken);
                    if (!preservacao.Sucesso)
                    {
                        return new ResultadoArmazenamento(
                            AcaoArmazenamento.TelaDeRecuperacao,
                            decisao.Acao,
                            estado,
                            "O banco ilegível não foi copiado, então o espelho não o substituiu. Nenhum arquivo foi apagado e nenhum dado foi semeado.",
                            new InvalidOperationException(preservacao.MotivoFalha ?? "Não foi possível preservar o banco vivo."));
                    }
                }

                var restauracao = await _restauracao.RestaurarAsync(cancellationToken);
                if (!restauracao.Sucesso)
                {
                    return FalhaDeExecucao(
                        decisao.Acao,
                        estado,
                        new InvalidOperationException(restauracao.MotivoFalha ?? "A restauração não concluiu."));
                }

                if (estado.SchemaEspelho == ComparacaoSchemaEspelho.EspelhoMaisAntigo)
                {
                    var falhaDepois = await MigrarOuFalharAsync(decisao.Acao, estado, cancellationToken);
                    if (falhaDepois is not null)
                    {
                        return falhaDepois with
                        {
                            Motivo = "O espelho antigo foi restaurado, mas a migração seguinte falhou. Nenhum arquivo foi apagado e nenhum dado foi semeado."
                        };
                    }
                }

                return Concluido(decisao, estado);

            case AcaoArmazenamento.Semear:
                var falhaSchema = await MigrarOuFalharAsync(decisao.Acao, estado, cancellationToken);
                if (falhaSchema is not null)
                {
                    return falhaSchema with
                    {
                        Motivo = "O schema não foi criado. Nenhum dado foi semeado e nenhum arquivo foi apagado."
                    };
                }

                await _semeadura.SemearAsync(cancellationToken);
                return Concluido(decisao, estado);

            default:
                throw new ArgumentOutOfRangeException(nameof(decisao), decisao.Acao, "Ação de armazenamento desconhecida.");
        }
    }

    private async Task<ResultadoArmazenamento?> MigrarOuFalharAsync(
        AcaoArmazenamento planejada,
        EstadoObservadoArmazenamento estado,
        CancellationToken cancellationToken)
    {
        var migracao = await _migracao.MigrarEVerificarAsync(_opcoes, cancellationToken);
        if (!migracao.ExigeRecuperacao)
            return null;

        var falha = migracao.FalhaMigrate ?? new IntegridadeBancoException(migracao);
        return FalhaDeExecucao(planejada, estado, falha);
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
        Exception? FalhaEspelho,
        bool BancoIlegivel);
}
