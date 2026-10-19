using OrcaFacil.Core.Armazenamento;

namespace OrcaFacil.Core.Armazenamento.Partida;

/// <summary>
/// A abertura não apaga arquivo. A limpeza total continua no pedido explícito da tela de configurações.
/// </summary>
public sealed class LimpezaTotalForaDaAbertura : ILimpezaTotalDados
{
    public Task RemoverBancoVivoAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException(
            "A abertura não apaga o banco. A limpeza total continua no pedido explícito da tela de configurações.");
    }

    public Task RemoverEspelhoAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException(
            "A abertura não apaga o espelho. A limpeza total continua no pedido explícito da tela de configurações.");
    }
}
