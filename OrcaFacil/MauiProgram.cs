using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using OrcaFacil.Core.B3;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Exportacao;
using OrcaFacil.Core.Llm;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Core.Services;
using OrcaFacil.Core.Tema;
using OrcaFacil.Data;
using OrcaFacil.Components.Layout;
using OrcaFacil.Services;
using OrcaFacil.Services.Espelho;
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

        // O log de arquivo precisa existir antes das notificações e da migração.
        // Uma falha aqui não pode derrubar a abertura: o logger fica só em memória.
        ConfigurarLogEmArquivo();

        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        ConfigurarNotificacoes(builder);
        builder.ConfigureFonts(fonts =>
        {
            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
        });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices();
        builder.Services.AddScoped<CascoNavegacao>();
        builder.Services.AddScoped<RascunhoLancamento>();
        builder.Services.AddHttpClient(NoticiaService.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/rss+xml");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/xml");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/xml");
        });

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        try
        {
            builder.Logging.AddSerilog(Log.Logger, dispose: true);
        }
        catch (Exception ex)
        {
            RegistrarFalhaDeLog(ex, "Falha ao ligar o Serilog ao host. A abertura segue.");
        }
#if DEBUG
        builder.Logging.AddDebug();
#endif

        // EF Core — DbContextFactory (não Scoped): evita uso concorrente de um único DbContext
        // (não thread-safe) num app MAUI Blazor Hybrid, que tem um único "circuito" de vida longa.
        // O caminho é lido na thread principal; o arquivo orcafacil.db não muda durante o processo.
        var conexaoSqlite = DbPathProvider.GetConnectionString();
        builder.Services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite(conexaoSqlite));

        // Partida do banco e do espelho. A pasta SAF entra em RegistroServicosPastaEspelho, mais abaixo.
        RegistroCoordenadorArmazenamento.Registrar(builder.Services);

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
        builder.Services.AddSingleton<IAtivoFinanceiroRepository, AtivoFinanceiroRepository>();
        builder.Services.AddSingleton<IOperacaoAtivoRepository, OperacaoAtivoRepository>();
        builder.Services.AddSingleton<ICestaRepository, CestaRepository>();
        builder.Services.AddSingleton<ICartaoRepository, CartaoRepository>();

        // Serviços de negócio
        builder.Services.AddSingleton<CopiaDespesasMesService>();
        builder.Services.AddSingleton<EstornoFaturaService>();
        builder.Services.AddSingleton<RendaFixaService>();
        builder.Services.AddSingleton<ImportacaoFaturaCsvService>();
        builder.Services.AddScoped<IFechamentoService, FechamentoService>();
        builder.Services.AddScoped<ITransacaoService, TransacaoService>();
        builder.Services.AddScoped<ICategoriaService, CategoriaService>();
        builder.Services.AddScoped<IOrcamentoService, OrcamentoService>();
        builder.Services.AddScoped<IMetaService, MetaService>();
        builder.Services.AddScoped<IRelatorioService, RelatorioService>();
        builder.Services.AddScoped<ICsvExportService, CsvExportService>();
        builder.Services.AddScoped<IArmazenamentoService, ArmazenamentoService>();
        builder.Services.AddSingleton<ResultadoRealizadoService>();
        builder.Services.AddSingleton<ProventoService>();
        builder.Services.AddSingleton<IParserExtratoB3, ParserExtratoB3>();
        builder.Services.AddSingleton<ImportacaoExtratoB3Service>();
        builder.Services.AddScoped<IAtivoService, AtivoService>();
        builder.Services.AddScoped<IOperacaoAtivoService, OperacaoAtivoService>();
        builder.Services.AddScoped<ICestaService, CestaService>();
        builder.Services.AddScoped<ICartaoService, CartaoService>();
        builder.Services.AddSingleton<INotificationSchedulerService, NotificationSchedulerService>();
        builder.Services.AddSingleton<ISyncService, MockSyncService>();
        builder.Services.AddSingleton<IPerfilContext, PerfilContext>();
        builder.Services.AddSingleton<IFotoPerfilService, FotoPerfilService>();
        builder.Services.AddSingleton<IPreferenciasSimples, PreferenciasMaui>();
        RegistroServicosPastaEspelho.Registrar(builder.Services);
        builder.Services.AddSingleton<ICofreChaveApi, CofreChaveApiSecureStorage>();
        builder.Services.AddSingleton<PreferenciasTema>();
        builder.Services.AddSingleton<IConfiguracaoService, ConfiguracaoService>();
        builder.Services.AddSingleton<ITemaAppService, TemaAppService>();
        builder.Services.AddSingleton<INoticiaService, NoticiaService>();
        // Produção da IA nasce só em Criar: sem redirect e com o prazo no token de 60 s.
        builder.Services.AddSingleton(_ => ClienteMensagens.Criar());
        builder.Services.AddSingleton<InicializacaoApp>();

        var app = builder.Build();

        // Migração, perfil e lembretes não bloqueiam esta thread. Falha de banco ou perfil
        // abre a tela de recuperação; o Serilog grava em AppDataDirectory/logs. O banco não é apagado.
        app.Services.GetRequiredService<InicializacaoApp>().IniciarSemBloquear();

        return app;
    }

    private static void ConfigurarLogEmArquivo()
    {
        try
        {
            var logPath = Path.Combine(FileSystem.AppDataDirectory, "logs", "orcafacil-.log");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
                .CreateLogger();
        }
        catch (Exception ex)
        {
            Log.Logger = new LoggerConfiguration().CreateLogger();
            RegistrarFalhaDeLog(ex, "Não foi possível gravar o log em arquivo. A abertura segue sem esse arquivo.");
        }
    }

    private static void ConfigurarNotificacoes(MauiAppBuilder builder)
    {
        try
        {
            builder.UseLocalNotification(config =>
            {
#if ANDROID
                // No Android o som é definido pelo canal; por isso há um canal com som e outro silencioso.
                config.AddAndroid(android =>
                {
                    try
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
                    }
                    catch (Exception ex)
                    {
                        RegistrarFalhaDeLog(
                            ex,
                            "Falha ao registrar os canais de notificação Android. A abertura segue sem esses canais.");
                    }
                });
#endif
            });
        }
        catch (Exception ex)
        {
            RegistrarFalhaDeLog(ex, "Falha ao configurar as notificações locais. A abertura segue sem elas.");
        }
    }

    private static void RegistrarFalhaDeLog(Exception ex, string mensagem)
    {
        try
        {
            Log.Error("{Mensagem} Tipo: {Tipo}", mensagem, VarreduraPlanilhaExportada.TipoDaFalha(ex));
        }
        catch (Exception logEx)
        {
            System.Diagnostics.Debug.WriteLine(logEx);
        }
    }
}
