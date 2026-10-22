using System.Security.Cryptography;
using OrcaFacil.Core.Armazenamento.Espelho.Pasta;
using OrcaFacil.Core.Data;

namespace OrcaFacil.Core.Armazenamento.Espelho;

/// <summary>
/// Gera o snapshot cifrado e publica na pasta escolhida.
/// Senha fraca, pasta indisponível ou falha no meio não trocam a senha memorizada
/// e não apagam o espelho anterior. A senha nova só entra na memória quando o nome
/// definitivo fica com o ciphertext novo.
/// </summary>
public sealed class BackupEspelhoAgora
{
    public const string MensagemGravado = "Backup gravado na pasta escolhida.";

    public const string MensagemGravadoComReserva =
        "Backup gravado na pasta escolhida. A limpeza da cópia anterior falhou; ela permanece na pasta.";

    public const string MensagemGravadoLeituraNaoConfirmada =
        "Backup atualizado na pasta escolhida. Não foi possível confirmar a leitura do arquivo final.";

    /// <summary>
    /// A frase volta a citar o Restaurar quando a parte B do #42 entrar.
    /// </summary>
    public const string MensagemRecusaEspelhoExistente =
        "Já existe um backup nesta pasta. A cópia anterior continua guardada na pasta de backup.";

    public const string MensagemConfirmacao =
        "Já existe um backup nesta pasta. Confirme para substituí-lo. O banco deste aparelho não será apagado.";

    public const string MensagemSemSenha = "Defina a senha do espelho antes do backup. Nada foi publicado.";

    public const string MensagemNaoCoincide = "As senhas não coincidem. Nada foi publicado.";

    public const string MensagemMantido = "Não foi possível publicar o espelho. O arquivo anterior foi mantido.";

    private readonly IVolumePastaEspelho _volume;
    private readonly IDestinoEspelho _destino;
    private readonly IProvedorSenhaEspelho _provedor;
    private readonly IDefinicaoSenhaEspelho _definicao;
    private readonly GeradorSnapshotBanco _gerador;
    private readonly IRegistroLog? _log;
    private readonly IAreaTemporariaEspelho? _area;

    public BackupEspelhoAgora(
        IVolumePastaEspelho volume,
        IDestinoEspelho destino,
        IProvedorSenhaEspelho provedor,
        IDefinicaoSenhaEspelho definicao,
        VersaoSchemaAplicacao versao,
        IRegistroLog? log = null,
        IAreaTemporariaEspelho? area = null)
    {
        _volume = volume ?? throw new ArgumentNullException(nameof(volume));
        _destino = destino ?? throw new ArgumentNullException(nameof(destino));
        _provedor = provedor ?? throw new ArgumentNullException(nameof(provedor));
        _definicao = definicao ?? throw new ArgumentNullException(nameof(definicao));
        if (!ReferenceEquals(provedor, definicao))
            throw new InvalidOperationException("A definição e a leitura da senha do espelho têm de ser o mesmo objeto.");

        _gerador = new GeradorSnapshotBanco(versao ?? throw new ArgumentNullException(nameof(versao)));
        _log = log;
        _area = area;
    }

    public async Task<ResultadoBackupEspelho> ExecutarAsync(
        string caminhoBancoVivo,
        string? senhaDigitada,
        string? confirmacao,
        CancellationToken cancellationToken = default,
        bool substituirEspelhoExistente = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminhoBancoVivo);
        cancellationToken.ThrowIfCancellationRequested();

        var digitou = !string.IsNullOrEmpty(senhaDigitada) || !string.IsNullOrEmpty(confirmacao);
        string? senhaNova = null;
        if (digitou)
        {
            if (!string.Equals(senhaDigitada, confirmacao, StringComparison.Ordinal))
                return ResultadoBackupEspelho.Falha(MensagemNaoCoincide);

            if (!RegraSenhaEspelho.AtendeForcaMinima(senhaDigitada))
                return ResultadoBackupEspelho.Falha(RegraSenhaEspelho.Requisitos);

            senhaNova = senhaDigitada;
        }

