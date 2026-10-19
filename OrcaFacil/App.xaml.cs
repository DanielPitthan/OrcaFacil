using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;
using OrcaFacil.Core.Exportacao;
using OrcaFacil.Services.Interfaces;

namespace OrcaFacil;

public partial class App : Application
{
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
		janela.Created += (_, _) => LimparPlanilhasExportadas();
		janela.Resumed += (_, _) => LimparPlanilhasExportadas();
		return janela;
	}

	private static void LimparPlanilhasExportadas()
	{
		try
		{
			ArquivoExportacaoPlanilha.ApagarAnteriores(FileSystem.CacheDirectory);
		}
		catch (Exception)
		{
			// A abertura segue mesmo se o cache da planilha não puder ser limpo.
		}
	}
}
