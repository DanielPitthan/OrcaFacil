using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Armazenamento;

/// <summary>
/// Semear dados de demonstração só vale para um banco realmente vazio.
/// Schema íntegro sem perfil, mas com lançamento já gravado, fica como está:
/// o espelho não é restaurado por cima e o único acréscimo é um perfil vazio.
/// </summary>
public static class PoliticaSemeadura
{
    public const string MensagemPerfilMinimo =
        "SemPerfil com lançamentos reais. O banco vivo foi mantido, o espelho não foi restaurado e só um perfil mínimo foi criado. Nenhum dado de demonstração foi semeado.";

    public const string NomePerfilMinimo = "Usuário";

    /// <summary>
    /// Devolve true quando a semeadura de demonstração não deve rodar.
    /// </summary>
    public static async Task<bool> CriarPerfilMinimoSeHaLancamentoAsync(
        AppDbContext contexto,
        IRegistroLog? log,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        if (!await contexto.Transacoes.AnyAsync(cancellationToken))
            return false;

        log?.Informacao(MensagemPerfilMinimo, []);

        if (await contexto.MembrosFamilia.AnyAsync(cancellationToken))
            return true;

        contexto.MembrosFamilia.Add(new MembroFamilia
        {
            Nome = NomePerfilMinimo,
            Avatar = "👤",
            Ativo = true,
            CriadoEm = DateTime.UtcNow
        });
        await contexto.SaveChangesAsync(cancellationToken);
        return true;
    }
}
