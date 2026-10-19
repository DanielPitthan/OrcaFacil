using Microsoft.Extensions.DependencyInjection;
using OrcaFacil.Core.Armazenamento;
using OrcaFacil.Core.Armazenamento.Espelho;
using OrcaFacil.Core.Armazenamento.Partida;
using OrcaFacil.Core.Data;
using OrcaFacil.Data;
using Serilog;

namespace OrcaFacil.Services.Espelho;

/// <summary>
/// Liga o coordenador na abertura. A senha do espelho é
/// <see cref="ProvedorSenhaEspelhoMemoria"/>, só nesta sessão.
/// Não registra cliente de IA nem o cofre da chave.
/// </summary>
public static class RegistroCoordenadorArmazenamento
{
    public static void Registrar(IServiceCollection services)
    {
        services.AddSingleton(new CaminhoBancoVivo(DbPathProvider.GetDbPath()));
        services.AddSingleton(new OpcoesPreparoBanco { VersaoAplicativo = VersaoAplicativo() });
        services.AddSingleton<IRegistroLog>(_ => new AdaptadorLog());
        services.AddSingleton<ISondaBancoVivo, SondaBancoArquivo>();
        services.AddSingleton<IPresencaPerfil, PresencaPerfilArquivo>();
        services.AddSingleton<IMigracaoBancoVivo>(provedor => new MigracaoBancoArquivo(
            provedor.GetRequiredService<CaminhoBancoVivo>(),
            provedor.GetRequiredService<IRegistroLog>()));
        services.AddSingleton<IPreservacaoBancoVivo, PreservacaoBancoArquivo>();
        services.AddSingleton<IRestauracaoEspelho, RestauracaoEspelhoDaPasta>();
        services.AddSingleton<ISemeaduraDados>(provedor => new SemeaduraDadosApp(
            provedor.GetRequiredService<CaminhoBancoVivo>(),
            provedor.GetRequiredService<IRegistroLog>(),
            static (contexto, _) => SeedData.PopularAsync(contexto)));
        services.AddSingleton<ILimpezaTotalDados, LimpezaTotalForaDaAbertura>();
        services.AddSingleton<CoordenadorArmazenamento>();
    }

    private static string VersaoAplicativo()
    {
        try
        {
            return AppInfo.VersionString;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return "desconhecida";
        }
    }

    private sealed class AdaptadorLog : IRegistroLog
    {
        public void Informacao(string mensagem, object?[] argumentos)
        {
            try
            {
                Log.Information(mensagem, argumentos);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        public void Erro(Exception? excecao, string mensagem, object?[] argumentos)
        {
            try
            {
                Log.Error(excecao, mensagem, argumentos);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }
    }
}

