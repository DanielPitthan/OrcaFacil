using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Cesta;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Repositories;

public class CartaoRepository : ICartaoRepository
{
    public const string NomeCategoriaCartaoCredito = "Cartão de Crédito";

    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public CartaoRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<Cartao>> ListarAsync(bool somenteAtivos = false, TipoCartao? tipo = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Cartoes.AsQueryable();
        if (somenteAtivos)
            query = query.Where(c => c.Ativo);
        if (tipo.HasValue)
            query = query.Where(c => c.Tipo == tipo.Value);

        return await query.OrderBy(c => c.Nome).ToListAsync();
    }

    public async Task<Cartao?> ObterAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Cartoes.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<bool> ExisteNomeAsync(string nome, int? ignorarId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Cartoes.AnyAsync(c => c.Nome == nome && (ignorarId == null || c.Id != ignorarId));
    }

    public async Task<bool> PossuiVinculosAsync(int cartaoId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        if (await context.RecargasCartao.AnyAsync(r => r.CartaoId == cartaoId))
            return true;
        if (await context.Transacoes.AnyAsync(t => t.CartaoId == cartaoId))
            return true;
        if (await context.ComprasCesta.AnyAsync(c => c.CartaoBeneficioId == cartaoId || c.CartaoCreditoId == cartaoId))
            return true;

        return false;
    }

    public async Task AdicionarAsync(Cartao cartao)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Cartoes.Add(cartao);
        await context.SaveChangesAsync();
    }

    public async Task AtualizarAsync(Cartao cartao)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Cartoes.Update(cartao);
        await context.SaveChangesAsync();
    }

    public async Task ExcluirAsync(Cartao cartao)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Cartoes.Remove(cartao);
        await context.SaveChangesAsync();
    }

    public async Task<List<RecargaCartao>> ListarRecargasAsync(int cartaoId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.RecargasCartao
            .Where(r => r.CartaoId == cartaoId)
            .OrderByDescending(r => r.Data)
            .ThenByDescending(r => r.Id)
            .ToListAsync();
    }

    public async Task<RecargaCartao?> ObterRecargaAsync(int recargaId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.RecargasCartao.FirstOrDefaultAsync(r => r.Id == recargaId);
    }

    public async Task AdicionarRecargaAsync(RecargaCartao recarga)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.RecargasCartao.Add(recarga);
        await context.SaveChangesAsync();
    }

    public async Task ExcluirRecargaAsync(RecargaCartao recarga)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.RecargasCartao.Remove(recarga);
        await context.SaveChangesAsync();
    }

    public async Task<decimal> ObterTotalRecargasAsync(int cartaoId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var valores = await context.RecargasCartao
            .Where(r => r.CartaoId == cartaoId)
            .Select(r => r.Valor)
            .ToListAsync();
        return CestaDespesaRegras.Dinheiro(valores.Sum());
    }

    public async Task<decimal> ObterTotalUsadoBeneficioAsync(int cartaoId, int? ignorarCompraId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.ComprasCesta.Where(c => c.CartaoBeneficioId == cartaoId);
        if (ignorarCompraId.HasValue)
            query = query.Where(c => c.Id != ignorarCompraId.Value);

        var valores = await query.Select(c => c.ValorBeneficio).ToListAsync();
        return CestaDespesaRegras.Dinheiro(valores.Sum());
    }

    public async Task<List<Transacao>> ListarFaturasAbertasAsync(int cartaoId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Transacoes
            .Include(t => t.Categoria)
            .Include(t => t.Cartao)
            .Include(t => t.Itens)
            .Where(t => t.Tipo == TipoTransacao.Despesa
                        && !t.Pago
                        && t.Categoria != null
                        && t.Categoria.Nome == NomeCategoriaCartaoCredito
                        && (t.CartaoId == cartaoId || t.CartaoId == null))
            .OrderByDescending(t => t.Data)
            .ThenByDescending(t => t.Id)
            .ToListAsync();
    }
}
