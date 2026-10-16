using System.Globalization;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Lancamentos;

namespace OrcaFacil.Tests.Lancamentos;

public class PainelLancamentosMesTests
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly DateTime Hoje = new(2026, 10, 16);

    [Fact]
    public void Sobra_Positiva_E_PercentualComUmaCasa()
    {
        var painel = Montar(new DateTime(2026, 10, 1),
            Lancamento(1, new DateTime(2026, 10, 2), 100m, TipoTransacao.Receita),
            Lancamento(2, new DateTime(2026, 10, 3), 40.15m, TipoTransacao.Despesa, pago: true));

        Assert.Equal(59.85m, painel.Sobra);
        Assert.Equal(40.2m, painel.PercentualReceitaComprometida);
        Assert.Equal(100m, painel.PercentualDespesasPagas);
        Assert.Equal("40,2% da receita comprometida", painel.TextoReceitaComprometida);
        Assert.False(painel.ReceitaComprometidaCritica);
        Assert.Equal(40.2d, painel.ProgressoReceitaComprometida);
    }

    [Fact]
    public void Sobra_Negativa_EncheABarraEmAlerta()
    {
        var painel = Montar(new DateTime(2026, 10, 1),
            Lancamento(1, new DateTime(2026, 10, 2), 100m, TipoTransacao.Receita),
            Lancamento(2, new DateTime(2026, 10, 3), 150m, TipoTransacao.Despesa));

        Assert.Equal(-50m, painel.Sobra);
        Assert.Equal("− R$ 50,00", painel.TextoSobra);
        Assert.Equal(150m, painel.PercentualReceitaComprometida);
        Assert.True(painel.ReceitaComprometidaCritica);
        Assert.Equal(100d, painel.ProgressoReceitaComprometida);
        Assert.Equal(150m, painel.APagar);
    }

    [Fact]
    public void ReceitaZero_NaoDivideEMostraTraco()
    {
        var painel = Montar(new DateTime(2026, 10, 1),
            Lancamento(1, new DateTime(2026, 10, 4), 10m, TipoTransacao.Despesa));

        Assert.Null(painel.PercentualReceitaComprometida);
        Assert.Equal(PainelLancamentosMes.Traco, painel.TextoReceitaComprometida);
        Assert.Equal(0d, painel.ProgressoReceitaComprometida);
        Assert.Equal(-10m, painel.Sobra);
    }

    [Fact]
    public void MesSemLancamentos_SeriesDoTamanhoDoMesEListaVazia()
    {
        var painel = Montar(new DateTime(2026, 10, 1));

        Assert.True(painel.MesSemLancamentos);
        Assert.Empty(painel.Grupos);
        Assert.Equal(0m, painel.Receitas);
        Assert.Equal(0m, painel.Despesas);
        Assert.Equal(31, painel.ReceitasRealizadas.Count);
        Assert.All(painel.SaldosRealizados, valor => Assert.Equal(0m, valor));
        Assert.Equal("1", painel.RotulosEixoX[0]);
        Assert.Equal("31", painel.RotulosEixoX[30]);
        Assert.Null(painel.PercentualDespesasPagas);
        Assert.Equal(PainelLancamentosMes.Traco, painel.TextoDespesasPagas);
    }

    [Fact]
    public void MesAtual_SeparaRealizadoEPrevistoNoDiaDeHoje()
    {
        var painel = Montar(new DateTime(2026, 10, 1),
            Lancamento(1, new DateTime(2026, 10, 10), 20m, TipoTransacao.Despesa, pago: true),
            Lancamento(2, new DateTime(2026, 10, 16), 5m, TipoTransacao.Receita),
            Lancamento(3, new DateTime(2026, 10, 20), 8m, TipoTransacao.Despesa));

        Assert.True(painel.ExibirMarcadorHoje);
        Assert.True(painel.ExibirPrevisto);
        Assert.Equal(15, painel.IndiceHoje);
        Assert.Equal(5m, painel.ReceitasRealizadas[15]);
        Assert.Equal(5m, painel.ReceitasPrevistas[15]);
        Assert.Equal(20m, painel.DespesasRealizadas[15]);
        Assert.Equal(20m, painel.DespesasPrevistas[15]);
        Assert.Equal(20m, painel.DespesasRealizadas[30]);
        Assert.Equal(28m, painel.DespesasPrevistas[30]);
        Assert.Equal(-15m, painel.SaldosRealizados[30]);
        Assert.Equal(-23m, painel.SaldosPrevistos[30]);
    }

    [Fact]
    public void MesPassado_NaoTemMarcadorHojeNemPrevisto()
    {
        var painel = Montar(new DateTime(2026, 9, 1),
            Lancamento(1, new DateTime(2026, 9, 30), 12m, TipoTransacao.Despesa, pago: true));

        Assert.False(painel.ExibirMarcadorHoje);
        Assert.False(painel.ExibirPrevisto);
        Assert.Null(painel.IndiceHoje);
        Assert.Null(painel.FracaoMarcadorHoje);
        Assert.Equal(12m, painel.DespesasRealizadas[29]);
        Assert.Equal(30, painel.RotulosEixoX.Count);
    }

    [Fact]
    public void LancamentoNoUltimoDia_EntraNoAcumuladoENoRotulo()
    {
        var painel = Montar(new DateTime(2026, 10, 1),
            Lancamento(1, new DateTime(2026, 10, 1), 3m, TipoTransacao.Receita),
            Lancamento(2, new DateTime(2026, 10, 31, 23, 0, 0), 7m, TipoTransacao.Receita));

        Assert.Equal(31, painel.ReceitasPrevistas.Count);
        Assert.Equal(3m, painel.ReceitasRealizadas[30]);
        Assert.Equal(10m, painel.ReceitasPrevistas[30]);
        Assert.Equal("31", painel.RotulosEixoX[30]);
        Assert.Equal("", painel.RotulosEixoX[29]);
        Assert.Equal("25", painel.RotulosEixoX[24]);
        Assert.Equal(10m, painel.Receitas);
    }

    [Fact]
    public void AgrupaPorData_MaisRecentePrimeiro_ComTotalDoDia()
    {
        var painel = Montar(new DateTime(2026, 10, 1),
            Lancamento(1, new DateTime(2026, 10, 21, 8, 0, 0), 100m, TipoTransacao.Despesa, "DAS"),
            Lancamento(2, new DateTime(2026, 10, 21, 18, 0, 0), 4300m, TipoTransacao.Despesa, "Cartão Itaú"),
            Lancamento(3, new DateTime(2026, 10, 20), 50m, TipoTransacao.Receita, "Salário"),
            Lancamento(4, new DateTime(2026, 10, 20), 10m, TipoTransacao.Despesa, "Telefone"));

        Assert.Equal(2, painel.Grupos.Count);
        Assert.Equal(new DateTime(2026, 10, 21), painel.Grupos[0].Data);
        Assert.Equal("QUA, 21 OUT", painel.Grupos[0].Cabecalho);
        Assert.Equal(-4400m, painel.Grupos[0].TotalLiquido);
        Assert.Equal("− R$ 4.400,00", painel.Grupos[0].TextoTotal);
        Assert.Equal("Cartão Itaú", painel.Grupos[0].Linhas[0].Titulo);
        Assert.Equal("− R$ 4.300,00", painel.Grupos[0].Linhas[0].TextoValor);
        Assert.Equal("A pagar", painel.Grupos[0].Linhas[0].TextoStatus);
        Assert.Equal(new DateTime(2026, 10, 20), painel.Grupos[1].Data);
        Assert.Equal(40m, painel.Grupos[1].TotalLiquido);
        Assert.Equal("+ R$ 40,00", painel.Grupos[1].TextoTotal);
        var receita = Assert.Single(painel.Grupos[1].Linhas, l => l.Tipo == TipoTransacao.Receita);
        Assert.Equal("+ R$ 50,00", receita.TextoValor);
        Assert.Equal("✓ Pago", receita.TextoStatus);
    }

    [Fact]
    public void Filtros_NaoAlteramOResumoDoMes()
    {
        var cartao = Categoria(9, "Cartão de Crédito", "💳");
        var mercado = Categoria(3, "Mercado", "🛒");
        var lancamentos = new[]
        {
            Lancamento(1, new DateTime(2026, 10, 21), 4300m, TipoTransacao.Despesa, "Cartão Itaú", cartao, pago: false,
                itens: [new ItemLancamento { Valor = 211.88m }]),
            Lancamento(2, new DateTime(2026, 10, 2), 1000m, TipoTransacao.Receita, "Salário", mercado)
        };

        var completo = PainelLancamentosMes.Montar(
            lancamentos, new DateTime(2026, 10, 1), Hoje, FiltroListaLancamento.Todos, null, null, PtBr);
        var filtrado = PainelLancamentosMes.Montar(
            lancamentos, new DateTime(2026, 10, 1), Hoje, FiltroListaLancamento.APagar, new[] { 9 }, "itaú", PtBr);

        Assert.Equal(completo.Receitas, filtrado.Receitas);
        Assert.Equal(completo.Despesas, filtrado.Despesas);
        Assert.Equal(completo.Sobra, filtrado.Sobra);
        Assert.Equal(1, filtrado.QuantidadeFiltrada);
        var linha = Assert.Single(Assert.Single(filtrado.Grupos).Linhas);
        Assert.True(linha.EhFaturaCartao);
        Assert.Equal("Cartão de crédito · 1 item", linha.Subtitulo);
        Assert.Equal("R$ 211,88 classificados", linha.Classificados);
        Assert.True(linha.PodeAlternarPago);
    }

    [Fact]
    public void FaturaClassificadaPorCompleto_NaoMostraALinhaDeAviso()
    {
        var cartao = Categoria(9, "Cartão de Crédito", "💳");
        var painel = Montar(new DateTime(2026, 10, 1),
            Lancamento(1, new DateTime(2026, 10, 4), 10m, TipoTransacao.Despesa, "Fatura", cartao,
                itens: [new ItemLancamento { Valor = 10m }, new ItemLancamento { Valor = 0m }]));

        var linha = Assert.Single(Assert.Single(painel.Grupos).Linhas);
        Assert.Equal("Cartão de crédito · 2 itens", linha.Subtitulo);
        Assert.Null(linha.Classificados);
    }

    private static PainelLancamentosMes Montar(DateTime mes, params Transacao[] lancamentos)
        => PainelLancamentosMes.Montar(lancamentos, mes, Hoje, FiltroListaLancamento.Todos, null, null, PtBr);

    private static Transacao Lancamento(
        int id,
        DateTime data,
        decimal valor,
        TipoTransacao tipo,
        string descricao = "Lançamento",
        Categoria? categoria = null,
        bool pago = false,
        ItemLancamento[]? itens = null)
        => new()
        {
            Id = id,
            Data = data,
            Valor = valor,
            Tipo = tipo,
            Descricao = descricao,
            Pago = pago,
            CategoriaId = categoria?.Id ?? 1,
            Categoria = categoria ?? Categoria(1, "Geral", "🏷️"),
            Itens = itens ?? []
        };

    private static Categoria Categoria(int id, string nome, string icone)
        => new() { Id = id, Nome = nome, Icone = icone, CorHex = "#6EA8FE" };
}
