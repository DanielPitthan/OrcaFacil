using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Lancamentos;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Core.Services;

namespace OrcaFacil.Tests.Lancamentos;

public class EstornoFaturaServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly EstornoFaturaService _service;
    private readonly TransacaoRepository _transacoes;
    private readonly int _cartaoCategoriaId;
    private readonly int _alimentacaoId;
    private readonly int _membroId;

    public EstornoFaturaServiceTests()
    {
        _service = new EstornoFaturaService(_factory);
        _transacoes = new TransacaoRepository(_factory);
        var categorias = new CategoriaRepository(_factory);
        var membros = new MembroFamiliaRepository(_factory);

        var cartao = new Categoria { Nome = "Cartão de Crédito", Icone = "💳", CorHex = "#B2CFE6", PermiteItensFatura = true };
        var alimentacao = new Categoria { Nome = "Alimentação", Icone = "🛒", CorHex = "#B2CFE6" };
        categorias.AddAsync(cartao).GetAwaiter().GetResult();
        categorias.AddAsync(alimentacao).GetAwaiter().GetResult();
        var membro = new MembroFamilia { Nome = "Usuário" };
        membros.AddAsync(membro).GetAwaiter().GetResult();

        _cartaoCategoriaId = cartao.Id;
        _alimentacaoId = alimentacao.Id;
        _membroId = membro.Id;
    }

    [Fact]
    public async Task LancarAsync_FaturaPagaSemCompraOriginal_BaixaOTotalEGravaEstornoAvulso()
    {
        var fatura = await CriarFaturaAsync(150m, pago: true);

        var item = await _service.LancarAsync(fatura.Id, 40.50m, "  Estorno mercado ", _alimentacaoId);

        Assert.True(item.EhEstorno);
        Assert.Equal(40.50m, item.Valor);
        Assert.Equal("Estorno mercado", item.Descricao);
        Assert.Equal(NaturezaItemLancamento.Avulsa, item.Natureza);
        Assert.Null(item.ParcelaAtual);
        Assert.Null(item.TotalParcelas);
        Assert.Equal(0, item.Ordem);

        await using var contexto = _factory.CreateDbContext();
        var gravada = await contexto.Transacoes.Include(t => t.Itens).SingleAsync();
        Assert.Equal(109.50m, gravada.Valor);
        Assert.True(gravada.Pago);
        var estorno = Assert.Single(gravada.Itens);
        Assert.Equal(item.Id, estorno.Id);
        Assert.True(estorno.EhEstorno);
    }

    [Fact]
    public async Task LancarAsync_MaiorQueOGastoDaCategoria_PermiteEAbateNaSoma()
    {
        var fatura = await CriarFaturaAsync(500m, pago: false, itens:
        [
            new ItemLancamento
            {
                Descricao = "Almoço",
                Valor = 100m,
                CategoriaId = _alimentacaoId,
                Natureza = NaturezaItemLancamento.Avulsa,
                Ordem = 0
            }
        ]);

        await _service.LancarAsync(fatura.Id, 250m, "Estorno acima do gasto", _alimentacaoId);

        await using var contexto = _factory.CreateDbContext();
        var gravada = await contexto.Transacoes.Include(t => t.Itens).SingleAsync();
        Assert.Equal(250m, gravada.Valor);
        Assert.Equal(2, gravada.Itens.Count);
        Assert.Equal(1, gravada.Itens.Single(i => i.EhEstorno).Ordem);

        var somas = await _transacoes.SomaPorCategoriaAsync(fatura.Data, fatura.Data, TipoTransacao.Despesa);
        Assert.Equal(-150m, somas[_alimentacaoId]);
        Assert.Equal(400m, somas[_cartaoCategoriaId]);
        Assert.Equal(250m, somas.Values.Sum());
    }

    [Fact]
    public async Task LancarAsync_MesFechado_RecusaComMensagemPadraoENaoGrava()
    {
        var fatura = await CriarFaturaAsync(200m, pago: true);
        await using (var contexto = _factory.CreateDbContext())
        {
            contexto.FechamentosPeriodo.Add(new FechamentoPeriodo { Mes = 10, Ano = 2026, Fechado = true });
            await contexto.SaveChangesAsync();
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.LancarAsync(fatura.Id, 10m, "Estorno", _alimentacaoId));

        Assert.Equal(ItensFaturaRegras.MensagemMesFechado(fatura.Data), ex.Message);
        Assert.Contains("10/2026", ex.Message);
        Assert.Contains("está fechado", ex.Message);

        await using var conferido = _factory.CreateDbContext();
        var gravada = await conferido.Transacoes.Include(t => t.Itens).SingleAsync();
        Assert.Equal(200m, gravada.Valor);
        Assert.Empty(gravada.Itens);
    }

    [Fact]
    public async Task LancarAsync_TotalLiquidoAbaixoDoMinimo_RecusaENaoGrava()
    {
        var fatura = await CriarFaturaAsync(30m, pago: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.LancarAsync(fatura.Id, 30m, "Estorno total", _alimentacaoId));

        Assert.Contains("0,01", ex.Message);

        await using var conferido = _factory.CreateDbContext();
        var gravada = await conferido.Transacoes.Include(t => t.Itens).SingleAsync();
        Assert.Equal(30m, gravada.Valor);
        Assert.Empty(gravada.Itens);
    }

    private async Task<Transacao> CriarFaturaAsync(decimal valor, bool pago, List<ItemLancamento>? itens = null)
    {
        var fatura = new Transacao
        {
            Descricao = "Fatura Nubank",
            Valor = valor,
            Data = new DateTime(2026, 10, 18),
            Tipo = TipoTransacao.Despesa,
            Pago = pago,
            CategoriaId = _cartaoCategoriaId,
            MembroFamiliaId = _membroId,
            Itens = itens ?? []
        };
        await _transacoes.AddAsync(fatura);
        return fatura;
    }

    public void Dispose() => _factory.Dispose();
}
