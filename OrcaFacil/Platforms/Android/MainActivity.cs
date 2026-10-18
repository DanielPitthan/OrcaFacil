using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Views;
using OrcaFacil.Platforms.Android.Espelho;

namespace OrcaFacil;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, WindowSoftInputMode = SoftInput.AdjustResize, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        PonteResultadoPastaEspelho.TentarEntregar(requestCode, resultCode, data);
        base.OnActivityResult(requestCode, resultCode, data);
    }
}
