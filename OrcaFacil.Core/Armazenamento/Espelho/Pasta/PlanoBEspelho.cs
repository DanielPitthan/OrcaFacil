namespace OrcaFacil.Core.Armazenamento.Espelho.Pasta;

/// <summary>Cache do app. Não é a pasta escolhida pelo usuário.</summary>
public interface IAreaTemporariaEspelho
{
    string CriarArquivo();

    void Descartar(string caminho);
}

/// <summary>Folha de compartilhamento. O arquivo fica no cache do app.</summary>
public interface ICompartilhadorArquivoLocal
{
    Task<ResultadoOperacaoPasta> CompartilharAsync(
        string caminhoLocal,
        string titulo,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Escolha de um arquivo avulso (plano B de importação). A cópia devolvida está no
/// cache do app; o arquivo de origem não é apagado.
/// </summary>
public interface ISeletorArquivoLocal
{
    Task<ResultadoSelecaoArquivo> EscolherAsync(CancellationToken cancellationToken = default);
}

public sealed class ResultadoSelecaoArquivo
{
    private ResultadoSelecaoArquivo(CodigoFalhaPastaEspelho codigo, string? caminhoLocal)
    {
        if (codigo == CodigoFalhaPastaEspelho.Nenhuma && string.IsNullOrWhiteSpace(caminhoLocal))
            throw new ArgumentException("Seleção concluída precisa do arquivo local.");
        if (codigo != CodigoFalhaPastaEspelho.Nenhuma && caminhoLocal is not null)
            throw new ArgumentException("Seleção que falhou não guarda caminho.");

        Codigo = codigo;
        CaminhoLocal = caminhoLocal;
    }

    public CodigoFalhaPastaEspelho Codigo { get; }

    public string? CaminhoLocal { get; }

    public bool Sucesso => Codigo == CodigoFalhaPastaEspelho.Nenhuma;

    public string Mensagem => Sucesso ? "" : MensagensPastaEspelho.Para(Codigo);

    public static ResultadoSelecaoArquivo Ok(string caminhoLocal) => new(CodigoFalhaPastaEspelho.Nenhuma, caminhoLocal);

    public static ResultadoSelecaoArquivo Falha(CodigoFalhaPastaEspelho codigo) => new(codigo, null);

    public override string ToString() => Sucesso ? "ResultadoSelecaoArquivo" : $"ResultadoSelecaoArquivo:{Codigo}";
}

/// <summary>
/// Abre o seletor de árvore, persiste a permissão e guarda a URI. Não desenha a tela
/// de configuração e não apaga arquivo da pasta.
/// </summary>
public interface ISeletorPastaEspelho
{
    Task<ResultadoEscolhaPasta> EscolherAsync(CancellationToken cancellationToken = default);

    ResultadoOperacaoPasta EsquecerEscolhaSemApagarArquivos();
}

public sealed class SeletorPastaEspelhoIndisponivel : ISeletorPastaEspelho
{
    private readonly IMemoriaPastaEspelho _memoria;

    public SeletorPastaEspelhoIndisponivel(IMemoriaPastaEspelho memoria)
    {
        _memoria = memoria ?? throw new ArgumentNullException(nameof(memoria));
    }

    public Task<ResultadoEscolhaPasta> EscolherAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(ResultadoEscolhaPasta.Falha(CodigoFalhaPastaEspelho.Indisponivel));

    public ResultadoOperacaoPasta EsquecerEscolhaSemApagarArquivos()
    {
        _memoria.Esquecer();
        return ResultadoOperacaoPasta.Ok();
    }
}

/// <summary>
/// Plano B para quando a pasta SAF não serve: exportar o espelho pela folha de
/// compartilhamento e importar um arquivo avulso pela mesma publicação atômica.
/// Não substitui o seletor de pasta e não apaga o arquivo de origem da importação.
/// </summary>
public sealed class PlanoBEspelho
{
    public const string TituloCompartilhamento = "Espelho do OrcaFacil";

    private readonly IVolumePastaEspelho _volume;
    private readonly IDestinoEspelho _destino;
    private readonly IAreaTemporariaEspelho _area;
    private readonly ICompartilhadorArquivoLocal _compartilhador;
    private readonly ISeletorArquivoLocal _seletorArquivo;

    public PlanoBEspelho(
        IVolumePastaEspelho volume,
        IDestinoEspelho destino,
        IAreaTemporariaEspelho area,
        ICompartilhadorArquivoLocal compartilhador,
        ISeletorArquivoLocal seletorArquivo)
    {
        _volume = volume ?? throw new ArgumentNullException(nameof(volume));
        _destino = destino ?? throw new ArgumentNullException(nameof(destino));
        _area = area ?? throw new ArgumentNullException(nameof(area));
        _compartilhador = compartilhador ?? throw new ArgumentNullException(nameof(compartilhador));
        _seletorArquivo = seletorArquivo ?? throw new ArgumentNullException(nameof(seletorArquivo));
    }

    public async Task<ResultadoOperacaoPasta> ExportarAsync(CancellationToken cancellationToken = default)
    {
        var diagnostico = _volume.Consultar();
        if (!diagnostico.AcessoLivre)
            return ResultadoOperacaoPasta.Falha(diagnostico.Codigo);
        if (!diagnostico.ArquivoNoNomeFinal)
            return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.ArquivoAusente);

        var local = _area.CriarArquivo();
        var entregue = false;
        try
        {
            var copia = await _volume.CopiarParaLocalAsync(_volume.NomeArquivo, local, cancellationToken);
            if (!copia.Sucesso)
                return copia;

            var compartilhado = await _compartilhador.CompartilharAsync(local, TituloCompartilhamento, cancellationToken);
            entregue = compartilhado.Sucesso;
            return compartilhado;
        }
        finally
        {
            // Sucesso: o outro app ainda pode ler o cache. Falha: o cache sai; a pasta do usuário não.
            if (!entregue)
                _area.Descartar(local);
        }
    }

    public async Task<ResultadoOperacaoPasta> ImportarAsync(CancellationToken cancellationToken = default)
    {
        var escolha = await _seletorArquivo.EscolherAsync(cancellationToken);
        if (!escolha.Sucesso || escolha.CaminhoLocal is null)
            return ResultadoOperacaoPasta.Falha(escolha.Codigo == CodigoFalhaPastaEspelho.Nenhuma
                ? CodigoFalhaPastaEspelho.Indisponivel
                : escolha.Codigo);

        try
        {
            if (_destino is DestinoEspelhoPasta pasta)
                return await pasta.PublicarComResultadoAsync(escolha.CaminhoLocal, cancellationToken);

            await _destino.PublicarAsync(escolha.CaminhoLocal, cancellationToken);
            return ResultadoOperacaoPasta.Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ResultadoOperacaoPasta.Falha(ClassificadorFalhaPastaEspelho.Classificar(SinalFalhaPasta.De(ex)));
        }
        finally
        {
            _area.Descartar(escolha.CaminhoLocal);
        }
    }
}
