using OrcaFacil.Core.Llm;

namespace OrcaFacil.Core.Llm.Avaliacao;

/// <summary>
/// Abstração fina sobre a leitura de um comprovante.
/// O CI usa só fake. Não carrega chave, URL nem a imagem no <see cref="object.ToString"/>.
/// </summary>
public interface ILeitorComprovante
{
    Task<ResultadoMensagem> LerAsync(FotoComprovantePronta foto, CancellationToken ct = default);
}
