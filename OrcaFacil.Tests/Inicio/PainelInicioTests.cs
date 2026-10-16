using System.Globalization;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Inicio;
using OrcaFacil.Core.Noticias;

namespace OrcaFacil.Tests.Inicio;

public class PainelInicioTests
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly DateTime Hoje = new(2026, 10, 16);

    [Fact]
    public void Categorias_SomaDasFatias_IgualaOTotal_E_AgrupaOutras()
    {
        var categorias = new EntradaCategoria[]
        {
            new("Cartão de Crédito", 10723.17m),
            new("Moradia", 6935.50m),
            new("Az Sistemas", 3259.76m),
            new("Transporte", 2348.18m),
            new("Energia", 548.44m),
            new("Impostos e Taxas", 178.31m),
            new("Telefone", 153.78m),
            new("Refeição", 69.90m)
        };

        var painel = Montar(categorias: categorias);
        var somaVisiveis = painel.CategoriasVisiveis.Sum(f => f.Total);
        var somaOutras = painel.Outras!.Total;
        var total = categorias.Sum(c => c.Total);

        Assert.Equal(5, painel.CategoriasVisiveis.Count);
        Assert.Equal(3, painel.CategoriasOcultas.Count);
        Assert.True(painel.Outras.EhAgrupamento);
        Assert.Equal(3, painel.Outras.Quantidade);
        Assert.Equal(total, somaVisiveis + somaOutras);
        Assert.Equal(total, painel.TotalCategorias);
        Assert.Equal(painel.TotalCategorias, painel.FatiasBarra.Sum(f => f.Total));
        Assert.Equal(100d, painel.FatiasBarra.Sum(f => f.LarguraPilha), 2);
        Assert.Equal("Cartão de Crédito", painel.CategoriasVisiveis[0].Nome);
        Assert.Contains("Outras 3 categorias", painel.Outras.Nome);
        Assert.Contains("Cartão de Crédito", painel.RotuloAcessivelCategorias);
    }

    [Fact]
    public void Classificar_SomaIgualAoTotalDasDespesas()
    {
        var classificadas = PainelInicio.Classificar(
        [
            new LancamentoCategoria("Cartão de Crédito", 100m,
            [
                new EntradaCategoria("Streaming", 44.99m),
                new EntradaCategoria("Refeição", 30m)
            ]),
            new LancamentoCategoria("Moradia", 50m, null),
            new LancamentoCategoria("Energia", 10m, [new EntradaCategoria("Energia", 40m)])
        ]);

        Assert.Equal(160m, classificadas.Sum(c => c.Total));
        Assert.Equal(44.99m, classificadas.Single(c => c.Nome == "Streaming").Total);
        Assert.Equal(25.01m, classificadas.Single(c => c.Nome == "Cartão de Crédito").Total);
        Assert.Equal(10m, classificadas.Single(c => c.Nome == "Energia").Total);
    }

    [Fact]
    public void Categorias_ComCincoOuMenos_NaoCriaOutras()
    {
        var painel = Montar(categorias: [new("Energia", 10m), new("Moradia", 5m)]);

        Assert.Null(painel.Outras);
        Assert.Empty(painel.CategoriasOcultas);
        Assert.Equal(15m, painel.CategoriasVisiveis.Sum(f => f.Total));
    }

    [Fact]
    public void Orcamento_NoLimite_Acima_E_AbaixoDoLimiar()
    {
        var alertas = new AlertaOrcamentoDto[]
        {
            new() { CategoriaNome = "Exato", ValorLimite = 100m, ValorGasto = 100m },
            new() { CategoriaNome = "Energia", ValorLimite = 100m, ValorGasto = 109m },
            new() { CategoriaNome = "Folgado", ValorLimite = 100m, ValorGasto = 89m },
            new() { CategoriaNome = "Moradia", ValorLimite = 1000m, ValorGasto = 990m }
        };

        var painel = Montar(alertas: alertas);

        Assert.Equal(3, painel.OrcamentosNoLimite);
        Assert.Equal("3 no limite", painel.TextoContagemOrcamentos);
        Assert.DoesNotContain(painel.Orcamentos, o => o.Nome == "Folgado");

        var exato = painel.Orcamentos.Single(o => o.Nome == "Exato");
        Assert.False(exato.Estourou);
        Assert.Equal("100%", exato.TextoChip);
        Assert.Equal(100d, exato.Progresso);
        Assert.Contains("restam", exato.TextoDetalhe);
        Assert.DoesNotContain("estourou", exato.TextoChip);

        var energia = painel.Orcamentos.Single(o => o.Nome == "Energia");
        Assert.True(energia.Estourou);
        Assert.Equal("109% · estourou", energia.TextoChip);
        Assert.Equal(100d, energia.Progresso);
        Assert.Contains("acima", energia.TextoDetalhe);
        Assert.StartsWith("Energia estourou (109%)", painel.ResumoAlerta);

        var vazio = Montar(alertas: [new AlertaOrcamentoDto { CategoriaNome = "Ok", ValorLimite = 100m, ValorGasto = 50m }]);
        Assert.Equal(0, vazio.OrcamentosNoLimite);
        Assert.False(vazio.ExibirAlertaOrcamento);
    }

    [Fact]
    public void Periodo_SemReceitas_MostraTraco()
    {
        var painel = Montar(mes: new ResumoMensalDto { TotalReceitas = 0m, TotalDespesas = 40m, TotalDespesasPagas = 10m });

        Assert.Equal(PainelInicio.Traco, painel.TextoReceitaComprometida);
        Assert.Null(painel.PercentualReceitaComprometida);
        Assert.Equal(0d, painel.ProgressoReceita);
        Assert.Equal("R$ 0,00", painel.TextoReceitas);
        Assert.Equal("− R$ 40,00", painel.TextoSaldo);
    }

    [Fact]
    public void DesdeOInicio_EscondePagamento_E_UsaAcumulado()
    {
        var painel = Montar(
            periodo: PeriodoResumo.DesdeOInicio,
            mes: new ResumoMensalDto { TotalReceitas = 10m, TotalDespesas = 4m, TotalDespesasPagas = 1m },
            acumulado: new ResumoAcumuladoDto { TotalReceitas = 100m, TotalDespesas = 40m });

        Assert.False(painel.ExibirPagamento);
        Assert.Equal("Saldo acumulado", painel.RotuloSaldo);
        Assert.Equal(100m, painel.Receitas);
        Assert.Equal(40m, painel.Despesas);
        Assert.Equal(60m, painel.Saldo);
        Assert.Null(painel.PercentualDespesasPagas);
    }

    [Fact]
    public void MesesSemDados_SaoOmitidos_E_ALacunaViraNota()
    {
        var tendencia = new TendenciaMensalDto[]
        {
            Mes(2026, 5, 0, 0),
            Mes(2026, 6, 0, 0),
            Mes(2026, 7, 0, 0),
            Mes(2026, 8, 30000m, 25000m),
            Mes(2026, 9, 0, 0),
            Mes(2026, 10, 30263.36m, 24489.02m)
        };

        var painel = Montar(tendencia: tendencia);

        Assert.Equal(["ago/26", "out/26"], painel.Meses.Select(m => m.Rotulo).ToArray());
        Assert.DoesNotContain(painel.Meses, m => m.Mes is 5 or 6 or 7 or 9);
        Assert.Equal("Sem lançamentos de mai/26 a jul/26 · Sem lançamentos em set/26", painel.NotaLacuna);
        Assert.True(painel.DestacarUltimoRotulo);
        Assert.True(painel.Meses[^1].Atual);
        Assert.True(painel.PassoEixoY >= 1);
        Assert.InRange(Math.Ceiling((double)painel.Meses.Max(m => Math.Max(m.Receitas, m.Despesas)) / painel.PassoEixoY), 1, 4);
    }

    [Fact]
    public void MetaCompacta_EscolheAAbertaMaisProxima()
    {
        var metas = new Meta[]
        {
            new() { Id = 1, Nome = "Reserva", ValorAlvo = 100m, ValorAtual = 100m, Concluida = true },
            new() { Id = 2, Nome = "Notebook", ValorAlvo = 1000m, ValorAtual = 559m, Concluida = false }
        };

        var painel = Montar(metas: metas);

        Assert.Equal(2, painel.Metas.Count);
        Assert.Equal("Notebook", painel.MetaDestaque!.Nome);
        Assert.Equal("55,9%", painel.MetaDestaque.TextoPercentual);
        Assert.Equal("Reserva", painel.Metas[0].Nome);
    }

    [Fact]
    public void TempoRelativo_UsaOInstanteInformado()
    {
        var agora = new DateTimeOffset(2026, 10, 16, 22, 36, 0, TimeSpan.FromHours(-3));
        var noticia = new Noticia("Manchete", "https://exemplo", "Exame", agora.AddMinutes(-12));

        var linhas = PainelInicio.FormatarNoticias([noticia], agora);

        Assert.Equal("Exame", linhas[0].Veiculo);
        Assert.Equal("há 12 min", linhas[0].Tempo);
        Assert.Equal("0", PainelInicio.FormatarMarca(0, PtBr));
        Assert.Equal("10k", PainelInicio.FormatarMarca(10000, PtBr));
        Assert.Equal("20k", PainelInicio.FormatarMarca(20000, PtBr));
    }

    private static PainelInicio Montar(
        PeriodoResumo periodo = PeriodoResumo.EsteMes,
        ResumoMensalDto? mes = null,
        ResumoAcumuladoDto? acumulado = null,
        IReadOnlyList<EntradaCategoria>? categorias = null,
        IReadOnlyList<TendenciaMensalDto>? tendencia = null,
        IReadOnlyList<AlertaOrcamentoDto>? alertas = null,
        IReadOnlyList<Meta>? metas = null)
    {
        return PainelInicio.Montar(new EntradaPainelInicio(
            periodo,
            mes ?? new ResumoMensalDto { TotalReceitas = 30263.36m, TotalDespesas = 24489.02m, TotalDespesasPagas = 24089.02m },
            acumulado ?? new ResumoAcumuladoDto { TotalReceitas = 96726.38m, TotalDespesas = 75178.83m },
            categorias ?? [],
            tendencia ?? [],
            alertas ?? [],
            metas ?? [],
            Hoje), PtBr);
    }

    private static TendenciaMensalDto Mes(int ano, int mes, decimal receitas, decimal despesas) => new()
    {
        Ano = ano,
        Mes = mes,
        TotalReceitas = receitas,
        TotalDespesas = despesas
    };
}
