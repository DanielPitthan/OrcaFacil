using OrcaFacil.Core.Armazenamento;
using OrcaFacil.Core.Data;

namespace OrcaFacil.Core.Armazenamento.Partida;

/// <summary>
/// Migra o banco vivo com as <see cref="OpcoesPreparoBanco"/> recebidas.
/// Não semeia. O backup de migration pendente continua em <see cref="BackupBanco"/> e não apaga os anteriores.
/// </summary>
public sealed class MigracaoBancoArquivo : IMigracaoBancoVivo
{
    private readonly CaminhoBancoVivo _caminho;
    private readonly IRegistroLog? _log;

    public MigracaoBancoArquivo(CaminhoBancoVivo caminho)
        : this(caminho, log: null)
    {
    }

    public MigracaoBancoArquivo(CaminhoBancoVivo caminho, IRegistroLog? log)
    {
        _caminho = caminho ?? throw new ArgumentNullException(nameof(caminho));
        _log = log;
    }

    public async Task<ResultadoPreparoBanco> MigrarEVerificarAsync(
        OpcoesPreparoBanco opcoes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        await using var contexto = ConexaoBancoArquivo.AbrirEscrita(_caminho.Valor);
        var resultado = await PreparoBanco.MigrarEVerificarAsync(contexto, opcoes, cancellationToken);
        if (_log is not null)
            RegistroPreparoBanco.Registrar(resultado, _log);

        return resultado;
    }
}
