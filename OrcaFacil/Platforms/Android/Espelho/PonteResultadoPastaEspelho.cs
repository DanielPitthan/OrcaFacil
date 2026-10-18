using Android.App;
using Android.Content;
using Android.Provider;
using OrcaFacil.Core.Armazenamento.Espelho.Pasta;

namespace OrcaFacil.Platforms.Android.Espelho;

/// <summary>
/// Liga <c>ACTION_OPEN_DOCUMENT_TREE</c> ao <see cref="MainActivity.OnActivityResult"/>.
/// A URI só segue na entrega em memória; este tipo não grava log.
/// </summary>
internal static class PonteResultadoPastaEspelho
{
    internal const int CodigoPedido = 45191;

    private static TaskCompletionSource<EntregaPickerPasta>? _pendente;

    public static Task<EntregaPickerPasta> SolicitarAsync(
        Activity activity,
        string? uriInicial,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activity);
        var tcs = new TaskCompletionSource<EntregaPickerPasta>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (Interlocked.CompareExchange(ref _pendente, tcs, null) is not null)
            return Task.FromResult(EntregaPickerPasta.Ocupada());

        var registro = cancellationToken.Register(() =>
        {
            if (Interlocked.CompareExchange(ref _pendente, null, tcs) == tcs)
                tcs.TrySetResult(EntregaPickerPasta.Cancelada());
        });
        _ = tcs.Task.ContinueWith(
            _ => registro.Dispose(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        try
        {
            var intent = MontarIntent(uriInicial);
            void Abrir()
            {
                try
                {
                    activity.StartActivityForResult(intent, CodigoPedido);
                }
                catch (Exception)
                {
                    if (Interlocked.CompareExchange(ref _pendente, null, tcs) == tcs)
                        tcs.TrySetResult(EntregaPickerPasta.Falha());
                }
            }

            if (Android.OS.Looper.MainLooper is { } principal && Android.OS.Looper.MyLooper() == principal)
                Abrir();
            else
                activity.RunOnUiThread(Abrir);
        }
        catch (Exception)
        {
            if (Interlocked.CompareExchange(ref _pendente, null, tcs) == tcs)
                tcs.TrySetResult(EntregaPickerPasta.Falha());
        }

        return tcs.Task;
    }

    public static void TentarEntregar(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode != CodigoPedido)
            return;

        var tcs = Interlocked.Exchange(ref _pendente, null);
        if (tcs is null)
            return;

        if (resultCode != Result.Ok || data?.Data is null)
        {
            tcs.TrySetResult(EntregaPickerPasta.Cancelada());
            return;
        }

        tcs.TrySetResult(EntregaPickerPasta.Confirmada(data.Data.ToString()));
    }

    private static Intent MontarIntent(string? uriInicial)
    {
        var intent = new Intent(Intent.ActionOpenDocumentTree);
        intent.AddFlags(
            ActivityFlags.GrantReadUriPermission
            | ActivityFlags.GrantWriteUriPermission
            | ActivityFlags.GrantPersistableUriPermission
            | ActivityFlags.GrantPrefixUriPermission);

        if (!UriEscolhaPasta.EhUriDeConteudo(uriInicial))
            return intent;

        try
        {
            var inicial = Android.Net.Uri.Parse(uriInicial);
            if (inicial is not null)
                intent.PutExtra(DocumentsContract.ExtraInitialUri, inicial);
        }
        catch (Exception)
        {
            // Sem URI inicial o seletor abre na raiz. A URI não vai para log.
        }

        return intent;
    }
}

internal sealed class EntregaPickerPasta
{
    public bool Confirmou { get; private init; }

    public bool Ocupado { get; private init; }

    public bool FalhaAoAbrir { get; private init; }

    public string? UriTexto { get; private init; }

    public static EntregaPickerPasta Cancelada() => new();

    public static EntregaPickerPasta Ocupada() => new() { Ocupado = true };

    public static EntregaPickerPasta Falha() => new() { FalhaAoAbrir = true };

    public static EntregaPickerPasta Confirmada(string? uriTexto) => new()
    {
        Confirmou = true,
        UriTexto = uriTexto
    };

    public override string ToString() => "EntregaPickerPasta";
}
