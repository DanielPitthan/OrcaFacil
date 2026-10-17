using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Repositories;
using Xunit;

namespace OrcaFacil.Tests.Repositories;

public class TransacaoRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly TransacaoRepository _repository;
    private readonly CategoriaRepository _categoriaRepository;
    private readonly MembroFamiliaRepository _membroRepository;

    public TransacaoRepositoryTests()
    {
        _repository = new TransacaoRepository(_factory);
        _categoriaRepository = new CategoriaRepository(_factory);
        _membroRepository = new MembroFamiliaRepository(_factory);
    }

    private async Task<(int categoriaId, int membroId)> CriarDependenciasAsync()
    {
        var categoria = new Categoria { Nome = "Alimentação", Icone = "🛒", CorHex = "#9fb4c7" };
        await _categoriaRepository.AddAsync(categoria);

        var membro = new MembroFamilia { Nome = "Usuário" };
        await _membroRepository.AddAsync(membro);

        return (categoria.Id, membro.Id);
    }

    [Fact]
    public async Task SomaPorCategoriaAsync_DeveAgruparCorretamente()
    {
        var (categoriaId, membroId) = await CriarDependenciasAsync();
        var mes = new DateTime(2026, 3, 1);

        await _repository.AddAsync(new Transacao { Descricao = "Mercado", Valor = 100m, Data = mes.AddDays(2), Tipo = TipoTransacao.Despesa, CategoriaId = categoriaId, MembroFamiliaId = membroId });
        await _repository.AddAsync(new Transacao { Descricao = "Feira", Valor = 50m, Data = mes.AddDays(10), Tipo = TipoTransacao.Despesa, CategoriaId = categoriaId, MembroFamiliaId = membroId });

        var somas = await _repository.SomaPorCategoriaAsync(mes, mes.AddMonths(1).AddTicks(-1), TipoTransacao.Despesa);

        Assert.Equal(150m, somas[categoriaId]);
    }

    [Fact]
    public async Task SomaPorCategoriaAsync_ComItensParciais_DistribuiRestanteSemDobrarTotal()
    {
        var (alimentacaoId, membroId) = await CriarDependenciasAsync();
        var cartao = new Categoria { Nome = "Cartão de Crédito", Icone = "💳", CorHex = "#B2CFE6", PermiteItensFatura = true };
        var transporte = new Categoria { Nome = "Transporte", Icone = "🚗", CorHex = "#A2B94F" };
        await _categoriaRepository.AddAsync(cartao);
        await _categoriaRepository.AddAsync(transporte);

        var mes = new DateTime(2026, 3, 1);
        await _repository.AddAsync(new Transacao
        {
            Descricao = "Fatura",
            Valor = 5000m,
            Data = mes.AddDays(18),
            Tipo = TipoTransacao.Despesa,
            CategoriaId = cartao.Id,
            MembroFamiliaId = membroId,
            Itens =
            [
                new ItemLancamento { Descricao = "Almoço", Valor = 500m, CategoriaId = alimentacaoId, Natureza = NaturezaItemLancamento.Avulsa },
                new ItemLancamento { Descricao = "Gasolina", Valor = 500m, CategoriaId = transporte.Id, Natureza = NaturezaItemLancamento.Avulsa }
            ]
        });
        await _repository.AddAsync(new Transacao
        {
            Descricao = "Feira",
            Valor = 80m,
            Data = mes.AddDays(2),
            Tipo = TipoTransacao.Despesa,
            CategoriaId = alimentacaoId,
            MembroFamiliaId = membroId
        });

        var inicio = mes;
        var fim = mes.AddMonths(1).AddTicks(-1);
        var somas = await _repository.SomaPorCategoriaAsync(inicio, fim, TipoTransacao.Despesa);
        var totalDespesas = await _repository.SomaPorTipoAsync(TipoTransacao.Despesa, inicio, fim);

        Assert.Equal(580m, somas[alimentacaoId]);
        Assert.Equal(500m, somas[transporte.Id]);
        Assert.Equal(4000m, somas[cartao.Id]);
        Assert.Equal(5080m, totalDespesas);
        Assert.Equal(totalDespesas, somas.Values.Sum());
    }

    [Fact]
    public async Task SomaPorCategoriaAsync_ComEstorno_AbateOCreditoDaCategoria()
    {
        var (alimentacaoId, membroId) = await CriarDependenciasAsync();
        var cartao = new Categoria { Nome = "Cartão de Crédito", Icone = "💳", CorHex = "#B2CFE6", PermiteItensFatura = true };
        await _categoriaRepository.AddAsync(cartao);

        var mes = new DateTime(2026, 3, 1);
        await _repository.AddAsync(new Transacao
        {
            Descricao = "Fatura",
            Valor = 400m,
            Data = mes.AddDays(18),
            Tipo = TipoTransacao.Despesa,
            CategoriaId = cartao.Id,
            MembroFamiliaId = membroId,
            Itens =
            [
                new ItemLancamento { Descricao = "Almoço", Valor = 300m, CategoriaId = alimentacaoId, Natureza = NaturezaItemLancamento.Avulsa, Ordem = 0 },
                new ItemLancamento { Descricao = "Estorno almoço", Valor = 100m, EhEstorno = true, CategoriaId = alimentacaoId, Natureza = NaturezaItemLancamento.Avulsa, Ordem = 1 }
            ]
        });
        await _repository.AddAsync(new Transacao
        {
            Descricao = "Feira",
            Valor = 80m,
            Data = mes.AddDays(2),
            Tipo = TipoTransacao.Despesa,
            CategoriaId = alimentacaoId,
            MembroFamiliaId = membroId
        });

        var inicio = mes;
        var fim = mes.AddMonths(1).AddTicks(-1);
        var somas = await _repository.SomaPorCategoriaAsync(inicio, fim, TipoTransacao.Despesa);
        var totalDespesas = await _repository.SomaPorTipoAsync(TipoTransacao.Despesa, inicio, fim);

        Assert.Equal(280m, somas[alimentacaoId]);
        Assert.Equal(200m, somas[cartao.Id]);
        Assert.Equal(480m, totalDespesas);
        Assert.Equal(totalDespesas, somas.Values.Sum());
    }

    [Fact]
    public async Task GetByPeriodoAsync_FiltraPelaCategoriaDoItem()
    {
        var (alimentacaoId, membroId) = await CriarDependenciasAsync();
        var cartao = new Categoria { Nome = "Cartão de Crédito", Icone = "💳", CorHex = "#B2CFE6", PermiteItensFatura = true };
        await _categoriaRepository.AddAsync(cartao);
        var mes = new DateTime(2026, 3, 1);

        await _repository.AddAsync(new Transacao
        {
            Descricao = "Fatura",
            Valor = 200m,
            Data = mes.AddDays(5),
            Tipo = TipoTransacao.Despesa,
            CategoriaId = cartao.Id,
            MembroFamiliaId = membroId,
            Itens = [new ItemLancamento { Descricao = "Almoço", Valor = 50m, CategoriaId = alimentacaoId }]
        });

        var lista = await _repository.GetByPeriodoAsync(mes, mes.AddMonths(1).AddTicks(-1), alimentacaoId);

        var fatura = Assert.Single(lista);
        Assert.Equal("Fatura", fatura.Descricao);
        Assert.Equal("Almoço", Assert.Single(fatura.Itens).Descricao);
    }

    [Fact]
    public async Task UpdateAsync_NaoApagaItensDaFatura()
    {
        var (alimentacaoId, membroId) = await CriarDependenciasAsync();
        var cartao = new Categoria { Nome = "Cartão de Crédito", Icone = "💳", CorHex = "#B2CFE6", PermiteItensFatura = true };
        await _categoriaRepository.AddAsync(cartao);

        var transacao = new Transacao
        {
            Descricao = "Fatura",
            Valor = 200m,
            Data = DateTime.Today,
            Tipo = TipoTransacao.Despesa,
            CategoriaId = cartao.Id,
            MembroFamiliaId = membroId,
            Itens = [new ItemLancamento { Descricao = "Almoço", Valor = 50m, CategoriaId = alimentacaoId }]
        };
        await _repository.AddAsync(transacao);

        var carregada = await _repository.GetByIdAsync(transacao.Id);
        carregada!.Pago = true;
        await _repository.UpdateAsync(carregada);

        var atualizada = await _repository.GetByIdAsync(transacao.Id);
        Assert.True(atualizada!.Pago);
        Assert.Equal("Almoço", Assert.Single(atualizada.Itens).Descricao);

        await _repository.DeleteAsync(atualizada);
        Assert.Null(await _repository.GetByIdAsync(transacao.Id));
    }

    [Fact]
    public async Task SomaPorTipoAsync_DeveFiltrarPorPeriodo()
    {
        var (categoriaId, membroId) = await CriarDependenciasAsync();

        await _repository.AddAsync(new Transacao { Descricao = "Dentro", Valor = 200m, Data = new DateTime(2026, 3, 15), Tipo = TipoTransacao.Receita, CategoriaId = categoriaId, MembroFamiliaId = membroId });
        await _repository.AddAsync(new Transacao { Descricao = "Fora", Valor = 999m, Data = new DateTime(2026, 4, 1), Tipo = TipoTransacao.Receita, CategoriaId = categoriaId, MembroFamiliaId = membroId });

        var total = await _repository.SomaPorTipoAsync(TipoTransacao.Receita, new DateTime(2026, 3, 1), new DateTime(2026, 3, 31, 23, 59, 59));

        Assert.Equal(200m, total);
    }

    [Fact]
    public async Task GetRecorrentesAtivasAsync_DeveIgnorarUnicas()
    {
        var (categoriaId, membroId) = await CriarDependenciasAsync();

        await _repository.AddAsync(new Transacao { Descricao = "Assinatura", Valor = 30m, Data = DateTime.Today, Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Mensal, CategoriaId = categoriaId, MembroFamiliaId = membroId });
        await _repository.AddAsync(new Transacao { Descricao = "Compra avulsa", Valor = 30m, Data = DateTime.Today, Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Unica, CategoriaId = categoriaId, MembroFamiliaId = membroId });

        var recorrentes = await _repository.GetRecorrentesAtivasAsync();

        Assert.Single(recorrentes);
        Assert.Equal("Assinatura", recorrentes[0].Descricao);
    }

    [Fact]
    public async Task SomaPorTipoAsync_DeveFiltrarPorPago()
    {
        var (categoriaId, membroId) = await CriarDependenciasAsync();
        var mes = new DateTime(2026, 3, 1);

        await _repository.AddAsync(new Transacao { Descricao = "Paga", Valor = 100m, Data = mes.AddDays(2), Tipo = TipoTransacao.Despesa, Pago = true, CategoriaId = categoriaId, MembroFamiliaId = membroId });
        await _repository.AddAsync(new Transacao { Descricao = "Pendente", Valor = 50m, Data = mes.AddDays(10), Tipo = TipoTransacao.Despesa, Pago = false, CategoriaId = categoriaId, MembroFamiliaId = membroId });

        var totalPago = await _repository.SomaPorTipoAsync(TipoTransacao.Despesa, mes, mes.AddMonths(1).AddTicks(-1), pago: true);
        var totalGeral = await _repository.SomaPorTipoAsync(TipoTransacao.Despesa, mes, mes.AddMonths(1).AddTicks(-1));

        Assert.Equal(100m, totalPago);
        Assert.Equal(150m, totalGeral);
    }

    [Fact]
    public async Task GetDespesasRecorrentesPorPeriodoAsync_DeveFiltrarTipoRecorrenciaEPeriodo()
    {
        var (categoriaId, membroId) = await CriarDependenciasAsync();
        var marco = new DateTime(2026, 3, 15);

        await _repository.AddAsync(new Transacao { Descricao = "Assinatura", Valor = 30m, Data = marco, Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Mensal, CategoriaId = categoriaId, MembroFamiliaId = membroId });
        await _repository.AddAsync(new Transacao { Descricao = "Avulsa", Valor = 30m, Data = marco, Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Unica, CategoriaId = categoriaId, MembroFamiliaId = membroId });
        await _repository.AddAsync(new Transacao { Descricao = "Salário", Valor = 3000m, Data = marco, Tipo = TipoTransacao.Receita, Recorrencia = TipoRecorrencia.Mensal, CategoriaId = categoriaId, MembroFamiliaId = membroId });
        await _repository.AddAsync(new Transacao { Descricao = "Fora do período", Valor = 30m, Data = marco.AddMonths(1), Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Mensal, CategoriaId = categoriaId, MembroFamiliaId = membroId });

        var elegiveis = await _repository.GetDespesasRecorrentesPorPeriodoAsync(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31, 23, 59, 59));

        Assert.Single(elegiveis);
        Assert.Equal("Assinatura", elegiveis[0].Descricao);
    }

    [Fact]
    public async Task GetOrigemIdsComCopiaNoPeriodoAsync_DeveRetornarApenasOrigensJaCopiadasNoPeriodo()
    {
        var (categoriaId, membroId) = await CriarDependenciasAsync();

        var origem = new Transacao { Descricao = "Assinatura", Valor = 30m, Data = new DateTime(2026, 3, 15), Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Mensal, CategoriaId = categoriaId, MembroFamiliaId = membroId };
        await _repository.AddAsync(origem);

        var copiaAbril = new Transacao { Descricao = "Assinatura", Valor = 30m, Data = new DateTime(2026, 4, 15), Tipo = TipoTransacao.Despesa, Recorrencia = TipoRecorrencia.Mensal, Pago = false, CategoriaId = categoriaId, MembroFamiliaId = membroId, TransacaoOrigemId = origem.Id };
        await _repository.AddAsync(copiaAbril);

        var idsAbril = await _repository.GetOrigemIdsComCopiaNoPeriodoAsync(new DateTime(2026, 4, 1), new DateTime(2026, 4, 30, 23, 59, 59));
        var idsMaio = await _repository.GetOrigemIdsComCopiaNoPeriodoAsync(new DateTime(2026, 5, 1), new DateTime(2026, 5, 31, 23, 59, 59));

        Assert.Contains(origem.Id, idsAbril);
        Assert.DoesNotContain(origem.Id, idsMaio);
    }

    [Fact]
    public async Task UpdateAsync_DevePermitirAlterarCategoriaComNavegacaoCarregada()
    {
        var (categoriaId, membroId) = await CriarDependenciasAsync();
        var outra = new Categoria { Nome = "Moradia", Icone = "🏠", CorHex = "#659ecd" };
        await _categoriaRepository.AddAsync(outra);

        var transacao = new Transacao
        {
            Descricao = "Conta",
            Valor = 100m,
            Data = DateTime.Today,
            Tipo = TipoTransacao.Despesa,
            CategoriaId = categoriaId,
            MembroFamiliaId = membroId
        };
        await _repository.AddAsync(transacao);

        var carregada = await _repository.GetByIdAsync(transacao.Id);
        Assert.NotNull(carregada);
        Assert.NotNull(carregada.Categoria);

        carregada.CategoriaId = outra.Id;
        await _repository.UpdateAsync(carregada);

        var atualizada = await _repository.GetByIdAsync(transacao.Id);
        Assert.NotNull(atualizada);
        Assert.Equal(outra.Id, atualizada.CategoriaId);
        Assert.Equal("Moradia", atualizada.Categoria?.Nome);
    }

    [Fact]
    public async Task GetByPeriodoAsync_DeveOrdenarReceitasAntesDeDespesas()
    {
        var (categoriaId, membroId) = await CriarDependenciasAsync();
        var mes = new DateTime(2026, 3, 1);

        await _repository.AddAsync(new Transacao { Descricao = "Mercado", Valor = 50m, Data = mes.AddDays(10), Tipo = TipoTransacao.Despesa, CategoriaId = categoriaId, MembroFamiliaId = membroId });
        await _repository.AddAsync(new Transacao { Descricao = "Salário", Valor = 3000m, Data = mes.AddDays(5), Tipo = TipoTransacao.Receita, CategoriaId = categoriaId, MembroFamiliaId = membroId });
        await _repository.AddAsync(new Transacao { Descricao = "Freelance", Valor = 200m, Data = mes.AddDays(15), Tipo = TipoTransacao.Receita, CategoriaId = categoriaId, MembroFamiliaId = membroId });

        var lista = await _repository.GetByPeriodoAsync(mes, mes.AddMonths(1).AddTicks(-1));

        Assert.Equal(3, lista.Count);
        Assert.All(lista.Take(2), t => Assert.Equal(TipoTransacao.Receita, t.Tipo));
        Assert.Equal(TipoTransacao.Despesa, lista[2].Tipo);
        Assert.Equal("Freelance", lista[0].Descricao);
        Assert.Equal("Salário", lista[1].Descricao);
    }

    [Fact]
    public async Task AddAsync_ItensRepetidosMesmaCategoriaCartaoEValor_PersisteDescritivoDeCadaLinha()
    {
        var (alimentacaoId, membroId) = await CriarDependenciasAsync();
        var cartaoCategoria = new Categoria { Nome = "Cartão de Crédito", Icone = "💳", CorHex = "#B2CFE6", PermiteItensFatura = true };
        var lazer = new Categoria { Nome = "Lazer", Icone = "🎉", CorHex = "#A2B94F" };
        var compras = new Categoria { Nome = "Compras", Icone = "📦", CorHex = "#A2B94F" };
        await _categoriaRepository.AddAsync(cartaoCategoria);
        await _categoriaRepository.AddAsync(lazer);
        await _categoriaRepository.AddAsync(compras);

        var cartoes = new CartaoRepository(_factory);
        var nubank = new Cartao { Nome = "Nubank", Bandeira = "Mastercard", UltimosDigitos = "1234", Tipo = TipoCartao.Credito };
        await cartoes.AdicionarAsync(nubank);

        var mes = new DateTime(2026, 10, 1);
        await _repository.AddAsync(new Transacao
        {
            Descricao = "Fatura Nubank",
            Valor = 2010.70m,
            Data = mes.AddDays(17),
            Tipo = TipoTransacao.Despesa,
            CategoriaId = cartaoCategoria.Id,
            CartaoId = nubank.Id,
            MembroFamiliaId = membroId,
            Itens =
            [
                Linha("Alimentação", "McDonald's", 40m, alimentacaoId, 0),
                Linha("Alimentação", "Big Steak", 40m, alimentacaoId, 1),
                Linha("Alimentação", "Jantar no restaurante XPTO", 40m, alimentacaoId, 2),
                Linha("Compras", "Celular da loja A", 900m, compras.Id, 3),
                Linha("Compras", "Celular da loja B", 900m, compras.Id, 4),
                Linha("Lazer", "Netflix", 55.90m, lazer.Id, 5, NaturezaItemLancamento.Assinatura),
                Linha("Lazer", "Amazon Prime", 14.90m, lazer.Id, 6, NaturezaItemLancamento.Assinatura),
                Linha("Lazer", null, 19.90m, lazer.Id, 7, NaturezaItemLancamento.Assinatura)
            ]
        });

        await _repository.AddAsync(new Transacao
        {
            Descricao = "Fatura Nubank",
            Valor = 2010.70m,
            Data = mes.AddDays(18),
            Tipo = TipoTransacao.Despesa,
            CategoriaId = cartaoCategoria.Id,
            CartaoId = nubank.Id,
            MembroFamiliaId = membroId,
            Itens = [Linha("Alimentação", "Padaria", 40m, alimentacaoId, 0)]
        });

        var lista = await _repository.GetByPeriodoAsync(mes, mes.AddMonths(1).AddTicks(-1));

        Assert.Equal(2, lista.Count);
        Assert.All(lista, t =>
        {
            Assert.Equal(nubank.Id, t.CartaoId);
            Assert.Equal(cartaoCategoria.Id, t.CategoriaId);
            Assert.Equal(2010.70m, t.Valor);
        });

        var fatura = lista.Single(t => t.Itens.Count == 8);
        var alimentacao = fatura.Itens.Where(i => i.CategoriaId == alimentacaoId).OrderBy(i => i.Ordem).ToList();
        Assert.Equal(3, alimentacao.Count);
        Assert.All(alimentacao, i => Assert.Equal(40m, i.Valor));
        Assert.Equal(["McDonald's", "Big Steak", "Jantar no restaurante XPTO"], alimentacao.Select(i => i.Descritivo));

        var assinaturas = fatura.Itens.Where(i => i.CategoriaId == lazer.Id).OrderBy(i => i.Ordem).ToList();
        Assert.Equal(["Netflix", "Amazon Prime", null], assinaturas.Select(i => i.Descritivo));

        fatura.Itens.Single(i => i.Descritivo == "Netflix").Descritivo = "Streaming 1";
        await _repository.SubstituirItensAsync(fatura.Id, fatura.Itens.OrderBy(i => i.Ordem).ToList());

        var atualizada = await _repository.GetByIdAsync(fatura.Id);
        Assert.NotNull(atualizada);
        Assert.Equal(8, atualizada.Itens.Count);
        Assert.Contains(atualizada.Itens, i => i.Descritivo == "Streaming 1" && i.CategoriaId == lazer.Id);
        Assert.Equal(3, atualizada.Itens.Count(i => i.CategoriaId == alimentacaoId && i.Valor == 40m));
    }

    private static ItemLancamento Linha(string descricao, string? descritivo, decimal valor, int categoriaId, int ordem, NaturezaItemLancamento natureza = NaturezaItemLancamento.Avulsa)
        => new()
        {
            Descricao = descricao,
            Descritivo = descritivo,
            Valor = valor,
            CategoriaId = categoriaId,
            Ordem = ordem,
            Natureza = natureza
        };

    public void Dispose() => _factory.Dispose();
}
