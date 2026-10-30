using OrcaFacil.Core.Categorias;
using OrcaFacil.Services;

namespace OrcaFacil.Platforms.Android;

/// <summary>
/// O voltar do Android, com a paleta aberta, cancela só esse diálogo.
/// O callback entra por último no dispatcher, na frente do WebView, e não chama GoBack.
/// </summary>
internal sealed class VoltarPaletaAndroid : IVoltarPaleta
{
    private readonly SessaoVoltarPaleta _sessao = new();
    private CallbackVoltar? _callback;

    public void Abrir(Action fechar)
    {
        _sessao.Abrir(fechar);
        ExecutarNaUi(Armar);
    }

    public void Fechar(Action fechar)
    {
        _sessao.Fechar(fechar);
        if (!_sessao.Aberta)
            ExecutarNaUi(Desarmar);
    }

    private void Armar()
    {
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is not global::AndroidX.Activity.ComponentActivity activity)
            return;

        Desarmar();
        _callback = new CallbackVoltar(_sessao, activity.OnBackPressedDispatcher);
        activity.OnBackPressedDispatcher.AddCallback(activity, _callback);
    }

    private void Desarmar()
    {
        var callback = _callback;
        _callback = null;
        if (callback is null)
            return;

        callback.Remove();
        AgendarDescarte(callback);
    }

    private static void AgendarDescarte(CallbackVoltar callback)
    {
        var principal = global::Android.OS.Looper.MainLooper;
        if (principal is null)
        {
            callback.Dispose();
            return;
        }

        // O descarte no meio do HandleOnBackPressed solta o peer JNI ainda em uso.
        new global::Android.OS.Handler(principal).Post(() => callback.Dispose());
    }

    private static void ExecutarNaUi(Action acao)
    {
        if (global::Android.OS.Looper.MainLooper is { } principal && global::Android.OS.Looper.MyLooper() == principal)
        {
            acao();
            return;
        }

        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is global::Android.App.Activity activity)
            activity.RunOnUiThread(acao);
    }

    private sealed class CallbackVoltar : global::AndroidX.Activity.OnBackPressedCallback
    {
        private readonly SessaoVoltarPaleta _sessao;
        private readonly global::AndroidX.Activity.OnBackPressedDispatcher _dispatcher;

        public CallbackVoltar(SessaoVoltarPaleta sessao, global::AndroidX.Activity.OnBackPressedDispatcher dispatcher)
            : base(true)
        {
            _sessao = sessao;
            _dispatcher = dispatcher;
        }

        public override void HandleOnBackPressed()
        {
            if (_sessao.TentarConsumir())
                return;

            Enabled = false;
            _dispatcher.OnBackPressed();
        }
    }
}