        var senha = senhaNova ?? _provedor.Obter();
        if (string.IsNullOrEmpty(senha))
            return ResultadoBackupEspelho.Falha(MensagemSemSenha);

        if (!RegraSenhaEspelho.AtendeForcaMinima(senha))
            return ResultadoBackupEspelho.Falha(RegraSenhaEspelho.Requisitos);

        DiagnosticoPastaEspelho diagnostico;
        try
        {
            diagnostico = _volume.Consultar();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ResultadoBackupEspelho.Falha(MensagensPastaEspelho.Para(CodigoFalhaPastaEspelho.Indisponivel));
        }

        if (!diagnostico.AcessoLivre)
            return ResultadoBackupEspelho.Falha(MensagensPastaEspelho.Para(diagnostico.Codigo));

        if (diagnostico.ArquivoNoNomeFinal)
        {
            var leitura = await PresencaDadosReaisUsuario.ClassificarAsync(caminhoBancoVivo, cancellationToken);
            if (leitura == LeituraDadosReais.SemDadosReais)
                return ResultadoBackupEspelho.Falha(MensagemRecusaEspelhoExistente);

            if (!substituirEspelhoExistente)
            {
                var geradoEm = await LerGeradoEmAsync(senha, cancellationToken);
                return ResultadoBackupEspelho.Confirmacao(MensagemConfirmacao, geradoEm);
            }
        }

        // A promoção que já trocou o nome devolve o mesmo código de uma troca que não saiu do lugar.
        // O hash do nome definitivo separa os dois quando a releitura funciona. Se a promoção já
        // ocorreu e a releitura falha, a senha nova entra mesmo assim: o nome definitivo pode ter
        // o ciphertext novo e a memória não pode ficar com a senha antiga.
        // No cancelamento vale o mesmo: hash diferente confirma a senha nova. Foto que não decide
        // (nula, não ausente) e promoção possível adotam a nova e retêm a antiga, com leitura não
        // confirmada. A senha retida fica só em memória.
        var antes = await FotografarAsync(cancellationToken);
        try
        {
            await _gerador.GerarAsync(caminhoBancoVivo, senha, _destino, cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            if (senhaNova is not null)
            {
                try
                {
                    var depoisDoCancelamento = await FotografarAsync(CancellationToken.None);
                    if (DefinitivoMudou(antes, depoisDoCancelamento))
                        _definicao.Definir(senhaNova, senhaNova);
                    else if (depoisDoCancelamento is null && PromocaoEfetuada(ex))
                        AdocaoSenhaLeituraNaoConfirmada.Aplicar(_definicao, senhaNova);
                }
                catch (Exception)
                {
                }
            }

            throw;
        }
        catch (IntegridadeSnapshotException ex)
        {
            return ResultadoBackupEspelho.Falha(ex.Message);
        }
        catch (ArgumentException)
        {
            return ResultadoBackupEspelho.Falha(RegraSenhaEspelho.Requisitos);
        }
        catch (Exception ex)
        {
            RegistroFalhaEspelho.Registrar(_log, ex, senha, senhaDigitada, confirmacao);
            var depois = await FotografarAsync(cancellationToken);
            if (DefinitivoMudou(antes, depois))
                return Publicado(senhaNova);

            if (PromocaoEfetuada(ex))
                return PublicadoSemConfirmarLeitura(senhaNova);

            return ResultadoBackupEspelho.Falha(MensagemMantido);
        }

        return Publicado(senhaNova);
    }

    private ResultadoBackupEspelho Publicado(string? senhaNova)
    {
        if (senhaNova is not null)
            _definicao.Definir(senhaNova, senhaNova);

        var mensagem = HaResiduo() ? MensagemGravadoComReserva : MensagemGravado;
        return ResultadoBackupEspelho.Ok(mensagem);
    }

    private ResultadoBackupEspelho PublicadoSemConfirmarLeitura(string? senhaNova)
    {
        if (senhaNova is not null)
            AdocaoSenhaLeituraNaoConfirmada.Aplicar(_definicao, senhaNova);

        return ResultadoBackupEspelho.Ok(MensagemGravadoLeituraNaoConfirmada);
    }

