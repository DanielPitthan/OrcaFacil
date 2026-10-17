using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.ApplicationModel;
using OrcaFacil.Core.Data;
using OrcaFacil.Data;
using OrcaFacil.Services.Interfaces;
using Serilog;

namespace OrcaFacil.Services;

public enum FaseInicializacao
{
    Pendente,
    EmAndamento,
    Pronta,
    Falhou
}

/// <summary>
/// Roda migração, perfil e lembretes fora da thread principal.
/// Falha de banco ou de perfil abre a tela de recuperação. O arquivo do banco não é apagado.
/// </summary>
public sealed class InicializacaoApp
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IPerfilContext _perfil;
    private readonly INotificationSchedulerService _agendador;
    private readonly SemaphoreSlim _porta = new(1, 1);
    private readonly object _estadoLock = new();

    private FaseInicializacao _fase = FaseInicializacao.Pendente;
    private string? _passo;
    private string? _resumoErro;
    private string? _caminhoBanco;
    private string? _pastaLogs;

    public InicializacaoApp(
        IDbContextFactory<AppDbContext> factory,
        IPerfilContext perfil,
        INotificationSchedulerService agendador)
    {
        _factory = factory;
        _perfil = perfil;
        _agendador = agendador;
    }

    public event Action? AoMudar;

    public (FaseInicializacao Fase, string? Passo, string? ResumoErro) LerEstado()
    {
        lock (_estadoLock)
            return (_fase, _passo, _resumoErro);
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
            var resumo = Resumir(ex);
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

            Publicar(FaseInicializacao.Falhou, passo, resumo);
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
        await PassoAsync("migração do banco", () => DbInitializer.InitializeAsync(_factory)).ConfigureAwait(false);
        await PassoAsync("inicialização do perfil", () => _perfil.InicializarAsync()).ConfigureAwait(false);

        // Falha do plugin de notificação não tranca o app. Falha de SQLite sobe e vira tela de recuperação.
        try
        {
            await PassoAsync("lembretes", ReagendarNaThreadPrincipalAsync).ConfigureAwait(false);
        }
        catch (Exception ex) when (!EhFalhaDeDados(ex))
        {
            Log.Error(
                ex,
                "Falha no passo lembretes. O aplicativo segue aberto. Banco {Banco} não foi apagado.",
                _caminhoBanco);
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

    private void Publicar(FaseInicializacao fase, string? passo, string? resumo)
    {
        Action? handler;
        lock (_estadoLock)
        {
            _fase = fase;
            _passo = passo;
            _resumoErro = resumo;
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

    private static bool EhFalhaDeDados(Exception ex)
    {
        for (var atual = ex; atual is not null; atual = atual.InnerException)
        {
            if (atual is DbException or DbUpdateException)
                return true;
        }

        return false;
    }

    internal static string Resumir(Exception ex)
    {
        var mensagens = new List<string>();
        for (var atual = ex; atual is not null && mensagens.Count < 3; atual = atual.InnerException)
        {
            if (string.IsNullOrWhiteSpace(atual.Message))
                continue;

            var texto = atual.Message.Trim();
            if (mensagens.Count > 0 && string.Equals(mensagens[^1], texto, StringComparison.Ordinal))
                continue;

            mensagens.Add(texto);
        }

        var resumo = mensagens.Count == 0 ? ex.GetType().Name : string.Join(" — ", mensagens);
        const int limite = 500;
        return resumo.Length <= limite ? resumo : resumo[..limite] + "…";
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
