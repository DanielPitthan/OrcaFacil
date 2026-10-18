using OrcaFacil.Core.Armazenamento;

namespace OrcaFacil.Core.Armazenamento.Espelho.Pasta;

/// <summary>
/// Traduz o diagnóstico da pasta para o que o coordenador já sabe fazer.
/// Pasta não escolhida, ou pasta livre sem arquivo, é espelho ausente.
/// Permissão revogada, pasta apagada, falta de espaço ou indisponibilidade
/// contam como espelho presente e ilegível, para a matriz não semear no lugar.
/// </summary>
public static class MapeamentoLeituraPastaEspelho
{
    public static bool ExisteParaOCoordenador(DiagnosticoPastaEspelho diagnostico)
    {
        ArgumentNullException.ThrowIfNull(diagnostico);
        if (diagnostico.AcessoLivre)
            return diagnostico.ArquivoNoNomeFinal;

        return diagnostico.Codigo != CodigoFalhaPastaEspelho.PastaNaoEscolhida;
    }

    public static LeituraEspelho? Bloqueio(DiagnosticoPastaEspelho diagnostico)
    {
        ArgumentNullException.ThrowIfNull(diagnostico);
        if (diagnostico.AcessoLivre)
            return null;

        return LeituraEspelho.Ilegivel(MensagensPastaEspelho.Para(diagnostico.Codigo));
    }
}

/// <summary>
/// <see cref="IEspelhoDados"/> da pasta escolhida. A leitura copia o espelho para o
/// cache do app e delega ao leitor local. Não apaga o arquivo da pasta.
/// </summary>
public sealed class EspelhoNaPastaUsuario : IEspelhoDados
{
    private readonly IVolumePastaEspelho _volume;
    private readonly ILeitorEspelhoLocal _leitor;
    private readonly IAreaTemporariaEspelho _area;

    public EspelhoNaPastaUsuario(
        IVolumePastaEspelho volume,
        ILeitorEspelhoLocal leitor,
        IAreaTemporariaEspelho area)
    {
        _volume = volume ?? throw new ArgumentNullException(nameof(volume));
        _leitor = leitor ?? throw new ArgumentNullException(nameof(leitor));
        _area = area ?? throw new ArgumentNullException(nameof(area));
    }

    public bool Existe()
    {
        try
        {
            return MapeamentoLeituraPastaEspelho.ExisteParaOCoordenador(_volume.Consultar());
        }
        catch (Exception)
        {
            // Consulta que estoura não pode parecer "não há espelho": o coordenador semearia.
            return true;
        }
    }

    public async Task<LeituraEspelho> LerAsync(CancellationToken cancellationToken = default)
    {
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
            return LeituraEspelho.Ilegivel(MensagensPastaEspelho.Para(CodigoFalhaPastaEspelho.Indisponivel));
        }

        var bloqueio = MapeamentoLeituraPastaEspelho.Bloqueio(diagnostico);
        if (bloqueio is not null)
            return bloqueio;

        if (!diagnostico.ArquivoNoNomeFinal)
            return LeituraEspelho.Ilegivel(MensagensPastaEspelho.Para(CodigoFalhaPastaEspelho.ArquivoAusente));

        var local = _area.CriarArquivo();
        try
        {
            var copia = await _volume.CopiarParaLocalAsync(_volume.NomeArquivo, local, cancellationToken);
            if (!copia.Sucesso)
                return LeituraEspelho.Ilegivel(copia.Mensagem);

            return await _leitor.LerAsync(local, cancellationToken);
        }
        finally
        {
            _area.Descartar(local);
        }
    }

    public override string ToString() => "EspelhoNaPastaUsuario";
}