    private static bool PromocaoEfetuada(Exception excecao)
    {
        for (var atual = excecao; atual is not null; atual = atual.InnerException)
        {
            if (atual is PublicacaoEspelhoException publicacao)
                return publicacao.PromocaoEfetuada;
        }

        return false;
    }

    private bool HaResiduo()
    {
        try
        {
            return _volume.Consultar().ResiduoDesteApp;
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

    private async Task<DateTimeOffset?> LerGeradoEmAsync(string senha, CancellationToken cancellationToken)
    {
        if (_area is null)
            return null;

        string? local = null;
        try
        {
            local = _area.CriarArquivo();
            var copia = await _volume.CopiarParaLocalAsync(_volume.NomeArquivo, local, cancellationToken);
            if (!copia.Sucesso)
                return null;

            var leitura = CofreEspelho.Ler(local, senha);
            CryptographicOperations.ZeroMemory(leitura.Sqlite);
            return leitura.Metadados.GeradoEm;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            if (local is not null)
                _area.Descartar(local);
        }
    }

    private async Task<FotoArquivo?> FotografarAsync(CancellationToken cancellationToken)
    {
        if (_area is null)
            return null;

        string? local = null;
        try
        {
            local = _area.CriarArquivo();
            var copia = await _volume.CopiarParaLocalAsync(_volume.NomeArquivo, local, cancellationToken);
            if (!copia.Sucesso)
            {
                return copia.Codigo == CodigoFalhaPastaEspelho.ArquivoAusente
                    ? FotoArquivo.Ausente
                    : null;
            }

            await using var fluxo = new FileStream(local, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hash = await SHA256.HashDataAsync(fluxo, cancellationToken);
            return FotoArquivo.De(hash);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            if (local is not null)
                _area.Descartar(local);
        }
    }

    private static bool DefinitivoMudou(FotoArquivo? antes, FotoArquivo? depois)
    {
        if (antes is null || depois is null || !depois.Value.Presente || depois.Value.Hash is null)
            return false;

        if (!antes.Value.Presente || antes.Value.Hash is null)
            return true;

        return !antes.Value.Hash.AsSpan().SequenceEqual(depois.Value.Hash);
    }

    private readonly record struct FotoArquivo(bool Presente, byte[]? Hash)
    {
        public static FotoArquivo Ausente { get; } = new(false, null);

        public static FotoArquivo De(byte[] hash) => new(true, hash);
    }
}

public sealed class ResultadoBackupEspelho
{
    private ResultadoBackupEspelho(
        bool sucesso,
        string mensagem,
        bool exigeConfirmacao,
        DateTimeOffset? espelhoExistenteEm)
    {
        if (string.IsNullOrWhiteSpace(mensagem))
            throw new ArgumentException("Informe a mensagem.", nameof(mensagem));
        if (sucesso && exigeConfirmacao)
            throw new ArgumentException("Sucesso não pede confirmação.", nameof(exigeConfirmacao));
        if (!exigeConfirmacao && espelhoExistenteEm is not null)
            throw new ArgumentException("A data do espelho só acompanha a confirmação.", nameof(espelhoExistenteEm));

        Sucesso = sucesso;
        Mensagem = mensagem;
        ExigeConfirmacao = exigeConfirmacao;
        EspelhoExistenteEm = espelhoExistenteEm;
    }

    public bool Sucesso { get; }

    public bool ExigeConfirmacao { get; }

    public DateTimeOffset? EspelhoExistenteEm { get; }

    public string Mensagem { get; }

    public static ResultadoBackupEspelho Ok(string mensagem) => new(true, mensagem, false, null);

    public static ResultadoBackupEspelho Falha(string mensagem) => new(false, mensagem, false, null);

    public static ResultadoBackupEspelho Confirmacao(string mensagem, DateTimeOffset? espelhoExistenteEm)
        => new(false, mensagem, true, espelhoExistenteEm);

    public override string ToString()
        => Sucesso
            ? "ResultadoBackupEspelho"
            : ExigeConfirmacao
                ? "ResultadoBackupEspelho:confirma"
                : "ResultadoBackupEspelho:falha";
}
