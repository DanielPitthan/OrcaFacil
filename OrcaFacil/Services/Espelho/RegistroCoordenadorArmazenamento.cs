using Microsoft.Extensions.DependencyInjection;
using OrcaFacil.Core.Armazenamento;
using OrcaFacil.Core.Armazenamento.Partida;
using OrcaFacil.Core.Data;
using OrcaFacil.Data;
using Serilog;

namespace OrcaFacil.Services.Espelho;

/// <summary>
/// Liga o coordenador na abertura. A senha continua
/// <see cref="ProvedorSenhaEspelhoAindaNaoInformada"/> até a tela de Armazenamento.
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
        services.AddSingleton<ISemeaduraDados, SemeaduraDadosApp>();
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

/// <summary>
/// Dados iniciais só depois que o coordenador criou o schema. Restauração que falha não chega aqui.
/// </summary>
public sealed class SemeaduraDadosApp : ISemeaduraDados
{
    private readonly CaminhoBancoVivo _caminho;
    private readonly IRegistroLog _log;

    public SemeaduraDadosApp(CaminhoBancoVivo caminho, IRegistroLog log)
    {
        _caminho = caminho ?? throw new ArgumentNullException(nameof(caminho));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task SemearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var contexto = ConexaoBancoArquivo.AbrirEscrita(_caminho.Valor);
        if (await PoliticaSemeadura.CriarPerfilMinimoSeHaLancamentoAsync(contexto, _log, cancellationToken))
            return;

        await SeedData.PopularAsync(contexto);
    }
}
