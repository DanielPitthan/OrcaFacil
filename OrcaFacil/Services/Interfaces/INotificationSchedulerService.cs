using OrcaFacil.Core.Entities;

namespace OrcaFacil.Services.Interfaces;

public interface INotificationSchedulerService
{
    Task AgendarLembreteAsync(Transacao transacao);
    Task CancelarLembreteAsync(int transacaoId);
    Task ReagendarTodasAsync();
    void CancelarTodas();
}
