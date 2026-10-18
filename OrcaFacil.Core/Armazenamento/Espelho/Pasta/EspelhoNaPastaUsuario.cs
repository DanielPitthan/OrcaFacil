using OrcaFacil.Core.Armazenamento;

namespace OrcaFacil.Core.Armazenamento.Espelho.Pasta;

/// <summary>
/// Traduz o diagnóstico da pasta para o que o coordenador já sabe fazer.
/// Pasta não escolhida, ou pasta livre sem arquivo deste app, é espelho ausente.
/// Reserva ou temporário deste app sem o nome definitivo conta como presente e ilegível:
/// a matriz abre a recuperação e não semeia. Permissão revogada, pasta apagada,
/// falta de espaço ou indisponibilidade também não parecem instalação nova.
/// </summary>
public static class MapeamentoLeituraPastaEspelho
{
    public static bool ExisteParaOCoordenador(DiagnosticoPastaEspelho diagnostico)
    {
        ArgumentNullException.ThrowIfNull(diagnostico);
        if (diagnostico.AcessoLivre)
            return diagnostico.ArquivoNoNomeFinal || diagnostico.ResiduoDesteApp;

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
        {
            if (diagnostico.ResiduoDesteApp)
            {
                if (diagnostico.NomeReserva is not null)
                    await TentarLerReservaSemApagarAsync(diagnostico.NomeReserva, cancellationToken);

                return LeituraEspelho.Ilegivel(
                    MensagensPastaEspelho.Para(CodigoFalhaPastaEspelho.EspelhoPreservadoForaDoNome));
            }

            return LeituraEspelho.Ilegivel(MensagensPastaEspelho.Para(CodigoFalhaPastaEspelho.ArquivoAusente));
        }

        return await LerNomeAsync(_volume.NomeArquivo, cancellationToken);
    }

    private async Task TentarLerReservaSemApagarAsync(string nomeReserva, CancellationToken cancellationToken)
    {
        try
        {
            await LerNomeAsync(nomeReserva, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // A reserva fica na pasta. A leitura do coordenador continua ilegível.
        }
    }

    private async Task<LeituraEspelho> LerNomeAsync(string nome, CancellationToken cancellationToken)
    {
        var local = _area.CriarArquivo();
        try
        {
            var copia = await _volume.CopiarParaLocalAsync(nome, local, cancellationToken);
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
