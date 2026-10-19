using Microsoft.Maui.ApplicationModel;
using OrcaFacil.Core.Armazenamento;
using OrcaFacil.Core.Data;
using OrcaFacil.Services.Interfaces;
using Serilog;

namespace OrcaFacil.Services;

/// <summary>
/// Na abertura, o coordenador restaura, migra ou semeia. Depois vêm o perfil e os lembretes.
/// Falha de restauração, banco ou perfil abre a tela de recuperação. Nenhum arquivo é apagado.
/// </summary>
public sealed class InicializacaoApp
{
    private readonly CoordenadorArmazenamento _armazenamento;
    private readonly IPerfilContext _perfil;
    private readonly INotificationSchedulerService _agendador;
    private readonly SemaphoreSlim _porta = new(1, 1);
    private readonly object _estadoLock = new();

    private FaseInicializacao _fase = FaseInicializacao.Pendente;
    private string? _passo;
    private string? _resumoErro;
    private bool _pedirAtualizacao;
    private string? _caminhoBanco;
    private string? _pastaLogs;

    public InicializacaoApp(
        CoordenadorArmazenamento armazenamento,
        IPerfilContext perfil,
        INotificationSchedulerService agendador)
    {
        _armazenamento = armazenamento;
        _perfil = perfil;
        _agendador = agendador;
    }

    public event Action? AoMudar;

    public (FaseInicializacao Fase, string? Passo, string? ResumoErro, bool PedirAtualizacao) LerEstado()
    {
        lock (_estadoLock)
            return (_fase, _passo, _resumoErro, _pedirAtualizacao);
    }

    /// <summary>
    /// Dispara a inicialização e retorna na hora. Quem chama está na thread principal
    /// (CreateMauiApp); o trabalho segue em Task.Run para não travar a UI nem o ANR.
    /// </summary>
    public void IniciarSemBloquear()
    {
        var fase = LerEstado().Fase;
        if (fase is FaseInicializacao.EmAndamento or FaseInicializacao.Pronta)
            return;

        _caminhoBanco ??= DbPathProvider.GetDbPath();
        _pastaLogs ??= Path.Combine(FileSystem.AppDataDirectory, "logs");
        _ = ExecutarAsync();
    }

    public Task TentarNovamenteAsync() => ExecutarAsync();

    private async Task ExecutarAsync()
    {
        if (!await _porta.WaitAsync(0).ConfigureAwait(false))
            return;

        var sucesso = false;
        try
        {
            Publicar(FaseInicializacao.EmAndamento, null, null);
            await Task.Run(ExecutarPassosAsync).ConfigureAwait(false);
            sucesso = true;
        }
        catch (Exception ex)
        {
            var passo = ex is FalhaInicializacaoException falha ? falha.Passo : "inicialização";
            var resumo = FalhasInicializacao.Resumir(ex);
            try
            {
                Log.Error(
                    ex,
                    "Falha no passo {Passo}. Log em {PastaLogs}. O banco {Banco} não foi apagado.",
                    passo,
                    _pastaLogs,
                    _caminhoBanco);
            }
            catch (Exception logEx)
            {
                System.Diagnostics.Debug.WriteLine(logEx);
            }

            Publicar(FaseInicializacao.Falhou, passo, resumo, manterAtualizacao: true);
        }
        finally
        {
            _porta.Release();
        }

        if (sucesso)
        {
            Publicar(FaseInicializacao.Pronta, null, null);
            try
            {
                Log.Information("Inicialização concluída. Banco em {Banco}.", _caminhoBanco);
            }
            catch (Exception logEx)
            {
                System.Diagnostics.Debug.WriteLine(logEx);
            }
        }
    }

    private async Task ExecutarPassosAsync()
    {
        await PassoAsync("armazenamento", PrepararArmazenamentoAsync).ConfigureAwait(false);
        await PassoAsync("inicialização do perfil", () => _perfil.InicializarAsync()).ConfigureAwait(false);

        // Falha do plugin de notificação não tranca o app. Falha de SQLite sobe e vira tela de recuperação.
        try
        {
            await PassoAsync("lembretes", ReagendarNaThreadPrincipalAsync).ConfigureAwait(false);
        }
        catch (Exception ex) when (!FalhasInicializacao.ImpedeAbertura(ex))
        {
            Log.Error(
                ex,
                "Falha no passo lembretes. O aplicativo segue aberto. Banco {Banco} não foi apagado.",
                _caminhoBanco);
        }
    }

    private async Task PrepararArmazenamentoAsync()
    {
        var resultado = await _armazenamento.PrepararAsync().ConfigureAwait(false);
        var pedirAtualizacao = resultado.Acao == AcaoArmazenamento.PedirAtualizacaoDoApp;
        lock (_estadoLock)
            _pedirAtualizacao = pedirAtualizacao;

        if (resultado.Acao is AcaoArmazenamento.TelaDeRecuperacao or AcaoArmazenamento.PedirAtualizacaoDoApp
            || resultado.Falha is not null)
        {
            var passo = pedirAtualizacao ? "atualização do aplicativo" : "armazenamento";
            throw new FalhaInicializacaoException(passo, new InvalidOperationException(resultado.Motivo));
        }
    }

    private async Task ReagendarNaThreadPrincipalAsync()
    {
        // O plugin de notificação no Android espera a thread principal, mas CreateMauiApp
        // já retornou: enfileirar aqui não bloqueia a abertura da janela.
        if (MainThread.IsMainThread)
        {
            await _agendador.ReagendarTodasAsync().ConfigureAwait(false);
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(() => _agendador.ReagendarTodasAsync()).ConfigureAwait(false);
    }

    private static async Task PassoAsync(string passo, Func<Task> acao)
    {
        try
        {
            Log.Information("Inicialização: início do passo {Passo}.", passo);
            await acao().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not FalhaInicializacaoException)
        {
            throw new FalhaInicializacaoException(passo, ex);
        }
    }

    private void Publicar(FaseInicializacao fase, string? passo, string? resumo, bool manterAtualizacao = false)
    {
        Action? handler;
        lock (_estadoLock)
        {
            _fase = fase;
            _passo = passo;
            _resumoErro = resumo;
            if (!manterAtualizacao)
                _pedirAtualizacao = false;
            handler = AoMudar;
        }

        try
        {
            handler?.Invoke();
        }
        catch (Exception ex)
        {
            try
            {
                Log.Warning(ex, "Falha ao avisar a tela sobre a inicialização.");
            }
            catch (Exception logEx)
            {
                System.Diagnostics.Debug.WriteLine(logEx);
            }
        }
    }

}

public sealed class FalhaInicializacaoException : Exception
{
    public string Passo { get; }

    public FalhaInicializacaoException(string passo, Exception inner)
        : base($"Falha no passo {passo}.", inner)
    {
        Passo = passo;
    }
}
