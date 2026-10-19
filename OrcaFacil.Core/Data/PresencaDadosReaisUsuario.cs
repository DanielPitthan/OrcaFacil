using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Data;

/// <summary>O que a leitura do banco vivo conseguiu afirmar.</summary>
public enum LeituraDadosReais
{
    /// <summary>Vazio, ou só categoria padrão, perfil "Usuário" e demonstração.</summary>
    SemDadosReais,

    /// <summary>Há registro que o usuário criou ou alterou.</summary>
    ComDadosReais,

    /// <summary>O arquivo não pôde ser lido. Não trate como vazio.</summary>
    Indeterminado
}

/// <summary>
/// Decide se o banco vivo ainda é o que o app grava na instalação.
/// Fica isolado de <see cref="OrcaFacil.Core.Armazenamento.SituacaoBancoVivo.SemPerfil"/>:
/// o coordenador, no PR da inicialização, trata schema íntegro sem membro.
/// Quando os dois estiverem no mesmo branch, a regra de "ainda é a instalação" deve ser uma só.
/// Log de evento não conta: o app o grava sozinho.
/// </summary>
public static class PresencaDadosReaisUsuario
{
    public static async Task<LeituraDadosReais> ClassificarAsync(
        string caminhoBanco,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(caminhoBanco) || !File.Exists(caminhoBanco))
            return LeituraDadosReais.SemDadosReais;

        try
        {
            var opcoes = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={caminhoBanco};Mode=ReadWrite;Pooling=False")
                .Options;
            await using var ctx = new AppDbContext(opcoes);
            return await ClassificarAsync(ctx, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return LeituraDadosReais.Indeterminado;
        }
    }

    public static async Task<LeituraDadosReais> ClassificarAsync(
        AppDbContext ctx,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (await ctx.Cartoes.AnyAsync(cancellationToken)
            || await ctx.RecargasCartao.AnyAsync(cancellationToken)
            || await ctx.AtivosFinanceiros.AnyAsync(cancellationToken)
            || await ctx.OperacoesAtivo.AnyAsync(cancellationToken)
            || await ctx.ProdutosCesta.AnyAsync(cancellationToken)
            || await ctx.LocaisCompra.AnyAsync(cancellationToken)
            || await ctx.ComprasCesta.AnyAsync(cancellationToken)
            || await ctx.ItensCompraCesta.AnyAsync(cancellationToken)
            || await ctx.FechamentosPeriodo.AnyAsync(cancellationToken)
            || await ctx.ItensLancamento.AnyAsync(cancellationToken))
            return LeituraDadosReais.ComDadosReais;

        var membros = await ctx.MembrosFamilia.AsNoTracking().ToListAsync(cancellationToken);
        if (membros.Count > 1 || (membros.Count == 1 && !CatalogoDemonstracao.EhPerfilPadrao(membros[0])))
            return LeituraDadosReais.ComDadosReais;

        var categorias = await ctx.Categorias.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var categoria in categorias)
        {
            if (!CatalogoDemonstracao.EhCategoriaPadrao(categoria))
                return LeituraDadosReais.ComDadosReais;
        }

        if (await LancamentoRealAsync(ctx, cancellationToken)
            || await OrcamentoRealAsync(ctx, cancellationToken)
            || await MetaRealAsync(ctx, cancellationToken))
            return LeituraDadosReais.ComDadosReais;

        return LeituraDadosReais.SemDadosReais;
    }

    private static async Task<bool> LancamentoRealAsync(AppDbContext ctx, CancellationToken cancellationToken)
    {
        var linhas = await ctx.Transacoes.AsNoTracking()
            .Select(t => new LinhaLancamento(
                t.Descricao,
                t.Valor,
                t.Tipo,
                t.Recorrencia,
                t.Categoria != null ? t.Categoria.Nome : null,
                t.Observacao,
                t.Pago,
                t.CartaoId,
                t.TransacaoOrigemId))
            .ToListAsync(cancellationToken);

        var contagem = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var linha in linhas)
        {
            if (!string.IsNullOrEmpty(linha.Observacao)
                || !linha.Pago
                || linha.CartaoId is not null
                || linha.TransacaoOrigemId is not null
                || linha.Categoria is null
                || !CatalogoDemonstracao.TentarLimiteLancamento(
                    linha.Descricao,
                    linha.Valor,
                    linha.Tipo,
                    linha.Recorrencia,
                    linha.Categoria,
                    out var limite))
                return true;

            var chave = $"{linha.Descricao}\u001f{linha.Valor}\u001f{linha.Tipo}\u001f{linha.Recorrencia}\u001f{linha.Categoria}";
            contagem.TryGetValue(chave, out var quantidade);
            quantidade++;
            if (quantidade > limite)
                return true;

            contagem[chave] = quantidade;
        }

        return false;
    }

    private static async Task<bool> OrcamentoRealAsync(AppDbContext ctx, CancellationToken cancellationToken)
    {
        var linhas = await ctx.Orcamentos.AsNoTracking()
            .Select(o => new LinhaOrcamento(
                o.Categoria != null ? o.Categoria.Nome : null,
                o.ValorLimite,
                o.MetaReducaoPercentual))
            .ToListAsync(cancellationToken);

        var contagem = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var linha in linhas)
        {
            if (linha.Categoria is null
                || linha.MetaReducao is not null
                || !CatalogoDemonstracao.TentarLimiteOrcamento(linha.Categoria, linha.ValorLimite, out var limite))
                return true;

            var chave = $"{linha.Categoria}\u001f{linha.ValorLimite}";
            contagem.TryGetValue(chave, out var quantidade);
            quantidade++;
            if (quantidade > limite)
                return true;

            contagem[chave] = quantidade;
        }

        return false;
    }

    private static async Task<bool> MetaRealAsync(AppDbContext ctx, CancellationToken cancellationToken)
    {
        var linhas = await ctx.Metas.AsNoTracking()
            .Select(m => new LinhaMeta(m.Nome, m.ValorAlvo, m.ValorAtual, m.Icone, m.Concluida))
            .ToListAsync(cancellationToken);

        var contagem = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var linha in linhas)
        {
            if (linha.Concluida
                || !CatalogoDemonstracao.TentarLimiteMeta(
                    linha.Nome,
                    linha.ValorAlvo,
                    linha.ValorAtual,
                    linha.Icone,
                    out var limite))
                return true;

            var chave = $"{linha.Nome}\u001f{linha.ValorAlvo}\u001f{linha.ValorAtual}\u001f{linha.Icone}";
            contagem.TryGetValue(chave, out var quantidade);
            quantidade++;
            if (quantidade > limite)
                return true;

            contagem[chave] = quantidade;
        }

        return false;
    }

    private readonly record struct LinhaLancamento(
        string Descricao,
        decimal Valor,
        TipoTransacao Tipo,
        TipoRecorrencia Recorrencia,
        string? Categoria,
        string? Observacao,
        bool Pago,
        int? CartaoId,
        int? TransacaoOrigemId);

    private readonly record struct LinhaOrcamento(string? Categoria, decimal ValorLimite, decimal? MetaReducao);

    private readonly record struct LinhaMeta(
        string Nome,
        decimal ValorAlvo,
        decimal ValorAtual,
        string Icone,
        bool Concluida);
}
