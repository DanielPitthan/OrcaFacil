using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Data;
using OrcaFacil.Services.Interfaces;

namespace OrcaFacil.Services;

public class ArmazenamentoService : IArmazenamentoService
{
    private readonly IManutencaoDadosRepository _manutencaoRepository;
    private readonly ILogEventoRepository _logRepository;
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly INotificationSchedulerService _notificationScheduler;
    private readonly IFotoPerfilService _fotoPerfilService;
    private readonly IConfiguracaoService _configuracao;
    private readonly IPerfilContext _perfilContext;

    public ArmazenamentoService(
        IManutencaoDadosRepository manutencaoRepository,
        ILogEventoRepository logRepository,
        IDbContextFactory<AppDbContext> contextFactory,
        INotificationSchedulerService notificationScheduler,
        IFotoPerfilService fotoPerfilService,
        IConfiguracaoService configuracao,
        IPerfilContext perfilContext)
    {
        _manutencaoRepository = manutencaoRepository;
        _logRepository = logRepository;
        _contextFactory = contextFactory;
        _notificationScheduler = notificationScheduler;
        _fotoPerfilService = fotoPerfilService;
        _configuracao = configuracao;
        _perfilContext = perfilContext;
    }

    public async Task<ResumoArmazenamentoDto> ObterResumoAsync()
    {
        var banco = DbPathProvider.GetDbPath();
        return new ResumoArmazenamentoDto
        {
            BancoBytes = TamanhoArquivo(banco) + TamanhoArquivo(banco + "-wal") + TamanhoArquivo(banco + "-shm"),
            LogsBytes = TamanhoPasta(Path.Combine(FileSystem.AppDataDirectory, "logs")),
            FotoBytes = TamanhoPasta(_fotoPerfilService.PastaFotos),
            Registros = await _manutencaoRepository.ContarRegistrosAsync()
        };
    }

    public async Task LimparAsync(TipoLimpezaDados tipo)
    {
        if (tipo == TipoLimpezaDados.Nenhum)
            return;

        _notificationScheduler.CancelarTodas();
        await _manutencaoRepository.LimparAsync(tipo);

        if (tipo.HasFlag(TipoLimpezaDados.Tudo))
        {
            _fotoPerfilService.Remover();
            _configuracao.Redefinir();

            await using var context = await _contextFactory.CreateDbContextAsync();
            await SeedData.PopularEssencialAsync(context);
            await _perfilContext.InicializarAsync();

            await _logRepository.RegistrarAsync(TipoLogEvento.LimpezaDados, "Aplicação zerada (todos os dados removidos)");
            return;
        }

        await _notificationScheduler.ReagendarTodasAsync();
        await _logRepository.RegistrarAsync(TipoLogEvento.LimpezaDados, $"Dados removidos: {Descrever(tipo)}");
    }

    private static string Descrever(TipoLimpezaDados tipo)
    {
        var partes = new List<string>();
        if (tipo.HasFlag(TipoLimpezaDados.Lancamentos)) partes.Add("lançamentos");
        if (tipo.HasFlag(TipoLimpezaDados.Orcamentos)) partes.Add("orçamento mensal");
        if (tipo.HasFlag(TipoLimpezaDados.Metas)) partes.Add("metas de economia");
        return string.Join(", ", partes);
    }

    private static long TamanhoArquivo(string caminho) => File.Exists(caminho) ? new FileInfo(caminho).Length : 0;

    private static long TamanhoPasta(string pasta)
        => Directory.Exists(pasta)
            ? new DirectoryInfo(pasta).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length)
            : 0;
}
