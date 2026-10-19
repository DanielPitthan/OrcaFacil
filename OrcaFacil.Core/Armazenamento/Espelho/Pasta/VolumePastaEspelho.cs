using OrcaFacil.Core.Armazenamento;

namespace OrcaFacil.Core.Armazenamento.Espelho.Pasta;

/// <summary>
/// Pasta do espelho vista por nome de exibição. A implementação Android fala com o SAF;
/// o teste usa memória. Nenhum método recebe nem devolve a URI da pasta.
/// </summary>
/// <remarks>
/// <see cref="CriarTemporarioAsync"/> só aceita o temporário desta publicação.
/// <see cref="DescartarTemporarioAsync"/> só apaga esse temporário.
/// <see cref="DescartarReservaAposTrocaAsync"/> só apaga a reserva depois que o nome
/// definitivo já existe. <see cref="RenomearAsync"/> não apaga e recusa nome alheio.
/// </remarks>
public interface IVolumePastaEspelho
{
    string NomeArquivo { get; }

    DiagnosticoPastaEspelho Consultar();

    Task<ResultadoOperacaoPasta> CriarTemporarioAsync(
        string nomeTemporario,
        string arquivoOrigemLocal,
        CancellationToken cancellationToken);

    Task<ResultadoRenomeacaoVolume> RenomearAsync(
        string nomeAtual,
        string nomeNovo,
        CancellationToken cancellationToken);

    Task<ResultadoOperacaoPasta> DescartarTemporarioAsync(
        string nomeTemporario,
        CancellationToken cancellationToken);

    Task<ResultadoOperacaoPasta> DescartarReservaAposTrocaAsync(
        string nomeReserva,
        CancellationToken cancellationToken);

    Task<ResultadoOperacaoPasta> CopiarParaLocalAsync(
        string nome,
        string caminhoLocal,
        CancellationToken cancellationToken);
}

/// <summary>Fora do Android não há SAF. Não cria arquivo e não apaga nada.</summary>
public sealed class VolumePastaEspelhoIndisponivel : IVolumePastaEspelho
{
    public string NomeArquivo => FormatoEspelho.NomeArquivoSugerido;

    public DiagnosticoPastaEspelho Consultar()
        => new(CodigoFalhaPastaEspelho.Indisponivel, false);

    public Task<ResultadoOperacaoPasta> CriarTemporarioAsync(
        string nomeTemporario,
        string arquivoOrigemLocal,
        CancellationToken cancellationToken)
        => Recusar();

    public Task<ResultadoRenomeacaoVolume> RenomearAsync(
        string nomeAtual,
        string nomeNovo,
        CancellationToken cancellationToken)
        => Task.FromResult(ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.Indisponivel));

    public Task<ResultadoOperacaoPasta> DescartarTemporarioAsync(
        string nomeTemporario,
        CancellationToken cancellationToken)
        => Recusar();

    public Task<ResultadoOperacaoPasta> DescartarReservaAposTrocaAsync(
        string nomeReserva,
        CancellationToken cancellationToken)
        => Recusar();

    public Task<ResultadoOperacaoPasta> CopiarParaLocalAsync(
        string nome,
        string caminhoLocal,
        CancellationToken cancellationToken)
        => Recusar();

    private static Task<ResultadoOperacaoPasta> Recusar()
        => Task.FromResult(ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.Indisponivel));
}

/// <summary>
/// Leitura do arquivo já copiado para o cache do app. A pasta do usuário não entra aqui.
/// </summary>
public interface ILeitorEspelhoLocal
{
    Task<LeituraEspelho> LerAsync(string caminhoLocal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sem senha, a pasta não é lida. Null significa que a cópia local pode seguir.
    /// </summary>
    LeituraEspelho? RecusaSemAbrirArquivo() => null;
}

public sealed class LeitorEspelhoLocal : ILeitorEspelhoLocal
{
    private readonly IProvedorSenhaEspelho _senha;
    private readonly VersaoSchemaAplicacao _versao;

    public LeitorEspelhoLocal(IProvedorSenhaEspelho senha, VersaoSchemaAplicacao versao)
    {
        _senha = senha ?? throw new ArgumentNullException(nameof(senha));
        _versao = versao ?? throw new ArgumentNullException(nameof(versao));
    }

    public LeituraEspelho? RecusaSemAbrirArquivo()
        => string.IsNullOrEmpty(_senha.Obter())
            ? LeituraEspelho.Ilegivel("Senha do espelho ausente.")
            : null;

    public Task<LeituraEspelho> LerAsync(string caminhoLocal, CancellationToken cancellationToken = default)
        => new EspelhoArquivo(caminhoLocal, _senha, _versao).LerAsync(cancellationToken);
}

/// <summary>Senha ainda não pedida. O PR da tela substitui este registro.</summary>
public sealed class ProvedorSenhaEspelhoAindaNaoInformada : IProvedorSenhaEspelho
{
    public string? Obter() => null;

    public override string ToString() => "ProvedorSenhaEspelhoAindaNaoInformada";
}
