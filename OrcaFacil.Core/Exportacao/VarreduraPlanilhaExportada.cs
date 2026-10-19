namespace OrcaFacil.Core.Exportacao;

/// <summary>
/// Uma limpeza por vez, fora de quem chamou. Só apaga xlsx do fluxo com
/// mais de dez minutos. Uma falha é registrada e não segue adiante.
/// </summary>
public sealed class VarreduraPlanilhaExportada
{
    private readonly TimeProvider _relogio;
    private readonly Action<Exception> _registrar;
    private int _varrendo;

    public VarreduraPlanilhaExportada(TimeProvider relogio, Action<Exception>? registrar = null)
    {
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
        _registrar = registrar ?? (_ => { });
    }

    public bool EmAndamento => Volatile.Read(ref _varrendo) != 0;

    /// <summary>
    /// Agenda a varredura. Devolve null quando já existe uma em andamento.
    /// A tarefa concluída não propaga a exceção da varredura.
    /// </summary>
    public Task? Disparar(Func<string?> obterCache)
    {
        ArgumentNullException.ThrowIfNull(obterCache);
        if (Interlocked.CompareExchange(ref _varrendo, 1, 0) != 0)
            return null;

        try
        {
            return Task.Run(() => Executar(obterCache));
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _varrendo, 0);
            Registrar(ex);
            return Task.CompletedTask;
        }
    }

    private void Executar(Func<string?> obterCache)
    {
        try
        {
            ArquivoExportacaoPlanilha.ApagarVencidos(obterCache(), _relogio.GetUtcNow());
        }
        catch (Exception ex)
        {
            Registrar(ex);
        }
        finally
        {
            Interlocked.Exchange(ref _varrendo, 0);
        }
    }

    private void Registrar(Exception excecao)
    {
        try
        {
            _registrar(excecao);
        }
        catch (Exception)
        {
            // O log também não pode derrubar a abertura.
        }
    }
}
