using Microsoft.Extensions.DependencyInjection;
using OrcaFacil.Core.Armazenamento;
using OrcaFacil.Core.Armazenamento.Espelho;
using OrcaFacil.Core.Armazenamento.Espelho.Pasta;
using OrcaFacil.Core.Data;
#if ANDROID
using OrcaFacil.Platforms.Android.Espelho;
#endif

namespace OrcaFacil.Services.Espelho;

/// <summary>
/// Registra a pasta do espelho. A abertura resolve estes serviços pelo coordenador.
/// A tela de Armazenamento chama o seletor de pasta.
/// </summary>
/// <remarks>
/// O <see cref="CoordenadorArmazenamento"/> não entra neste registro. A abertura o monta
/// em <see cref="RegistroCoordenadorArmazenamento"/>, com dez argumentos, nesta ordem: sonda,
/// espelho, versão, restauração, migração, semeadura, limpeza, <see cref="OpcoesPreparoBanco"/>
/// (com <see cref="OpcoesPreparoBanco.VersaoAplicativo"/>), <see cref="IPreservacaoBancoVivo"/>
/// e <see cref="IPresencaPerfil"/>. Null lança <see cref="ArgumentNullException"/>.
/// A senha do espelho é <see cref="ProvedorSenhaEspelhoMemoria"/>, só nesta sessão.
/// <see cref="IMigracaoBancoVivo.MigrarEVerificarAsync"/> recebe as opções.
/// <see cref="IPresencaPerfil.HaMembroAsync"/> só é consultada com schema íntegro;
/// <see cref="ClassificacaoBancoVivo.AplicarPoliticaSeed"/> pode virar
/// <see cref="SituacaoBancoVivo.SemPerfil"/>. <see cref="EspelhoNaPastaUsuario"/> só implementa
/// <see cref="IEspelhoDados"/> e não é <see cref="IPresencaPerfil"/>.
/// </remarks>
public static class RegistroServicosPastaEspelho
{
    public static void Registrar(IServiceCollection services)
    {
        services.AddSingleton<IMemoriaPastaEspelho, MemoriaPastaEspelho>();
        services.AddSingleton<IProvedorSenhaEspelho, ProvedorSenhaEspelhoMemoria>();
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
