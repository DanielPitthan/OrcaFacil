namespace OrcaFacil.Core.Exportacao;

/// <summary>
/// Uma exportação por vez, fora da thread de quem chamou.
/// A flag liga antes de qualquer espera e o try cobre a montagem da
/// configuração e a leitura do cache. O arquivo só some no Resume,
/// na abertura ou no começo da exportação seguinte.
/// </summary>
public sealed class OrquestradorExportacaoPlanilha
{
    private readonly TimeProvider _relogio;
    private readonly Func<string> _novoSufixo;
    private volatile bool _exportando;

    public OrquestradorExportacaoPlanilha(TimeProvider relogio, Func<string>? novoSufixo = null)
    {
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
        _novoSufixo = novoSufixo ?? ArquivoExportacaoPlanilha.NovoSufixo;
    }

    public bool Exportando => _exportando;

    public string? Aviso { get; private set; }

    public bool FolhaAberta { get; private set; }

    public string? Caminho { get; private set; }

    public async Task<ResultadoExportacao> ExportarAsync(
        Func<ConfiguracaoExportavel> montar,
        Func<string> obterCache,
        Action<string, ConfiguracaoExportavel> gravar,
        Func<string, Task<DesfechoCompartilhamentoPlanilha>> compartilhar)
    {
        ArgumentNullException.ThrowIfNull(montar);
        ArgumentNullException.ThrowIfNull(obterCache);
        ArgumentNullException.ThrowIfNull(gravar);
        ArgumentNullException.ThrowIfNull(compartilhar);

        if (_exportando)
            return ResultadoExportacao.Ignorado;

        _exportando = true;
        string? reservado = null;
        try
        {
            Aviso = null;
            FolhaAberta = false;
            Caminho = null;

            var configuracao = montar();
            var cache = obterCache();
            var caminho = await Task.Run(() => Preparar(cache, configuracao, gravar));
            reservado = caminho;
            Caminho = caminho;

            var desfecho = await compartilhar(caminho);
            FolhaAberta = desfecho == DesfechoCompartilhamentoPlanilha.FolhaAberta;
            Aviso = desfecho switch
            {
                DesfechoCompartilhamentoPlanilha.FolhaAberta => MensagemExportacaoPlanilha.ProntaParaCompartilhar,
                DesfechoCompartilhamentoPlanilha.Cancelado => null,
                _ => MensagemExportacaoPlanilha.Compartilhamento
            };
            return Atual(true);
        }
        catch (Exception ex)
        {
            FolhaAberta = false;
            Aviso = MensagemExportacaoPlanilha.Para(ex) ?? MensagemExportacaoPlanilha.Generica;
            return Atual(true);
        }
        finally
        {
            _exportando = false;
            ArquivoExportacaoPlanilha.Liberar(reservado);
        }
    }

    private string Preparar(string cache, ConfiguracaoExportavel configuracao, Action<string, ConfiguracaoExportavel> gravar)
    {
        ArquivoExportacaoPlanilha.ApagarAnteriores(cache);
        Directory.CreateDirectory(cache);
        var agora = _relogio.GetLocalNow().DateTime;
        var caminho = ArquivoExportacaoPlanilha.Caminho(cache, agora, _novoSufixo());
        ArquivoExportacaoPlanilha.Reservar(caminho);
        try
        {
            gravar(caminho, configuracao);
            return caminho;
        }
        catch
        {
            ArquivoExportacaoPlanilha.ApagarSeForDesteApp(caminho, cache);
            ArquivoExportacaoPlanilha.Liberar(caminho);
            throw;
        }
    }

    private ResultadoExportacao Atual(bool executou) => new(executou, Aviso, FolhaAberta, Caminho);
}

public sealed record ResultadoExportacao(bool Executou, string? Aviso, bool FolhaAberta, string? Caminho)
{
    public static ResultadoExportacao Ignorado { get; } = new(false, null, false, null);
}

public enum DesfechoCompartilhamentoPlanilha
{
    FolhaAberta,
    Cancelado,
    Falhou
}
