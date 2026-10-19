using Microsoft.Maui.ApplicationModel;
using OrcaFacil.Core.Armazenamento.Espelho.Pasta;

namespace OrcaFacil.Platforms.Android.Espelho;

/// <summary>
/// Escolhe a pasta com <c>ACTION_OPEN_DOCUMENT_TREE</c> e só grava a URI depois que
/// <c>takePersistableUriPermission</c> confirma leitura e escrita.
/// </summary>
public sealed class SeletorPastaEspelhoAndroid : ISeletorPastaEspelho
{
    private readonly IMemoriaPastaEspelho _memoria;

    public SeletorPastaEspelhoAndroid(IMemoriaPastaEspelho memoria)
    {
        _memoria = memoria ?? throw new ArgumentNullException(nameof(memoria));
    }

    public async Task<ResultadoEscolhaPasta> EscolherAsync(CancellationToken cancellationToken = default)
    {
        var activity = Platform.CurrentActivity;
        if (activity is null)
            return ResultadoEscolhaPasta.Falha(CodigoFalhaPastaEspelho.Indisponivel);

        var entrega = await PonteResultadoPastaEspelho.SolicitarAsync(activity, _memoria.LerUri(), cancellationToken);
        if (entrega.Ocupado)
            return ResultadoEscolhaPasta.Falha(CodigoFalhaPastaEspelho.Ocupado);
        if (entrega.FalhaAoAbrir)
            return ResultadoEscolhaPasta.Falha(CodigoFalhaPastaEspelho.Indisponivel);

        var leitura = false;
        var escrita = false;
        var persistiu = false;
        if (entrega.Confirmou)
        {
            var resolver = global::Android.App.Application.Context?.ContentResolver ?? activity.ContentResolver;
            (leitura, escrita, persistiu) = PermissaoPastaEspelho.TentarPersistir(resolver, entrega.UriTexto);
        }

        var codigo = AplicacaoEscolhaPasta.Aplicar(
            _memoria,
            entrega.Confirmou,
            leitura,
            escrita,
            persistiu,
            persistiu ? entrega.UriTexto : null);

        return codigo == CodigoFalhaPastaEspelho.Nenhuma
            ? ResultadoEscolhaPasta.Ok()
            : ResultadoEscolhaPasta.Falha(codigo);
    }

    public ResultadoOperacaoPasta EsquecerEscolhaSemApagarArquivos()
    {
        var resolver = Platform.CurrentActivity?.ContentResolver
            ?? global::Android.App.Application.Context?.ContentResolver;
        PermissaoPastaEspelho.Liberar(resolver, _memoria.LerUri());
        _memoria.Esquecer();
        return ResultadoOperacaoPasta.Ok();
    }
}
