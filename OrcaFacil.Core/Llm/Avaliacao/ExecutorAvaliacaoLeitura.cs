using OrcaFacil.Core.Llm;

namespace OrcaFacil.Core.Llm.Avaliacao;

/// <summary>
/// Roda os casos num <see cref="ILeitorComprovante"/>. Não abre rede, não lê chave e não grava foto.
/// </summary>
public sealed class ExecutorAvaliacaoLeitura(ILeitorComprovante leitor)
{
    private readonly ILeitorComprovante _leitor = leitor ?? throw new ArgumentNullException(nameof(leitor));

    public async Task<ResultadoAvaliacaoLeitura> ExecutarAsync(
        IReadOnlyList<(FotoComprovantePronta Foto, GabaritoComprovante Gabarito)> casos,
        MetasLeituraComprovante metas,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(casos);
        ArgumentNullException.ThrowIfNull(metas);

        var avaliacoes = new List<AvaliacaoComprovante>(casos.Count);
        foreach (var (foto, gabarito) in casos)
        {
            ct.ThrowIfCancellationRequested();
            var resultado = await _leitor.LerAsync(foto, ct);
            avaliacoes.Add(AvaliadorLeituraComprovante.Avaliar(gabarito, resultado));
        }

        return AvaliadorLeituraComprovante.Consolidar(avaliacoes, metas);
    }
}
