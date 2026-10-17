using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Lancamentos;

namespace OrcaFacil.Core.Services;

/// <summary>
/// Lança um estorno avulso na fatura e baixa o total líquido na mesma gravação.
/// O modal da fatura (próxima entrega) chama este serviço; o item de compra continua separado.
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
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transacaoEf = await context.Database.BeginTransactionAsync(cancellationToken);

        var fatura = await context.Transacoes
            .Include(t => t.Categoria)
            .Include(t => t.Itens)
            .FirstOrDefaultAsync(t => t.Id == faturaId, cancellationToken)
            ?? throw new InvalidOperationException("Lançamento não encontrado.");

        var mesFechado = await context.FechamentosPeriodo.AnyAsync(
            f => f.Mes == fatura.Data.Month && f.Ano == fatura.Data.Year && f.Fechado,
            cancellationToken);
        if (mesFechado)
            throw new InvalidOperationException(ItensFaturaRegras.MensagemMesFechado(fatura.Data));

        if (!await context.Categorias.AnyAsync(c => c.Id == categoriaId, cancellationToken))
            throw new InvalidOperationException("Categoria do item inválida.");

        var novoTotal = fatura.Valor - valor;
        var ordem = fatura.Itens.Count == 0 ? 0 : fatura.Itens.Max(i => i.Ordem) + 1;
        var item = new ItemLancamento
        {
            Descricao = descricao?.Trim() ?? string.Empty,
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
}
