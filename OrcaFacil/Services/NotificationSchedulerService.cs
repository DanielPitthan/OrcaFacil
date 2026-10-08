using Microsoft.Extensions.Logging;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Services.Interfaces;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using Plugin.LocalNotification.Core.Models.AndroidOption;

namespace OrcaFacil.Services;

public class NotificationSchedulerService : INotificationSchedulerService
{
    public const string CanalComSom = "despesas";
    public const string CanalSilencioso = "despesas_silencioso";

    private const int HoraAviso = 9;
    private const int SlotsPorTransacao = 10;
    private const int DiasMaximoVencida = 7;
    private const int JanelaFuturaDias = 30;

    private readonly ITransacaoRepository _transacaoRepository;
    private readonly IConfiguracaoService _configuracao;
    private readonly ILogger<NotificationSchedulerService> _logger;

    public NotificationSchedulerService(
        ITransacaoRepository transacaoRepository,
        IConfiguracaoService configuracao,
        ILogger<NotificationSchedulerService> logger)
    {
        _transacaoRepository = transacaoRepository;
        _configuracao = configuracao;
        _logger = logger;
    }

    public Task AgendarLembreteAsync(Transacao transacao) => AgendarAsync(transacao, permitirImediato: true);

    public Task CancelarLembreteAsync(int transacaoId)
    {
        try
        {
            var ids = Enumerable.Range(0, SlotsPorTransacao)
                .Select(slot => transacaoId * SlotsPorTransacao + slot)
                .Append(transacaoId) // id usado pelas versões anteriores do app
                .ToArray();
            LocalNotificationCenter.Current.Cancel(ids);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao cancelar lembretes do lançamento {Id}", transacaoId);
        }
        return Task.CompletedTask;
    }

    public void CancelarTodas()
    {
        try
        {
            LocalNotificationCenter.Current.CancelAll();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao cancelar todas as notificações");
        }
    }

    public async Task ReagendarTodasAsync()
    {
        CancelarTodas();

        if (!_configuracao.NotificacoesAtivas)
            return;

        var pendentes = await _transacaoRepository.GetDespesasPendentesParaAvisoAsync(DateTime.Today.AddDays(JanelaFuturaDias + 3));
        foreach (var transacao in pendentes)
            await AgendarAsync(transacao, permitirImediato: false);
    }

    private async Task AgendarAsync(Transacao transacao, bool permitirImediato)
    {
        try
        {
            if (!_configuracao.NotificacoesAtivas || transacao.Tipo != TipoTransacao.Despesa || transacao.Pago)
                return;

            var avisos = CalcularAvisos(transacao.Data.Date, _configuracao.PrazosAviso, permitirImediato).ToList();
            if (avisos.Count == 0)
                return;

            if (!await LocalNotificationCenter.Current.AreNotificationsEnabled())
                await LocalNotificationCenter.Current.RequestNotificationPermission();

            var somAtivo = _configuracao.SomAtivo;
            foreach (var (slot, horario, titulo) in avisos)
            {
                var request = new NotificationRequest
                {
                    NotificationId = transacao.Id * SlotsPorTransacao + slot,
                    Title = titulo,
                    Description = $"{transacao.Descricao} — {transacao.Valor:C} (vence {transacao.Data:dd/MM/yyyy})",
                    Silent = !somAtivo,
                    Schedule = new NotificationRequestSchedule { NotifyTime = horario },
                    Android = new AndroidOptions { ChannelId = somAtivo ? CanalComSom : CanalSilencioso }
                };

                await LocalNotificationCenter.Current.Show(request);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao agendar lembretes para o lançamento {Id}", transacao.Id);
        }
    }

    /// <summary>
    /// Gera (slot, horário, título) para cada prazo marcado, sempre às 09:00. Horários já passados são
    /// descartados; se nenhum restar e a despesa já estiver no dia/vencida, <paramref name="permitirImediato"/>
    /// dispara um aviso em 1 minuto (útil logo após criar/editar o lançamento).
    /// </summary>
    private static IEnumerable<(int Slot, DateTime Horario, string Titulo)> CalcularAvisos(DateTime vencimento, PrazoAviso prazos, bool permitirImediato)
    {
        var candidatos = new List<(int Slot, DateTime Dia, string Titulo)>();

        if (prazos.HasFlag(PrazoAviso.TresDiasAntes))
            candidatos.Add((0, vencimento.AddDays(-3), "Despesa vence em 3 dias"));
        if (prazos.HasFlag(PrazoAviso.UmDiaAntes))
            candidatos.Add((1, vencimento.AddDays(-1), "Despesa vence amanhã"));
        if (prazos.HasFlag(PrazoAviso.NoDia))
            candidatos.Add((2, vencimento, "Despesa vence hoje"));
        if (prazos.HasFlag(PrazoAviso.UmDiaDepois))
            candidatos.Add((3, vencimento.AddDays(1), "Despesa venceu ontem"));
        if (prazos.HasFlag(PrazoAviso.Vencidas))
        {
            for (var dia = 2; dia <= DiasMaximoVencida; dia++)
                candidatos.Add((2 + dia, vencimento.AddDays(dia), $"Despesa vencida há {dia} dias"));
        }

        var agora = DateTime.Now;
        var limiteFuturo = DateTime.Today.AddDays(JanelaFuturaDias);

        var futuros = candidatos
            .Select(c => (c.Slot, Horario: c.Dia.AddHours(HoraAviso), c.Titulo))
            .Where(c => c.Horario > agora && c.Horario.Date <= limiteFuturo)
            .ToList();

        var precisaAvisoImediato = permitirImediato
            && vencimento <= DateTime.Today
            && vencimento >= DateTime.Today.AddDays(-DiasMaximoVencida)
            && futuros.All(f => f.Horario.Date != DateTime.Today)
            && (prazos.HasFlag(PrazoAviso.NoDia) || prazos.HasFlag(PrazoAviso.Vencidas) || prazos.HasFlag(PrazoAviso.UmDiaDepois));

        if (precisaAvisoImediato)
        {
            var titulo = vencimento == DateTime.Today ? "Despesa vence hoje" : "Despesa vencida";
            var slotLivre = Enumerable.Range(0, SlotsPorTransacao).First(s => futuros.All(f => f.Slot != s) && candidatos.All(c => c.Slot != s || c.Dia.AddHours(HoraAviso) <= agora));
            futuros.Add((slotLivre, agora.AddMinutes(1), titulo));
        }

        return futuros;
    }
}
