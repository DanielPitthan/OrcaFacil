using OrcaFacil.Core.Armazenamento.Partida;
using OrcaFacil.Core.Data;

namespace OrcaFacil.Core.Armazenamento;

/// <summary>
/// Classe registrada na abertura. A demonstração é o callback do app
/// (<c>SeedData.PopularAsync</c>). Este tipo decide se ela roda.
/// </summary>
public sealed class SemeaduraDadosApp : ISemeaduraDados
{
    private readonly CaminhoBancoVivo _caminho;
    private readonly IRegistroLog _log;
    private readonly Func<AppDbContext, CancellationToken, Task> _demonstracao;

    public SemeaduraDadosApp(
        CaminhoBancoVivo caminho,
        IRegistroLog log,
        Func<AppDbContext, CancellationToken, Task> demonstracao)
    {
        _caminho = caminho ?? throw new ArgumentNullException(nameof(caminho));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _demonstracao = demonstracao ?? throw new ArgumentNullException(nameof(demonstracao));
    }

    public async Task SemearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var contexto = ConexaoBancoArquivo.AbrirEscrita(_caminho.Valor);
        if (await PoliticaSemeadura.CriarPerfilMinimoSeHaDadoDoUsuarioAsync(contexto, _log, cancellationToken))
            return;

        await _demonstracao(contexto, cancellationToken);
    }
}
