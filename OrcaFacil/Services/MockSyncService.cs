using Microsoft.Extensions.Logging;
using OrcaFacil.Core.Services;

namespace OrcaFacil.Services;

/// <summary>
/// Implementação mock de ISyncService: simula uma sincronização em nuvem (log + delay),
/// sem qualquer chamada de rede real. Serve como placeholder até uma integração real
/// (Firebase/Azure) ser conectada — bastará trocar o registro no DI (MauiProgram.cs).
/// </summary>
public class MockSyncService : ISyncService
{
    private readonly ILogger<MockSyncService> _logger;
    private DateTime? _ultimaSincronizacao;

    public MockSyncService(ILogger<MockSyncService> logger)
    {
        _logger = logger;
    }

    public async Task<SyncResult> SyncUpAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Simulando envio de dados para a nuvem...");
        await Task.Delay(Random.Shared.Next(500, 1500), ct);
        _ultimaSincronizacao = DateTime.UtcNow;
        return new SyncResult(true, "Sincronização (envio) simulada concluída", 0);
    }

    public async Task<SyncResult> SyncDownAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Simulando recebimento de dados da nuvem...");
        await Task.Delay(Random.Shared.Next(500, 1500), ct);
        _ultimaSincronizacao = DateTime.UtcNow;
        return new SyncResult(true, "Sincronização (recebimento) simulada concluída", 0);
    }

    public Task<DateTime?> GetLastSyncAsync() => Task.FromResult(_ultimaSincronizacao);
}
