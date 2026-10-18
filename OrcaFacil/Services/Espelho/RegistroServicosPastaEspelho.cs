using Microsoft.Extensions.DependencyInjection;
using OrcaFacil.Core.Armazenamento;
using OrcaFacil.Core.Armazenamento.Espelho;
using OrcaFacil.Core.Armazenamento.Espelho.Pasta;
#if ANDROID
using OrcaFacil.Platforms.Android.Espelho;
#endif

namespace OrcaFacil.Services.Espelho;

/// <summary>
/// Registra a pasta do espelho. Nada aqui é resolvido em <c>CreateMauiApp</c>:
/// <c>InicializacaoApp</c> não depende destes serviços e o startup segue como estava.
/// A tela que chama o seletor fica para o PR seguinte.
/// </summary>
public static class RegistroServicosPastaEspelho
{
    public static void Registrar(IServiceCollection services)
    {
        services.AddSingleton<IMemoriaPastaEspelho, MemoriaPastaEspelho>();
        services.AddSingleton<IProvedorSenhaEspelho, ProvedorSenhaEspelhoAindaNaoInformada>();
        services.AddSingleton(_ => VersaoSchemaAplicacao.DoApp);
        services.AddSingleton<IVersaoSchemaApp>(provedor => provedor.GetRequiredService<VersaoSchemaAplicacao>());
        services.AddSingleton<IAreaTemporariaEspelho, AreaTemporariaEspelhoMaui>();
        services.AddSingleton<ILeitorEspelhoLocal, LeitorEspelhoLocal>();
        services.AddSingleton<ProtocoloPublicacaoEspelho>();
        services.AddSingleton<IDestinoEspelho, DestinoEspelhoPasta>();
        services.AddSingleton<IEspelhoDados, EspelhoNaPastaUsuario>();
        services.AddSingleton<ICompartilhadorArquivoLocal, CompartilhadorArquivoMaui>();
        services.AddSingleton<ISeletorArquivoLocal, SeletorArquivoEspelhoMaui>();
        services.AddSingleton<PlanoBEspelho>();

#if ANDROID
        services.AddSingleton<IVolumePastaEspelho, VolumePastaEspelhoAndroid>();
        services.AddSingleton<ISeletorPastaEspelho, SeletorPastaEspelhoAndroid>();
#else
        services.AddSingleton<IVolumePastaEspelho, VolumePastaEspelhoIndisponivel>();
        services.AddSingleton<ISeletorPastaEspelho, SeletorPastaEspelhoIndisponivel>();
#endif
    }
}
