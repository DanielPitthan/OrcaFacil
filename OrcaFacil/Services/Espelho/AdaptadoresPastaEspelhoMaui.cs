using OrcaFacil.Core.Armazenamento.Espelho.Pasta;
using OrcaFacil.Core.Exportacao;

namespace OrcaFacil.Services.Espelho;

/// <summary>Cache do app para cópia de leitura, exportação e importação. Não é a pasta do SAF.</summary>
public sealed class AreaTemporariaEspelhoMaui : IAreaTemporariaEspelho
{
    private readonly AreaTemporariaEspelhoEmDisco _disco = new(
        Path.Combine(FileSystem.CacheDirectory, "orcafacil-espelho"));

    public string CriarArquivo() => _disco.CriarArquivo();

    public void Descartar(string caminho) => _disco.Descartar(caminho);
}

public sealed class CompartilhadorArquivoMaui : ICompartilhadorArquivoLocal
{
    public async Task<ResultadoOperacaoPasta> CompartilharAsync(
        string caminhoLocal,
        string titulo,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = titulo,
                File = Arquivo(caminhoLocal)
            });
            return ResultadoOperacaoPasta.Ok();
        }
        catch (OperationCanceledException)
        {
            return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.Cancelado);
        }
        catch (Exception ex)
        {
            return ResultadoOperacaoPasta.Falha(
                ClassificadorFalhaPastaEspelho.Classificar(SinalFalhaPasta.De(ex)));
        }
    }

    private static ShareFile Arquivo(string caminho)
    {
        if (ArquivoExportacaoPlanilha.NomeEhDesteApp(Path.GetFileName(caminho)))
            return new ShareFile(caminho, ArquivoExportacaoPlanilha.Mime);

        return new ShareFile(caminho);
    }
}

/// <summary>
/// Plano B de importação. Copia o arquivo escolhido para o cache e não apaga a origem.
/// </summary>
public sealed class SeletorArquivoEspelhoMaui : ISeletorArquivoLocal
{
    private readonly IAreaTemporariaEspelho _area;

    public SeletorArquivoEspelhoMaui(IAreaTemporariaEspelho area)
    {
        _area = area ?? throw new ArgumentNullException(nameof(area));
    }

    public async Task<ResultadoSelecaoArquivo> EscolherAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FileResult? escolhido;
        try
        {
            escolhido = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Importar espelho do OrcaFacil"
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ResultadoSelecaoArquivo.Falha(
                ClassificadorFalhaPastaEspelho.Classificar(SinalFalhaPasta.De(ex)));
        }

        if (escolhido is null)
            return ResultadoSelecaoArquivo.Falha(CodigoFalhaPastaEspelho.Cancelado);

        var destino = _area.CriarArquivo();
        try
        {
            await using var entrada = await escolhido.OpenReadAsync();
            await using var saida = new FileStream(
                destino,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous);
            await entrada.CopyToAsync(saida, cancellationToken);
            await saida.FlushAsync(cancellationToken);
            return ResultadoSelecaoArquivo.Ok(destino);
        }
        catch (OperationCanceledException)
        {
            _area.Descartar(destino);
            throw;
        }
        catch (Exception ex)
        {
            _area.Descartar(destino);
            return ResultadoSelecaoArquivo.Falha(
                ClassificadorFalhaPastaEspelho.Classificar(SinalFalhaPasta.De(ex)));
        }
    }
}
