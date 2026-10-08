using Android.App;
using Android.Content;
using Microsoft.Extensions.DependencyInjection;
using OrcaFacil.Services.Interfaces;

namespace OrcaFacil.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = true, Label = "Pro Orçamento Boot Receiver")]
[IntentFilter(new[] { Intent.ActionBootCompleted })]
public class BootReceiver : BroadcastReceiver
{
    public override async void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action != Intent.ActionBootCompleted)
            return;

        var services = IPlatformApplication.Current?.Services;
        if (services is null)
            return;

        var scheduler = services.GetService<INotificationSchedulerService>();
        if (scheduler is not null)
            await scheduler.ReagendarTodasAsync();
    }
}
