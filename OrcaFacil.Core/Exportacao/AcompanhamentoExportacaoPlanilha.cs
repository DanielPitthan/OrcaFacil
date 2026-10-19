namespace OrcaFacil.Core.Exportacao;

/// <summary>
/// Liga um painel ao orquestrador. O que aparece na tela é lido do
/// orquestrador; no fim da exportação o painel pede um novo render.
/// </summary>
public sealed class AcompanhamentoExportacaoPlanilha : IDisposable
{
    private readonly OrquestradorExportacaoPlanilha _orquestrador;
    private readonly Func<Task> _renderizar;
    private int _vivo = 1;
    private Task _notificacao = Task.CompletedTask;

    public AcompanhamentoExportacaoPlanilha(
        OrquestradorExportacaoPlanilha orquestrador,
        Func<Task> renderizar)
    {
        _orquestrador = orquestrador ?? throw new ArgumentNullException(nameof(orquestrador));
        _renderizar = renderizar ?? throw new ArgumentNullException(nameof(renderizar));
        _orquestrador.EstadoMudou += AoMudar;
    }

    public bool Exportando => _orquestrador.Exportando;

    public string? Aviso => _orquestrador.Aviso;

    public bool FolhaAberta => _orquestrador.FolhaAberta;

    public Exception? FalhaNotificacao { get; private set; }

    public Task Notificacao => _notificacao;

    public void RegistrarFalha(Exception excecao)
    {
        ArgumentNullException.ThrowIfNull(excecao);
        FalhaNotificacao ??= excecao;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _vivo, 0) == 0)
            return;

        _orquestrador.EstadoMudou -= AoMudar;
    }

    private void AoMudar()
    {
        if (Volatile.Read(ref _vivo) == 0)
            return;

        _notificacao = NotificarAsync();
    }

    private async Task NotificarAsync()
    {
        if (Volatile.Read(ref _vivo) == 0)
            return;

        try
        {
            await _renderizar();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            RegistrarFalha(ex);
        }
    }
}
