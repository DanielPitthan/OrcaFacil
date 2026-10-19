using OrcaFacil.Core.Armazenamento.Espelho.Pasta;

namespace OrcaFacil.Core.Armazenamento.Espelho;

/// <summary>
/// Gera o snapshot cifrado e publica na pasta escolhida.
/// Senha fraca, pasta indisponível ou falha no meio não chamam uma troca que apague o espelho anterior.
/// A senha nova só fica em memória depois que a publicação conclui.
/// </summary>
public sealed class BackupEspelhoAgora
{
    public const string MensagemGravado = "Backup gravado na pasta escolhida.";

    public const string MensagemSemSenha = "Defina a senha do espelho antes do backup. Nada foi publicado.";

    public const string MensagemNaoCoincide = "As senhas não coincidem. Nada foi publicado.";

    public const string MensagemMantido = "Não foi possível publicar o espelho. O arquivo anterior foi mantido.";

    private readonly IVolumePastaEspelho _volume;
    private readonly IDestinoEspelho _destino;
    private readonly IProvedorSenhaEspelho _provedor;
    private readonly IDefinicaoSenhaEspelho _definicao;
    private readonly GeradorSnapshotBanco _gerador;

    public BackupEspelhoAgora(
        IVolumePastaEspelho volume,
        IDestinoEspelho destino,
        IProvedorSenhaEspelho provedor,
        IDefinicaoSenhaEspelho definicao,
        VersaoSchemaAplicacao versao)
    {
        _volume = volume ?? throw new ArgumentNullException(nameof(volume));
        _destino = destino ?? throw new ArgumentNullException(nameof(destino));
        _provedor = provedor ?? throw new ArgumentNullException(nameof(provedor));
        _definicao = definicao ?? throw new ArgumentNullException(nameof(definicao));
        if (!ReferenceEquals(provedor, definicao))
            throw new InvalidOperationException("A definição e a leitura da senha do espelho têm de ser o mesmo objeto.");

        _gerador = new GeradorSnapshotBanco(versao ?? throw new ArgumentNullException(nameof(versao)));
    }

    public async Task<ResultadoBackupEspelho> ExecutarAsync(
        string caminhoBancoVivo,
        string? senhaDigitada,
        string? confirmacao,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminhoBancoVivo);
        cancellationToken.ThrowIfCancellationRequested();

        var digitou = !string.IsNullOrEmpty(senhaDigitada) || !string.IsNullOrEmpty(confirmacao);
        string? senhaNova = null;
        if (digitou)
        {
            if (!string.Equals(senhaDigitada, confirmacao, StringComparison.Ordinal))
                return ResultadoBackupEspelho.Falha(MensagemNaoCoincide, senhaDigitada, confirmacao);

            if (!RegraSenhaEspelho.AtendeForcaMinima(senhaDigitada))
                return ResultadoBackupEspelho.Falha(RegraSenhaEspelho.Requisitos, senhaDigitada, confirmacao);

            senhaNova = senhaDigitada;
        }

        var senha = senhaNova ?? _provedor.Obter();
        if (string.IsNullOrEmpty(senha))
            return ResultadoBackupEspelho.Falha(MensagemSemSenha, senhaDigitada, confirmacao);

        if (!RegraSenhaEspelho.AtendeForcaMinima(senha))
            return ResultadoBackupEspelho.Falha(RegraSenhaEspelho.Requisitos, senha, senhaDigitada, confirmacao);

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
            return ResultadoBackupEspelho.Falha(
                MensagensPastaEspelho.Para(CodigoFalhaPastaEspelho.Indisponivel),
                senha,
                senhaDigitada,
                confirmacao);
        }

        if (!diagnostico.AcessoLivre)
        {
            return ResultadoBackupEspelho.Falha(
                MensagensPastaEspelho.Para(diagnostico.Codigo),
                senha,
                senhaDigitada,
                confirmacao);
        }

        try
        {
            await _gerador.GerarAsync(caminhoBancoVivo, senha, _destino, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IntegridadeSnapshotException ex)
        {
            return ResultadoBackupEspelho.Falha(ex.Message, senha, senhaDigitada, confirmacao);
        }
        catch (ArgumentException)
        {
            return ResultadoBackupEspelho.Falha(RegraSenhaEspelho.Requisitos, senha, senhaDigitada, confirmacao);
        }
        catch (IOException ex)
        {
            return ResultadoBackupEspelho.Falha(
                string.IsNullOrWhiteSpace(ex.Message) ? MensagemMantido : ex.Message,
                senha,
                senhaDigitada,
                confirmacao);
        }
        catch (Exception)
        {
            return ResultadoBackupEspelho.Falha(MensagemMantido, senha, senhaDigitada, confirmacao);
        }

        if (senhaNova is not null)
            _definicao.Definir(senhaNova, senhaNova);

        return ResultadoBackupEspelho.Ok(MensagemGravado, senha);
    }
}

public sealed class ResultadoBackupEspelho
{
    private ResultadoBackupEspelho(bool sucesso, string mensagem)
    {
        if (string.IsNullOrWhiteSpace(mensagem))
            throw new ArgumentException("Informe a mensagem.", nameof(mensagem));

        Sucesso = sucesso;
        Mensagem = mensagem;
    }

    public bool Sucesso { get; }

    public string Mensagem { get; }

    public static ResultadoBackupEspelho Ok(string mensagem, string? senha)
        => new(true, TextoSemSegredoEspelho.Limpar(mensagem, "Backup gravado.", senha));

    public static ResultadoBackupEspelho Falha(string mensagem, params string?[] segredos)
        => new(false, TextoSemSegredoEspelho.Limpar(mensagem, "Nada foi publicado.", segredos));

    public override string ToString() => Sucesso ? "ResultadoBackupEspelho" : "ResultadoBackupEspelho:falha";
}
