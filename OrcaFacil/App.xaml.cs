using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;
using OrcaFacil.Core.Exportacao;
using OrcaFacil.Services.Interfaces;
using Serilog;

namespace OrcaFacil;

public partial class App : Application
{
	private static readonly VarreduraPlanilhaExportada Varredura = new(
		TimeProvider.System,
		static excecao => Log.Error("{Mensagem} Tipo: {Tipo}",
			VarreduraPlanilhaExportada.TextoFalhaLimpeza,
			VarreduraPlanilhaExportada.TipoDaFalha(excecao)));

	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Antes do BlazorWebView: UserAppTheme já está certo no primeiro render do layout.
		var services = activationState?.Context.Services ?? IPlatformApplication.Current?.Services;
		services?.GetService<ITemaAppService>()?.AplicarPreferenciaSalva();

		var janela = new Window(new MainPage()) { Title = "OrcaFacil" };
		janela.Created += (_, _) => Varredura.Disparar(LerCache);
		janela.Activated += (_, _) => Varredura.Disparar(LerCache);
		return janela;
	}

	private static string? LerCache() => FileSystem.CacheDirectory;
}
