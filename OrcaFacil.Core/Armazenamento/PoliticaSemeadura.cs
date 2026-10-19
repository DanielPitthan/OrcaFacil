using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Armazenamento;

/// <summary>O que o banco vivo contém, para a abertura e para o backup.</summary>
public enum SituacaoConteudoBanco
{
    /// <summary>Vazio, ou só com categoria padrão do sistema. A abertura semeia.</summary>
    Vazio,

    /// <summary>Só a demonstração semeada e/ou o perfil mínimo "Usuário". A abertura não semeia por cima.</summary>
    SoDemonstracao,

    /// <summary>Qualquer outro dado, inclusive log. Sem perfil, a abertura cria só o "Usuário".</summary>
    DadoReal
}

/// <summary>
/// Uma função e uma lista de tabelas. A abertura semeia apenas em
/// <see cref="SituacaoConteudoBanco.Vazio"/> e nunca semeia por cima. Sem perfil e com
/// dado real, cria só o perfil mínimo. O backup, quando já existe espelho, recusa em
/// vazio e em <see cref="SituacaoConteudoBanco.SoDemonstracao"/> e pede confirmação em
/// <see cref="SituacaoConteudoBanco.DadoReal"/>.
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
    /// A abertura semeia só quando a classificação é <see cref="SituacaoConteudoBanco.Vazio"/>.
    /// </summary>
    public static async Task<bool> HaDadoDoUsuarioAsync(AppDbContext contexto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        return await ClassificarConteudoAsync(contexto, cancellationToken) != SituacaoConteudoBanco.Vazio;
    }

    /// <summary>
    /// Categoria padrão do sistema não conta, mesmo com os outros campos diferentes.
    /// Lançamento, orçamento e meta no formato do catálogo, até o limite semeado, e o
    /// perfil mínimo ficam em <see cref="SituacaoConteudoBanco.SoDemonstracao"/>.
    /// Qualquer outra linha é <see cref="SituacaoConteudoBanco.DadoReal"/>.
    /// </summary>
    public static async Task<SituacaoConteudoBanco> ClassificarConteudoAsync(
        AppDbContext contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (await contexto.Categorias.AnyAsync(categoria => !categoria.EhPadraoDoSistema, cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.ItensLancamento.AnyAsync(cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.FechamentosPeriodo.AnyAsync(cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.LogsEvento.AnyAsync(cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.AtivosFinanceiros.AnyAsync(cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.OperacoesAtivo.AnyAsync(cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.ProdutosCesta.AnyAsync(cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.LocaisCompra.AnyAsync(cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.ComprasCesta.AnyAsync(cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.ItensCompraCesta.AnyAsync(cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.Cartoes.AnyAsync(cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.RecargasCartao.AnyAsync(cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        var membros = await contexto.MembrosFamilia.AsNoTracking().ToListAsync(cancellationToken);
        if (!PerfisSoMinimos(membros))
            return SituacaoConteudoBanco.DadoReal;

        var nomesCategoria = await contexto.Categorias.AsNoTracking()
            .ToDictionaryAsync(categoria => categoria.Id, categoria => categoria.Nome, cancellationToken);

        var transacoes = await contexto.Transacoes.AsNoTracking().ToListAsync(cancellationToken);
        if (!LancamentosSoDemonstracao(transacoes, nomesCategoria))
            return SituacaoConteudoBanco.DadoReal;

        var orcamentos = await contexto.Orcamentos.AsNoTracking().ToListAsync(cancellationToken);
        if (!OrcamentosSoDemonstracao(orcamentos, nomesCategoria))
            return SituacaoConteudoBanco.DadoReal;

        var metas = await contexto.Metas.AsNoTracking().ToListAsync(cancellationToken);
        if (!MetasSoDemonstracao(metas))
            return SituacaoConteudoBanco.DadoReal;

        if (membros.Count == 0 && transacoes.Count == 0 && orcamentos.Count == 0 && metas.Count == 0)
            return SituacaoConteudoBanco.Vazio;

        return SituacaoConteudoBanco.SoDemonstracao;
    }

    private static bool PerfisSoMinimos(IReadOnlyList<MembroFamilia> membros)
    {
        if (membros.Count > 1)
            return false;

        return membros.Count == 0 || EhPerfilMinimo(membros[0]);
    }

    private static bool EhPerfilMinimo(MembroFamilia membro)
        => membro.Nome == NomePerfilMinimo
            && membro.Avatar == CatalogoDemonstracao.AvatarPerfilPadrao
            && membro.Ativo
            && string.IsNullOrWhiteSpace(membro.Apelido)
            && string.IsNullOrWhiteSpace(membro.FotoPath)
            && membro.Role == PerfilRole.Admin;

    private static bool LancamentosSoDemonstracao(
        IReadOnlyList<Transacao> transacoes,
        IReadOnlyDictionary<int, string> nomesCategoria)
    {
        var contagem = new int[CatalogoDemonstracao.Lancamentos.Count];
        foreach (var transacao in transacoes)
        {
            if (!string.IsNullOrWhiteSpace(transacao.Observacao) || !transacao.Pago)
                return false;

            if (transacao.CartaoId is not null || transacao.TransacaoOrigemId is not null)
                return false;

            var indice = IndiceLancamento(transacao, nomesCategoria);
            if (indice < 0)
                return false;

            contagem[indice]++;
            if (contagem[indice] > CatalogoDemonstracao.RepeticoesMensais)
                return false;
        }

        return true;
    }

    private static int IndiceLancamento(Transacao transacao, IReadOnlyDictionary<int, string> nomesCategoria)
    {
        if (!nomesCategoria.TryGetValue(transacao.CategoriaId, out var nome) || string.IsNullOrEmpty(nome))
            return -1;

        var lista = CatalogoDemonstracao.Lancamentos;
        for (var i = 0; i < lista.Count; i++)
        {
            var item = lista[i];
            if (item.Descricao == transacao.Descricao
                && item.Valor == transacao.Valor
                && item.Tipo == transacao.Tipo
                && item.Recorrencia == transacao.Recorrencia
                && item.Categoria == nome)
                return i;
        }

        return -1;
    }

    private static bool OrcamentosSoDemonstracao(
        IReadOnlyList<Orcamento> orcamentos,
        IReadOnlyDictionary<int, string> nomesCategoria)
    {
        var contagem = new int[CatalogoDemonstracao.Orcamentos.Count];
        foreach (var orcamento in orcamentos)
        {
            if (orcamento.MetaReducaoPercentual is not null)
                return false;

            if (!nomesCategoria.TryGetValue(orcamento.CategoriaId, out var nome) || string.IsNullOrEmpty(nome))
                return false;

            var indice = -1;
            var lista = CatalogoDemonstracao.Orcamentos;
            for (var i = 0; i < lista.Count; i++)
            {
                if (lista[i].Categoria == nome && lista[i].ValorLimite == orcamento.ValorLimite)
                {
                    indice = i;
                    break;
                }
            }

            if (indice < 0)
                return false;

            contagem[indice]++;
            if (contagem[indice] > 1)
                return false;
        }

        return true;
    }

    private static bool MetasSoDemonstracao(IReadOnlyList<Meta> metas)
    {
        var contagem = new int[CatalogoDemonstracao.Metas.Count];
        foreach (var meta in metas)
        {
            if (meta.Concluida)
                return false;

            var indice = -1;
            var lista = CatalogoDemonstracao.Metas;
            for (var i = 0; i < lista.Count; i++)
            {
                var item = lista[i];
                if (item.Nome == meta.Nome
                    && item.ValorAlvo == meta.ValorAlvo
                    && item.ValorAtual == meta.ValorAtual
                    && item.Icone == meta.Icone)
                {
                    indice = i;
                    break;
                }
            }

            if (indice < 0)
                return false;

            contagem[indice]++;
            if (contagem[indice] > 1)
                return false;
        }

        return true;
    }
}
