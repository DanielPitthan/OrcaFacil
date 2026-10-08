namespace OrcaFacil.Core.Services;

public record SyncResult(bool Success, string Message, int ItemsSynced);

/// <summary>
/// Abstração para sincronização em nuvem. Nesta versão só existe uma implementação mock
/// (log + delay simulado); uma implementação real (Firebase/Azure) pode substituí-la via DI
/// sem exigir mudanças no restante do app.
/// </summary>
public interface ISyncService
{
    Task<SyncResult> SyncUpAsync(CancellationToken ct = default);
    Task<SyncResult> SyncDownAsync(CancellationToken ct = default);
    Task<DateTime?> GetLastSyncAsync();
}
