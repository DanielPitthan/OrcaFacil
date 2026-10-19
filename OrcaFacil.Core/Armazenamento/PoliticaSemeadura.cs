using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Armazenamento;

/// <summary>
/// Semear a demonstração só vale para um banco sem dado do usuário.
/// Categoria padrão do sistema não conta. Qualquer outra linha — membro, categoria
/// criada pela pessoa, lançamento, cartão, orçamento, meta, cesta, ativo, recarga,
/// fechamento, log — mantém o banco, não restaura o espelho e só cria o perfil vazio.
/// </summary>
public static class PoliticaSemeadura
{
    public const string MensagemPerfilMinimo =
        "SemPerfil com dados do usuário. O banco vivo foi mantido, o espelho não foi restaurado e só um perfil mínimo foi criado. Nenhum dado de demonstração foi semeado.";

    public const string NomePerfilMinimo = "Usuário";

    /// <summary>Cada <see cref="DbSet{TEntity}"/> de <see cref="AppDbContext"/> entra nesta lista.</summary>
    public static readonly string[] ConjuntosObservados =
    [
        nameof(AppDbContext.MembrosFamilia),
        nameof(AppDbContext.Categorias),
        nameof(AppDbContext.Transacoes),
        nameof(AppDbContext.ItensLancamento),
        nameof(AppDbContext.Orcamentos),
        nameof(AppDbContext.Metas),
        nameof(AppDbContext.FechamentosPeriodo),
        nameof(AppDbContext.LogsEvento),
        nameof(AppDbContext.AtivosFinanceiros),
        nameof(AppDbContext.OperacoesAtivo),
        nameof(AppDbContext.ProdutosCesta),
        nameof(AppDbContext.LocaisCompra),
        nameof(AppDbContext.ComprasCesta),
        nameof(AppDbContext.ItensCompraCesta),
        nameof(AppDbContext.Cartoes),
        nameof(AppDbContext.RecargasCartao)
    ];

    /// <summary>
    /// Devolve true quando a semeadura de demonstração não deve rodar.
    /// </summary>
    public static async Task<bool> CriarPerfilMinimoSeHaDadoDoUsuarioAsync(
        AppDbContext contexto,
        IRegistroLog? log,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        if (!await HaDadoDoUsuarioAsync(contexto, cancellationToken))
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

    /// <summary>
    /// Critério único de dado do usuário. A abertura e o backup chamam este método.
    /// Categoria padrão do sistema não conta. Qualquer linha de log conta.
    /// </summary>
    public static async Task<bool> HaDadoDoUsuarioAsync(AppDbContext contexto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (await contexto.MembrosFamilia.AnyAsync(cancellationToken))
            return true;

        if (await contexto.Categorias.AnyAsync(categoria => !categoria.EhPadraoDoSistema, cancellationToken))
            return true;

        if (await contexto.Transacoes.AnyAsync(cancellationToken))
            return true;

        if (await contexto.ItensLancamento.AnyAsync(cancellationToken))
            return true;

        if (await contexto.Orcamentos.AnyAsync(cancellationToken))
            return true;

        if (await contexto.Metas.AnyAsync(cancellationToken))
            return true;

        if (await contexto.FechamentosPeriodo.AnyAsync(cancellationToken))
            return true;

        if (await contexto.LogsEvento.AnyAsync(cancellationToken))
            return true;

        if (await contexto.AtivosFinanceiros.AnyAsync(cancellationToken))
            return true;

        if (await contexto.OperacoesAtivo.AnyAsync(cancellationToken))
            return true;

        if (await contexto.ProdutosCesta.AnyAsync(cancellationToken))
            return true;

        if (await contexto.LocaisCompra.AnyAsync(cancellationToken))
            return true;

        if (await contexto.ComprasCesta.AnyAsync(cancellationToken))
            return true;

        if (await contexto.ItensCompraCesta.AnyAsync(cancellationToken))
            return true;

        if (await contexto.Cartoes.AnyAsync(cancellationToken))
            return true;

        return await contexto.RecargasCartao.AnyAsync(cancellationToken);
    }
}
