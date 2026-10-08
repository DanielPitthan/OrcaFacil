using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Core.Services;
using OrcaFacil.Data;
using OrcaFacil.Services;
using OrcaFacil.Services.Interfaces;
using Plugin.LocalNotification;
#if ANDROID
using Plugin.LocalNotification.AndroidOption;
using Plugin.LocalNotification.Core.Models.AndroidOption;
#endif
using Serilog;

namespace OrcaFacil;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // App em português (pt-BR): formatação de datas, moeda e números em todas as páginas.
        var culturaPtBr = new CultureInfo("pt-BR");
        CultureInfo.DefaultThreadCurrentCulture = culturaPtBr;
        CultureInfo.DefaultThreadCurrentUICulture = culturaPtBr;

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseLocalNotification(config =>
            {
#if ANDROID
                // No Android o som é definido pelo canal; por isso há um canal com som e outro silencioso.
                config.AddAndroid(android =>
                {
                    android.AddChannel(new AndroidNotificationChannelRequest
                    {
                        Id = NotificationSchedulerService.CanalComSom,
                        Name = "Despesas",
                        Description = "Lembretes de despesas a vencer e vencidas",
                        Importance = AndroidImportance.High
                    });
                    android.AddChannel(new AndroidNotificationChannelRequest
                    {
                        Id = NotificationSchedulerService.CanalSilencioso,
                        Name = "Despesas (silencioso)",
                        Description = "Lembretes de despesas sem som",
                        Importance = AndroidImportance.Low,
                        EnableSound = false,
                        EnableVibration = false
                    });
                });
#endif
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        // Logging — Serilog gravando em arquivo local (AppDataDirectory), além do output de debug.
        var logPath = Path.Combine(FileSystem.AppDataDirectory, "logs", "orcafacil-.log");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .CreateLogger();
        builder.Logging.AddSerilog(Log.Logger, dispose: true);
#if DEBUG
        builder.Logging.AddDebug();
#endif

        // EF Core — DbContextFactory (não Scoped): evita uso concorrente de um único DbContext
        // (não thread-safe) num app MAUI Blazor Hybrid, que tem um único "circuito" de vida longa.
        builder.Services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite(DbPathProvider.GetConnectionString()));

        // Repositórios — stateless (só guardam o IDbContextFactory, que já é Singleton),
        // por isso registrados como Singleton também: evita capturar um serviço "Scoped"
        // dentro dos serviços Singleton (NotificationSchedulerService, ISyncService) que os usam.
        builder.Services.AddSingleton<ITransacaoRepository, TransacaoRepository>();
        builder.Services.AddSingleton<ICategoriaRepository, CategoriaRepository>();
        builder.Services.AddSingleton<IOrcamentoRepository, OrcamentoRepository>();
        builder.Services.AddSingleton<IMetaRepository, MetaRepository>();
        builder.Services.AddSingleton<IMembroFamiliaRepository, MembroFamiliaRepository>();
        builder.Services.AddSingleton<IFechamentoRepository, FechamentoRepository>();
        builder.Services.AddSingleton<ILogEventoRepository, LogEventoRepository>();
        builder.Services.AddSingleton<IManutencaoDadosRepository, ManutencaoDadosRepository>();

        // Serviços de negócio
        builder.Services.AddScoped<IFechamentoService, FechamentoService>();
        builder.Services.AddScoped<ITransacaoService, TransacaoService>();
        builder.Services.AddScoped<ICategoriaService, CategoriaService>();
        builder.Services.AddScoped<IOrcamentoService, OrcamentoService>();
        builder.Services.AddScoped<IMetaService, MetaService>();
        builder.Services.AddScoped<IRelatorioService, RelatorioService>();
        builder.Services.AddScoped<ICsvExportService, CsvExportService>();
        builder.Services.AddScoped<IArmazenamentoService, ArmazenamentoService>();
        builder.Services.AddSingleton<INotificationSchedulerService, NotificationSchedulerService>();
        builder.Services.AddSingleton<ISyncService, MockSyncService>();
        builder.Services.AddSingleton<IPerfilContext, PerfilContext>();
        builder.Services.AddSingleton<IFotoPerfilService, FotoPerfilService>();
        builder.Services.AddSingleton<IConfiguracaoService, ConfiguracaoService>();

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            DbInitializer.InitializeAsync(factory).GetAwaiter().GetResult();

            var perfilContext = scope.ServiceProvider.GetRequiredService<IPerfilContext>();
            perfilContext.InicializarAsync().GetAwaiter().GetResult();

            var notificationScheduler = scope.ServiceProvider.GetRequiredService<INotificationSchedulerService>();
            notificationScheduler.ReagendarTodasAsync().GetAwaiter().GetResult();
        }

        return app;
    }
}
