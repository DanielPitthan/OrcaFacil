using OrcaFacil.Core.Armazenamento;
using OrcaFacil.Core.Data;

namespace OrcaFacil.Core.Armazenamento.Partida;

/// <summary>
/// Lê o banco vivo com <see cref="VerificadorIntegridadeBanco"/>. Não cria o arquivo e não migra.
/// Pasta no lugar do arquivo, ou consulta que falha, conta como presente: não pode parecer instalação nova.
/// </summary>
public sealed class SondaBancoArquivo : ISondaBancoVivo
{
    private readonly CaminhoBancoVivo _caminho;

    public SondaBancoArquivo(CaminhoBancoVivo caminho)
    {
        _caminho = caminho ?? throw new ArgumentNullException(nameof(caminho));
    }

    public bool Existe()
    {
        try
        {
            if (Directory.Exists(_caminho.Valor))
                return true;

            return File.Exists(_caminho.Valor);
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    public async Task<RelatorioIntegridadeBanco> VerificarAsync(CancellationToken cancellationToken = default)
    {
        await using var contexto = ConexaoBancoArquivo.AbrirLeitura(_caminho.Valor);
        return await VerificadorIntegridadeBanco.VerificarAsync(contexto, cancellationToken);
    }
}
