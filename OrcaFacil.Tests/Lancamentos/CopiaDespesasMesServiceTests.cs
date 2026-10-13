using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Core.Services;

namespace OrcaFacil.Tests.Lancamentos;

public class CopiaDespesasMesServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly TransacaoRepository _transacoes;
    private readonly CopiaDespesasMesService _service;
    private readonly int _cartaoId;
    private readonly int _alimentacaoId;
    private readonly int _lazerId;
    private readonly int _transporteId;
    private readonly int _membroId;

    public CopiaDespesasMesServiceTests()
    {
        _transacoes = new TransacaoRepository(_factory);
        _service = new CopiaDespesasMesService(_transacoes);

        var categorias = new CategoriaRepository(_factory);
        var membros = new MembroFamiliaRepository(_factory);

        var cartao = new Categoria { Nome = "Cartão de Crédito", Icone = "💳", CorHex = "#B2CFE6", PermiteItensFatura = true };
        var alimentacao = new Categoria { Nome = "Alimentação", Icone = "🛒", CorHex = "#B2CFE6" };
        var lazer = new Categoria { Nome = "Lazer", Icone = "🎉", CorHex = "#A2B94F" };
        var transporte = new Categoria { Nome = "Transporte", Icone = "🚗", CorHex = "#A2B94F" };
        categorias.AddAsync(cartao).GetAwaiter().GetResult();
        categorias.AddAsync(alimentacao).GetAwaiter().GetResult();
        categorias.AddAsync(lazer).GetAwaiter().GetResult();
        categorias.AddAsync(transporte).GetAwaiter().GetResult();

        var membro = new MembroFamilia { Nome = "Usuário" };
        membros.AddAsync(membro).GetAwaiter().GetResult();

        _cartaoId = cartao.Id;
        _alimentacaoId = alimentacao.Id;
        _lazerId = lazer.Id;
        _transporteId = transporte.Id;
        _membroId = membro.Id;
    }

    [Fact]
    public async Task CopiarAsync_FaturaComItens_CopiaAssinaturaAvancaParcelaERecalculaTotal()
    {
        await _transacoes.AddAsync(Fatura(new DateTime(2026, 3, 18), TipoRecorrencia.Unica, 5000m,
        [
            Item("Netflix", 70m, NaturezaItemLancamento.Assinatura, _lazerId, 0),
            Item("Mesa de jantar", 2000m, NaturezaItemLancamento.Parcelada, _alimentacaoId, 1, 2, 12),
            Item("Almoço", 500m, NaturezaItemLancamento.Avulsa, _alimentacaoId, 2),
            Item("Consultoria", 100m, NaturezaItemLancamento.Parcelada, _transporteId, 3, 12, 12)
        ]));

        var copias = await _service.CopiarAsync(4, 2026);

        Assert.Single(copias);
        var copia = (await _transacoes.GetByPeriodoAsync(new DateTime(2026, 4, 1), new DateTime(2026, 4, 30, 23, 59, 59))).Single();
        Assert.Equal(2070m, copia.Valor);
        Assert.Equal(2, copia.Itens.Count);

        var netflix = copia.Itens.Single(i => i.Descricao == "Netflix");
        Assert.Equal(NaturezaItemLancamento.Assinatura, netflix.Natureza);
        Assert.Equal(70m, netflix.Valor);

        var mesa = copia.Itens.Single(i => i.Descricao == "Mesa de jantar");
        Assert.Equal(3, mesa.ParcelaAtual);
        Assert.Equal(12, mesa.TotalParcelas);
        Assert.Equal(2000m, mesa.Valor);
        Assert.DoesNotContain(copia.Itens, i => i.Descricao is "Almoço" or "Consultoria");
    }

    [Fact]
    public async Task CopiarAsync_FaturaRecorrenteSemItens_CopiaValorCheio()
    {
        await _transacoes.AddAsync(Fatura(new DateTime(2026, 3, 10), TipoRecorrencia.Mensal, 1240m, []));

        var copias = await _service.CopiarAsync(4, 2026);

        Assert.Single(copias);
        Assert.Equal(1240m, copias[0].Valor);
        Assert.Empty(copias[0].Itens);
    }

    [Fact]
    public async Task CopiarAsync_SoAvulsas_NaoCopia()
    {
        await _transacoes.AddAsync(Fatura(new DateTime(2026, 3, 10), TipoRecorrencia.Mensal, 500m,
        [
            Item("Almoço", 500m, NaturezaItemLancamento.Avulsa, _alimentacaoId, 0)
        ]));

        var copias = await _service.CopiarAsync(4, 2026);

        Assert.Empty(copias);
    }

    private Transacao Fatura(DateTime data, TipoRecorrencia recorrencia, decimal valor, List<ItemLancamento> itens) => new()
    {
        Descricao = "Fatura cartão",
        Valor = valor,
        Data = data,
        Tipo = TipoTransacao.Despesa,
        Recorrencia = recorrencia,
        CategoriaId = _cartaoId,
        MembroFamiliaId = _membroId,
        Itens = itens
    };

    private static ItemLancamento Item(string descricao, decimal valor, NaturezaItemLancamento natureza, int categoriaId, int ordem, int? parcelaAtual = null, int? totalParcelas = null) => new()
    {
        Descricao = descricao,
        Valor = valor,
        Natureza = natureza,
        CategoriaId = categoriaId,
        Ordem = ordem,
        ParcelaAtual = parcelaAtual,
        TotalParcelas = totalParcelas
    };

    public void Dispose() => _factory.Dispose();
}
