using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Cesta;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Lancamentos;
using OrcaFacil.Core.Llm;

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
            .Include(c => c.CartaoBeneficio)
            .Include(c => c.CartaoCredito)
            .Include(c => c.ItemFatura)
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
            .Include(c => c.CartaoBeneficio)
            .Include(c => c.CartaoCredito)
            .Include(c => c.ItemFatura)
            .Include(c => c.Itens)
            .ThenInclude(i => i.Produto)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<CompraCesta> SalvarCompraAsync(
        CompraCesta compra,
        Transacao? despesa,
        FaturaCreditoCesta? faturaCredito = null,
        IReadOnlyList<ProdutoNovoCompra>? produtosNovos = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await using var transacao = await context.Database.BeginTransactionAsync();
        await GarantirPeriodoAbertoNoContextoAsync(context, compra.Data);
        if (produtosNovos is { Count: > 0 })
            await MaterializarProdutosNovosAsync(context, compra, produtosNovos);

        var salva = await GravarCompraNoContextoAsync(context, compra, despesa, faturaCredito);
        await transacao.CommitAsync();
        return salva;
    }

    private static async Task GarantirPeriodoAbertoNoContextoAsync(AppDbContext context, DateTime data)
    {
        var fechado = await context.FechamentosPeriodo.AnyAsync(f => f.Mes == data.Month && f.Ano == data.Year && f.Fechado);
        if (fechado)
            throw new InvalidOperationException(ItensFaturaRegras.MensagemMesFechado(data));
    }

    private static async Task MaterializarProdutosNovosAsync(
        AppDbContext context,
        CompraCesta compra,
        IReadOnlyList<ProdutoNovoCompra> novos)
    {
        if (compra.Itens is not IList<CompraCestaItem> itens)
            throw new InvalidOperationException("A compra não aceita os produtos novos.");

        var porChave = new Dictionary<string, ProdutoCesta>(StringComparer.Ordinal);
        foreach (var produto in await context.ProdutosCesta.OrderBy(p => p.Id).ToListAsync())
        {
            var chaveExistente = NormalizadorComprovante.TextoComparacao(produto.Nome);
            if (chaveExistente.Length > 0)
                porChave.TryAdd(chaveExistente, produto);
        }

        var houveNovo = false;
        foreach (var novo in novos)
        {
            var chave = NormalizadorComprovante.TextoComparacao(novo.Nome);
            if (chave.Length == 0)
                throw new InvalidOperationException("Informe o nome do produto.");

            if (novo.CategoriaId <= 0)
                throw new InvalidOperationException("Selecione a categoria do produto.");

            if (porChave.ContainsKey(chave))
                continue;

            porChave[chave] = new ProdutoCesta
            {
                Nome = novo.Nome.Trim(),
                CategoriaId = novo.CategoriaId,
                Ativo = true
            };
            context.ProdutosCesta.Add(porChave[chave]);
            houveNovo = true;
        }

        if (houveNovo)
            await context.SaveChangesAsync();

        foreach (var novo in novos)
        {
            var chave = NormalizadorComprovante.TextoComparacao(novo.Nome);
            var produto = porChave[chave];
            var item = itens[novo.IndiceItem];
            item.Produto = null;
            item.ProdutoCestaId = produto.Id;
        }
    }

    private static async Task<CompraCesta> GravarCompraNoContextoAsync(
        AppDbContext context,
        CompraCesta compra,
        Transacao? despesa,
        FaturaCreditoCesta? faturaCredito)
    {
        if (compra.Id == 0)
        {
            if (faturaCredito is not null)
                compra.ItemFaturaId = await ProcessarFaturaCreditoAsync(context, null, faturaCredito);

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
        atual.CartaoBeneficioId = compra.CartaoBeneficioId;
        atual.ValorDinheiro = compra.ValorDinheiro;
        atual.ValorCartaoCredito = compra.ValorCartaoCredito;
        atual.CartaoCreditoId = compra.CartaoCreditoId;

        atual.ItemFaturaId = await ProcessarFaturaCreditoAsync(context, atual.ItemFaturaId, faturaCredito);

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
        var itemFaturaId = compra.ItemFaturaId;
        compra.Transacao = null;
        compra.TransacaoId = null;
        compra.ItemFatura = null;
        compra.ItemFaturaId = null;
        context.ComprasCesta.Remove(compra);
        await context.SaveChangesAsync();

        if (despesaId is int transacaoId)
            await RemoverTransacaoAsync(context, transacaoId);

        if (itemFaturaId is int itemId)
            await RemoverItemFaturaAsync(context, itemId);
    }

    public async Task<int> ObterOuCriarCategoriaCestaAsync()
        => await ObterOuCriarCategoriaPadraoAsync(CestaDespesaRegras.NomeCategoria, "🧺", "#6DA538");

    public async Task<string> TextoCatalogoLeituraAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await CatalogoPedidoComprovante.MontarAsync(context, cancellationToken);
    }

    public async Task<int> ObterOuCriarCategoriaCartaoCreditoAsync()
        => await ObterOuCriarCategoriaPadraoAsync(CartaoRepository.NomeCategoriaCartaoCredito, "💳", "#B2CFE6");

    private async Task<int> ObterOuCriarCategoriaPadraoAsync(string nome, string icone, string corHex)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await GarantirCategoriaNoContextoAsync(context, nome, icone, corHex);
    }

    private static async Task<int> GarantirCategoriaNoContextoAsync(AppDbContext context, string nome, string icone, string corHex)
    {
        var categoria = await context.Categorias.FirstOrDefaultAsync(c => c.Nome == nome);
        if (categoria is null)
        {
            categoria = new Categoria
            {
                Nome = nome,
                Icone = icone,
                CorHex = corHex,
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

    private static async Task<int?> ProcessarFaturaCreditoAsync(
        AppDbContext context,
        int? itemFaturaAnteriorId,
        FaturaCreditoCesta? faturaCredito)
    {
        if (faturaCredito is null || faturaCredito.Valor <= 0)
        {
            if (itemFaturaAnteriorId is int itemId)
                await RemoverItemFaturaAsync(context, itemId);
            return null;
        }

        var cartao = await context.Cartoes.FirstOrDefaultAsync(c => c.Id == faturaCredito.CartaoCreditoId)
            ?? throw new InvalidOperationException("Selecione o cartão de crédito.");

        if (itemFaturaAnteriorId is int itemAnteriorId)
        {
            var itemAnterior = await context.ItensLancamento
                .Include(i => i.Transacao!)
                .ThenInclude(t => t.Itens)
                .FirstOrDefaultAsync(i => i.Id == itemAnteriorId);

            if (itemAnterior?.Transacao is not null)
            {
                var transacaoAnterior = itemAnterior.Transacao;
                var mesmaFatura = faturaCredito.FaturaTransacaoId is null
                    ? transacaoAnterior.CartaoId == cartao.Id
                    : transacaoAnterior.Id == faturaCredito.FaturaTransacaoId.Value;

                if (mesmaFatura)
                {
                    if (itemAnterior.EhEstorno)
                        throw new InvalidOperationException("O item da fatura vinculado à compra não pode ser um estorno.");

                    var diferenca = CestaDespesaRegras.Dinheiro(faturaCredito.Valor - itemAnterior.Valor);
                    itemAnterior.Descricao = faturaCredito.DescricaoItem;
                    itemAnterior.Valor = faturaCredito.Valor;
                    itemAnterior.CategoriaId = faturaCredito.CategoriaCestaId;
                    transacaoAnterior.CartaoId = cartao.Id;
                    transacaoAnterior.Valor = CestaDespesaRegras.Dinheiro(Math.Max(0.01m, transacaoAnterior.Valor + diferenca));
                    transacaoAnterior.AtualizadoEm = DateTime.UtcNow;
                    await context.SaveChangesAsync();
                    return itemAnterior.Id;
                }
            }

            await RemoverItemFaturaAsync(context, itemAnteriorId);
        }

        if (faturaCredito.FaturaTransacaoId is int faturaId)
        {
            var fatura = await context.Transacoes
                .Include(t => t.Itens)
                .FirstOrDefaultAsync(t => t.Id == faturaId)
                ?? throw new InvalidOperationException("A fatura escolhida não foi encontrada.");

            if (fatura.CartaoId is not null && fatura.CartaoId != cartao.Id)
                throw new InvalidOperationException("A fatura escolhida pertence a outro cartão.");

            fatura.CartaoId = cartao.Id;
            fatura.Valor = CestaDespesaRegras.Dinheiro(fatura.Valor + faturaCredito.Valor);
            fatura.AtualizadoEm = DateTime.UtcNow;

            var ordem = fatura.Itens.Count == 0 ? 0 : fatura.Itens.Max(i => i.Ordem) + 1;
            var novoItem = new ItemLancamento
            {
                TransacaoId = fatura.Id,
                Descricao = faturaCredito.DescricaoItem,
                Valor = faturaCredito.Valor,
                Natureza = NaturezaItemLancamento.Avulsa,
                CategoriaId = faturaCredito.CategoriaCestaId,
                Ordem = ordem
            };
            context.ItensLancamento.Add(novoItem);
            await context.SaveChangesAsync();
            return novoItem.Id;
        }

        var categoriaCartaoId = await GarantirCategoriaNoContextoAsync(
            context, CartaoRepository.NomeCategoriaCartaoCredito, "💳", "#B2CFE6");

        var descricaoFatura = $"Fatura {cartao.Nome}";
        if (descricaoFatura.Length > 120)
            descricaoFatura = descricaoFatura[..120];

        var novaFatura = new Transacao
        {
            Descricao = descricaoFatura,
            Valor = faturaCredito.Valor,
            Data = faturaCredito.DataCompra,
            Tipo = TipoTransacao.Despesa,
            Recorrencia = TipoRecorrencia.Unica,
            Pago = false,
            CategoriaId = categoriaCartaoId,
            CartaoId = cartao.Id,
            MembroFamiliaId = faturaCredito.MembroFamiliaId
        };
        var itemNovaFatura = new ItemLancamento
        {
            Descricao = faturaCredito.DescricaoItem,
            Valor = faturaCredito.Valor,
            Natureza = NaturezaItemLancamento.Avulsa,
            CategoriaId = faturaCredito.CategoriaCestaId,
            Ordem = 0
        };
        novaFatura.Itens.Add(itemNovaFatura);
        context.Transacoes.Add(novaFatura);
        await context.SaveChangesAsync();
        return itemNovaFatura.Id;
    }

    private static async Task RemoverItemFaturaAsync(AppDbContext context, int itemFaturaId)
    {
        var item = await context.ItensLancamento.FirstOrDefaultAsync(i => i.Id == itemFaturaId);
        if (item is null || item.EhEstorno)
            return;

        var transacaoId = item.TransacaoId;
        var valorItem = ItensFaturaRegras.ValorAssinado(item);
        context.ItensLancamento.Remove(item);
        await context.SaveChangesAsync();

        var fatura = await context.Transacoes
            .Include(t => t.Itens)
            .FirstOrDefaultAsync(t => t.Id == transacaoId);
        if (fatura is null)
            return;

        var novoTotal = CestaDespesaRegras.Dinheiro(fatura.Valor - valorItem);
        var somaItensRestantes = CestaDespesaRegras.Dinheiro(fatura.Itens.Sum(ItensFaturaRegras.ValorAssinado));

        if (fatura.Itens.Count == 0 && novoTotal <= 0)
        {
            context.Transacoes.Remove(fatura);
            await context.SaveChangesAsync();
            return;
        }

        var liquido = Math.Max(ItensFaturaRegras.TotalMinimo, novoTotal);
        if (somaItensRestantes > liquido)
            liquido = somaItensRestantes;

        fatura.Valor = liquido;
        fatura.AtualizadoEm = DateTime.UtcNow;
        await context.SaveChangesAsync();
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
