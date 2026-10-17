using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Lancamentos;

namespace OrcaFacil.Core.Services;

/// <summary>
/// Lança, edita ou exclui um estorno avulso e ajusta o total líquido na mesma gravação.
/// O sheet da fatura chama estes métodos quando a fatura já existe; o item de compra continua separado.
/// Excluir um estorno devolve a magnitude ao total.
/// </summary>
public class EstornoFaturaService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public EstornoFaturaService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<ItemLancamento> LancarAsync(
        int faturaId,
        decimal valor,
        string descricao,
        int categoriaId,
        string? descritivo = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transacaoEf = await context.Database.BeginTransactionAsync(cancellationToken);

        var fatura = await CarregarAsync(context, faturaId, cancellationToken);
        await GarantirMesAbertoAsync(context, fatura, cancellationToken);
        await GarantirCategoriaAsync(context, categoriaId, cancellationToken);

        var novoTotal = fatura.Valor - valor;
        var ordem = fatura.Itens.Count == 0 ? 0 : fatura.Itens.Max(i => i.Ordem) + 1;
        var item = new ItemLancamento
        {
            Descricao = descricao?.Trim() ?? string.Empty,
            Descritivo = ItensFaturaInclusao.NormalizarDescritivo(descritivo),
            Valor = valor,
            EhEstorno = true,
            Natureza = NaturezaItemLancamento.Avulsa,
            CategoriaId = categoriaId,
            Ordem = ordem
        };

        var itens = fatura.Itens.Append(item).ToList();
        ItensFaturaRegras.Validar(
            novoTotal,
            fatura.CategoriaId,
            fatura.Categoria?.PermiteItensFatura == true,
            fatura.Tipo,
            itens);

        fatura.Valor = novoTotal;
        fatura.AtualizadoEm = DateTime.UtcNow;
        fatura.Itens.Add(item);

        await context.SaveChangesAsync(cancellationToken);
        await transacaoEf.CommitAsync(cancellationToken);
        return item;
    }

    public async Task<ItemLancamento> EditarAsync(
        int faturaId,
        int itemId,
        decimal valor,
        string descricao,
        int categoriaId,
        string? descritivo = null,
        CancellationToken cancellationToken = default)
    {
        var textoDescritivo = ItensFaturaInclusao.NormalizarDescritivo(descritivo);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transacaoEf = await context.Database.BeginTransactionAsync(cancellationToken);

        var fatura = await CarregarAsync(context, faturaId, cancellationToken);
        await GarantirMesAbertoAsync(context, fatura, cancellationToken);
        await GarantirCategoriaAsync(context, categoriaId, cancellationToken);

        var item = fatura.Itens.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException("Item não encontrado.");
        if (!item.EhEstorno)
            throw new InvalidOperationException("Só um estorno pode ser editado por este caminho.");

        var novoTotal = fatura.Valor + item.Valor - valor;
        item.Descricao = descricao?.Trim() ?? string.Empty;
        item.Descritivo = textoDescritivo;
        item.Valor = valor;
        item.CategoriaId = categoriaId;
        item.Natureza = NaturezaItemLancamento.Avulsa;
        item.ParcelaAtual = null;
        item.TotalParcelas = null;

        ItensFaturaRegras.Validar(
            novoTotal,
            fatura.CategoriaId,
            fatura.Categoria?.PermiteItensFatura == true,
            fatura.Tipo,
            fatura.Itens.ToList());

        fatura.Valor = novoTotal;
        fatura.AtualizadoEm = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        await transacaoEf.CommitAsync(cancellationToken);
        return item;
    }

    /// <summary>Remove o estorno e soma a magnitude de volta ao total da fatura.</summary>
    public async Task ExcluirAsync(int faturaId, int itemId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transacaoEf = await context.Database.BeginTransactionAsync(cancellationToken);

        var fatura = await CarregarAsync(context, faturaId, cancellationToken);
        await GarantirMesAbertoAsync(context, fatura, cancellationToken);

        var item = fatura.Itens.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException("Item não encontrado.");
        if (!item.EhEstorno)
            throw new InvalidOperationException("Só um estorno altera o total da fatura ao ser excluído.");

        fatura.Valor += item.Valor;
        fatura.Itens.Remove(item);
        context.ItensLancamento.Remove(item);
        fatura.AtualizadoEm = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        await transacaoEf.CommitAsync(cancellationToken);
    }

    private static async Task<Transacao> CarregarAsync(
        AppDbContext context,
        int faturaId,
        CancellationToken cancellationToken)
        => await context.Transacoes
            .Include(t => t.Categoria)
            .Include(t => t.Itens)
            .FirstOrDefaultAsync(t => t.Id == faturaId, cancellationToken)
            ?? throw new InvalidOperationException("Lançamento não encontrado.");

    private static async Task GarantirMesAbertoAsync(
        AppDbContext context,
        Transacao fatura,
        CancellationToken cancellationToken)
    {
        var mesFechado = await context.FechamentosPeriodo.AnyAsync(
            f => f.Mes == fatura.Data.Month && f.Ano == fatura.Data.Year && f.Fechado,
            cancellationToken);
        if (mesFechado)
            throw new InvalidOperationException(ItensFaturaRegras.MensagemMesFechado(fatura.Data));
    }

    private static async Task GarantirCategoriaAsync(
        AppDbContext context,
        int categoriaId,
        CancellationToken cancellationToken)
    {
        if (!await context.Categorias.AnyAsync(c => c.Id == categoriaId, cancellationToken))
            throw new InvalidOperationException("Categoria do item inválida.");
    }
}
