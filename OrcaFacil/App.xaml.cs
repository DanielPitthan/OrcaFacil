using Microsoft.Extensions.DependencyInjection;
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

		return new Window(new MainPage()) { Title = "OrcaFacil" };
	}
}
