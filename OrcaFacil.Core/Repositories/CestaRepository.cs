using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Cesta;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Repositories;

public class CestaRepository : ICestaRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public CestaRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<ProdutoCesta>> ListarProdutosAsync(bool somenteAtivos)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.ProdutosCesta.Include(p => p.Categoria).AsQueryable();
        if (somenteAtivos)
            query = query.Where(p => p.Ativo);

        return await query.OrderBy(p => p.Nome).ToListAsync();
    }

    public async Task<ProdutoCesta?> ObterProdutoAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ProdutosCesta.FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<bool> ExisteNomeProdutoAsync(string nome, int? ignorarId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ProdutosCesta.AnyAsync(p => p.Nome == nome && (ignorarId == null || p.Id != ignorarId));
    }

    public async Task<bool> ProdutoPossuiComprasAsync(int produtoId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ItensCompraCesta.AnyAsync(i => i.ProdutoCestaId == produtoId);
    }

    public async Task AdicionarProdutoAsync(ProdutoCesta produto)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.ProdutosCesta.Add(produto);
        await context.SaveChangesAsync();
    }

    public async Task AtualizarProdutoAsync(ProdutoCesta produto)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        produto.Categoria = null;
        context.ProdutosCesta.Update(produto);
        await context.SaveChangesAsync();
    }

    public async Task ExcluirProdutoAsync(ProdutoCesta produto)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.ProdutosCesta.Remove(produto);
        await context.SaveChangesAsync();
    }

    public async Task<List<LocalCompra>> ListarLocaisAsync(bool somenteAtivos)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.LocaisCompra.AsQueryable();
        if (somenteAtivos)
            query = query.Where(l => l.Ativo);

        return await query.OrderBy(l => l.Nome).ToListAsync();
    }

    public async Task<LocalCompra?> ObterLocalAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.LocaisCompra.FirstOrDefaultAsync(l => l.Id == id);
    }

    public async Task<bool> ExisteNomeLocalAsync(string nome, int? ignorarId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.LocaisCompra.AnyAsync(l => l.Nome == nome && (ignorarId == null || l.Id != ignorarId));
    }

    public async Task<bool> LocalPossuiComprasAsync(int localId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ComprasCesta.AnyAsync(c => c.LocalCompraId == localId);
    }

    public async Task AdicionarLocalAsync(LocalCompra local)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.LocaisCompra.Add(local);
        await context.SaveChangesAsync();
    }

    public async Task AtualizarLocalAsync(LocalCompra local)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.LocaisCompra.Update(local);
        await context.SaveChangesAsync();
    }

    public async Task ExcluirLocalAsync(LocalCompra local)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.LocaisCompra.Remove(local);
        await context.SaveChangesAsync();
    }

    public async Task<List<CompraCesta>> ListarComprasAsync(int? mes, int? ano)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.ComprasCesta
            .Include(c => c.LocalCompra)
            .Include(c => c.Itens)
            .ThenInclude(i => i.Produto)
            .AsQueryable();

        if (mes.HasValue && ano.HasValue)
            query = query.Where(c => c.Mes == mes.Value && c.Ano == ano.Value);

        return await query
            .OrderByDescending(c => c.Data)
            .ThenByDescending(c => c.Id)
            .ToListAsync();
    }

    public async Task<CompraCesta?> ObterCompraAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ComprasCesta
            .Include(c => c.LocalCompra)
            .Include(c => c.Itens)
            .ThenInclude(i => i.Produto)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<CompraCesta> SalvarCompraAsync(CompraCesta compra, Transacao? despesa)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        if (compra.Id == 0)
        {
            compra.Transacao = despesa;
            context.ComprasCesta.Add(compra);
            await context.SaveChangesAsync();
            return compra;
        }

        var atual = await context.ComprasCesta
            .Include(c => c.Itens)
            .Include(c => c.Transacao!)
            .ThenInclude(t => t.Itens)
            .FirstOrDefaultAsync(c => c.Id == compra.Id)
            ?? throw new InvalidOperationException("Compra não encontrada.");

        atual.Data = compra.Data;
        atual.Mes = compra.Mes;
        atual.Ano = compra.Ano;
        atual.LocalCompraId = compra.LocalCompraId;
        atual.ValorBeneficio = compra.ValorBeneficio;
        atual.TipoCartaoBeneficio = compra.TipoCartaoBeneficio;
        atual.ValorDinheiro = compra.ValorDinheiro;
        atual.ValorCartaoCredito = compra.ValorCartaoCredito;

        context.ItensCompraCesta.RemoveRange(atual.Itens.ToList());
        atual.Itens.Clear();
        foreach (var item in compra.Itens)
        {
            item.Id = 0;
            item.CompraCestaId = atual.Id;
            item.CompraCesta = null;
            item.Produto = null;
            atual.Itens.Add(item);
        }

        if (despesa is null)
        {
            await RemoverDespesaAsync(context, atual);
        }
        else if (atual.Transacao is null)
        {
            atual.Transacao = despesa;
        }
        else
        {
            var transacao = atual.Transacao;
            transacao.Descricao = despesa.Descricao;
            transacao.Valor = despesa.Valor;
            transacao.Data = despesa.Data;
            transacao.Observacao = despesa.Observacao;
            transacao.CategoriaId = despesa.CategoriaId;
            transacao.Pago = true;
            transacao.AtualizadoEm = DateTime.UtcNow;
            context.ItensLancamento.RemoveRange(transacao.Itens.ToList());
            transacao.Itens.Clear();
            foreach (var item in despesa.Itens)
            {
                item.Id = 0;
                item.TransacaoId = transacao.Id;
                item.Transacao = null;
                item.Categoria = null;
                transacao.Itens.Add(item);
            }
        }

        await context.SaveChangesAsync();
        return atual;
    }

    public async Task ExcluirCompraAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var compra = await context.ComprasCesta
            .Include(c => c.Transacao)
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new InvalidOperationException("Compra não encontrada.");

        var despesaId = compra.TransacaoId;
        compra.Transacao = null;
        compra.TransacaoId = null;
        context.ComprasCesta.Remove(compra);
        await context.SaveChangesAsync();

        if (despesaId is int transacaoId)
            await RemoverTransacaoAsync(context, transacaoId);
    }

    public async Task<int> ObterOuCriarCategoriaCestaAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var categoria = await context.Categorias.FirstOrDefaultAsync(c => c.Nome == CestaDespesaRegras.NomeCategoria);
        if (categoria is null)
        {
            categoria = new Categoria
            {
                Nome = CestaDespesaRegras.NomeCategoria,
                Icone = "🧺",
                CorHex = "#6DA538",
                TipoPadrao = TipoTransacao.Despesa,
                EhPadraoDoSistema = true,
                PermiteItensFatura = true,
                Ativo = true
            };
            context.Categorias.Add(categoria);
            await context.SaveChangesAsync();
            return categoria.Id;
        }

        var mudou = false;
        if (!categoria.Ativo)
        {
            categoria.Ativo = true;
            mudou = true;
        }

        if (!categoria.PermiteItensFatura)
        {
            categoria.PermiteItensFatura = true;
            mudou = true;
        }

        if (mudou)
            await context.SaveChangesAsync();

        return categoria.Id;
    }

    private static async Task RemoverDespesaAsync(AppDbContext context, CompraCesta compra)
    {
        if (compra.TransacaoId is not int transacaoId)
            return;

        compra.Transacao = null;
        compra.TransacaoId = null;
        await context.SaveChangesAsync();
        await RemoverTransacaoAsync(context, transacaoId);
    }

    private static async Task RemoverTransacaoAsync(AppDbContext context, int transacaoId)
    {
        var transacao = await context.Transacoes.FirstOrDefaultAsync(t => t.Id == transacaoId);
        if (transacao is null)
            return;

        context.Transacoes.Remove(transacao);
        await context.SaveChangesAsync();
    }
}
