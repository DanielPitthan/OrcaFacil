using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Armazenamento;

namespace OrcaFacil.Core.Armazenamento.Partida;

/// <summary>
/// Há membro no schema já íntegro. Só leitura: não cria arquivo e não semeia.
/// </summary>
public sealed class PresencaPerfilArquivo : IPresencaPerfil
{
    private readonly CaminhoBancoVivo _caminho;

    public PresencaPerfilArquivo(CaminhoBancoVivo caminho)
    {
        _caminho = caminho ?? throw new ArgumentNullException(nameof(caminho));
    }

    public async Task<bool> HaMembroAsync(CancellationToken cancellationToken = default)
    {
        await using var contexto = ConexaoBancoArquivo.AbrirLeitura(_caminho.Valor);
        return await contexto.MembrosFamilia.AnyAsync(cancellationToken);
    }
}
