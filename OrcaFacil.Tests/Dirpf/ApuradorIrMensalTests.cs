using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.Dirpf;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Tests.Dirpf;

public class ApuradorIrMensalTests
{
    [Fact]
    public void VendaDeAcoesAteOLimite_FicaIsenta_ENaoGeraDarf()
    {
        var apuracao = Apurar(
            Compra(1, new DateTime(2024, 1, 2), 10, 10m),
            Venda(2, new DateTime(2024, 1, 20), 10, 300m, 3_000m));

        var comum = Linha(apuracao, 1, CestaApuracaoIr.Comum);
        Assert.Equal(0m, comum.Resultado);
        Assert.Equal(0m, comum.Imposto);
        Assert.Equal(3_000m, comum.VendasAcoesComuns);
        Assert.Equal(2_900m, comum.GanhoIsentoAcoes);
        Assert.Equal(0m, comum.Imposto);
        Assert.Equal(0m, Linha(apuracao, 1, CestaApuracaoIr.DayTrade).Resultado);
        Assert.Equal(2_900m, Assert.Single(apuracao.GanhosIsentos).Valor);
        Assert.Equal(0m, Darf(apuracao, 1).Valor);
        Assert.Null(Darf(apuracao, 1).Vencimento);
    }

    [Fact]
    public void VendaAcimaDoLimite_UsaAAliquotaDaTabela_ECodigoDoDarf()
    {
        var apuracao = Apurar(
            Compra(1, new DateTime(2024, 1, 2), 100, 100m),
            Venda(2, new DateTime(2024, 1, 31), 100, 250m, 25_000m));

        var comum = Linha(apuracao, 1, CestaApuracaoIr.Comum);
        Assert.Equal(15_000m, comum.Resultado);
        Assert.Equal(15_000m, comum.BaseCalculo);
        Assert.Equal(0.15m, comum.Aliquota);
        Assert.Equal(2_250m, comum.Imposto);
        Assert.Equal(0m, comum.GanhoIsentoAcoes);
        Assert.Empty(apuracao.GanhosIsentos);

        var darf = Darf(apuracao, 1);
        Assert.Equal(6015, darf.CodigoReceita);
        Assert.Equal(2_250m, darf.Valor);
        Assert.Equal(new DateTime(2024, 2, 29), darf.Vencimento);
        Assert.Equal(0m, darf.SaldoAcumulado);
    }

    [Fact]
    public void LimiteExatoFicaIsento_EUmCentavoAcimaTributaTudo()
    {
        var noLimite = Apurar(
            Compra(1, new DateTime(2024, 3, 1), 1, 1m),
            Venda(2, new DateTime(2024, 3, 10), 1, 20_000m, 20_000m));
        Assert.Equal(19_999m, Linha(noLimite, 3, CestaApuracaoIr.Comum).GanhoIsentoAcoes);
        Assert.Equal(0m, Linha(noLimite, 3, CestaApuracaoIr.Comum).Imposto);

        var acima = Apurar(
            Compra(1, new DateTime(2024, 3, 1), 1, 1m),
            Venda(2, new DateTime(2024, 3, 10), 1, 20_000.01m, 20_000.01m));
        var comum = Linha(acima, 3, CestaApuracaoIr.Comum);
        Assert.Equal(0m, comum.GanhoIsentoAcoes);
        Assert.Equal(20_000.01m - 1m, comum.BaseCalculo);
        Assert.Equal(CalculadoraResultadoRealizado.Arredondar((20_000.01m - 1m) * 0.15m, 2), comum.Imposto);
    }

    [Fact]
    public void DayTrade_NaoUsaAIsencao_EAAliquotaVemDaTabela()
    {
        var apuracao = Apurar(
            Compra(1, new DateTime(2024, 4, 8), 10, 10m, corretora: "XP"),
            Venda(2, new DateTime(2024, 4, 8), 10, 30m, 300m, corretora: "XP"));

        var day = Linha(apuracao, 4, CestaApuracaoIr.DayTrade);
        Assert.Equal(200m, day.Resultado);
        Assert.Equal(0.20m, day.Aliquota);
        Assert.Equal(40m, day.Imposto);
        Assert.Equal(0m, Linha(apuracao, 4, CestaApuracaoIr.Comum).VendasAcoesComuns);
        Assert.Equal(0m, Linha(apuracao, 4, CestaApuracaoIr.Comum).GanhoIsentoAcoes);
        Assert.Equal(40m, Darf(apuracao, 4).Valor);

        var regras = Regras();
        regras.Single(regra => regra.Codigo == CodigosRegraFiscal.AliquotaDayTrade).Valor = 0.10m;
        var reduzida = ApuradorIrMensal.Apurar(
        [
            Compra(1, new DateTime(2024, 4, 8), 10, 10m, corretora: "XP"),
            Venda(2, new DateTime(2024, 4, 8), 10, 30m, 300m, corretora: "XP")
        ], regras, 2024);
        Assert.Equal(20m, Linha(reduzida, 4, CestaApuracaoIr.DayTrade).Imposto);
    }

    [Fact]
    public void DayTradeParcial_SeparaOResultadoDaCalculadora_ESoAParteComumEntraNaIsencao()
    {
        var operacoes = new[]
        {
            Compra(1, new DateTime(2024, 5, 2), 60, 10m),
            Compra(2, new DateTime(2024, 5, 10), 40, 12m, corretora: "XP"),
            Venda(3, new DateTime(2024, 5, 10), 100, 20m, 2_000m, corretora: "XP")
        };
        var calculado = CalculadoraResultadoRealizado.Calcular(
            operacoes.Select(ParaMovimento),
            _ => new RegraResultado(true, 2, 0.15m));
        Assert.Equal(920m, Assert.Single(calculado.Vendas).Resultado);

        var apuracao = Apurar(operacoes);
        Assert.Equal(368m, Linha(apuracao, 5, CestaApuracaoIr.DayTrade).Resultado);
        Assert.Equal(73.60m, Linha(apuracao, 5, CestaApuracaoIr.DayTrade).Imposto);
        var comum = Linha(apuracao, 5, CestaApuracaoIr.Comum);
        Assert.Equal(552m, comum.GanhoIsentoAcoes);
        Assert.Equal(1_200m, comum.VendasAcoesComuns);
        Assert.Equal(0m, comum.Imposto);
    }

    [Fact]
    public void PrejuizoCompensaGanhoPosteriorNaMesmaCesta()
    {
        var apuracao = Apurar(
            Compra(1, new DateTime(2024, 3, 1), 100, 260m),
            Venda(2, new DateTime(2024, 3, 20), 100, 250m, 25_000m),
            Compra(3, new DateTime(2024, 5, 1), 100, 245m),
            Venda(4, new DateTime(2024, 5, 20), 100, 250m, 25_000m),
            Compra(5, new DateTime(2024, 7, 1), 100, 240m),
            Venda(6, new DateTime(2024, 7, 20), 100, 250m, 25_000m));

        var marco = Linha(apuracao, 3, CestaApuracaoIr.Comum);
        Assert.Equal(-1_000m, marco.Resultado);
        Assert.Equal(1_000m, marco.PrejuizoACompensar);
        Assert.Equal(0m, marco.Imposto);

        var maio = Linha(apuracao, 5, CestaApuracaoIr.Comum);
        Assert.Equal(500m, maio.Resultado);
        Assert.Equal(500m, maio.PrejuizoCompensado);
        Assert.Equal(500m, maio.PrejuizoACompensar);
        Assert.Equal(0m, maio.BaseCalculo);

        var julho = Linha(apuracao, 7, CestaApuracaoIr.Comum);
        Assert.Equal(1_000m, julho.Resultado);
        Assert.Equal(500m, julho.PrejuizoCompensado);
        Assert.Equal(500m, julho.BaseCalculo);
        Assert.Equal(75m, julho.Imposto);
        Assert.Equal(0m, julho.PrejuizoACompensar);
    }

    [Fact]
    public void PrejuizoCruzaOAno_ENaoCruzaQuandoARegraDesliga()
    {
        var operacoes = new[]
        {
            Compra(1, new DateTime(2024, 12, 2), 100, 254m),
            Venda(2, new DateTime(2024, 12, 20), 100, 250m, 25_000m),
            Compra(3, new DateTime(2025, 1, 6), 100, 246m),
            Venda(4, new DateTime(2025, 1, 20), 100, 250m, 25_000m)
        };

        var padrao = Apurar(2025, operacoes);
        var janeiro = Linha(padrao, 1, CestaApuracaoIr.Comum);
        Assert.Equal(400m, janeiro.PrejuizoCompensado);
        Assert.Equal(0m, janeiro.BaseCalculo);
        Assert.Equal(0m, janeiro.Imposto);

        var regras = Regras();
        regras.Single(regra => regra.Codigo == CodigosRegraFiscal.PrejuizoCompensaEntreAnos).Valor = 0m;
        var semCruzar = ApuradorIrMensal.Apurar(operacoes, regras, 2025);
        var janeiroZerado = Linha(semCruzar, 1, CestaApuracaoIr.Comum);
        Assert.Equal(400m, janeiroZerado.BaseCalculo);
        Assert.Equal(60m, janeiroZerado.Imposto);
        Assert.Equal(0m, janeiroZerado.PrejuizoCompensado);
    }

    [Fact]
    public void PrejuizoNaoCruzaCestaNaViradaDoAno()
    {
        var operacoes = new[]
        {
            Compra(1, new DateTime(2024, 12, 2), 100, 254m),
            Venda(2, new DateTime(2024, 12, 20), 100, 250m, 25_000m),
            Compra(3, new DateTime(2024, 12, 2), 10, 20m, TipoAtivo.Fii, ativoId: 2),
            Venda(4, new DateTime(2024, 12, 20), 10, 10m, 100m, TipoAtivo.Fii, ativoId: 2),
            Compra(5, new DateTime(2024, 12, 10), 10, 30m, ativoId: 3, corretora: "XP"),
            Venda(6, new DateTime(2024, 12, 10), 10, 10m, 100m, ativoId: 3, corretora: "XP"),
            Compra(7, new DateTime(2025, 1, 6), 100, 240m),
            Venda(8, new DateTime(2025, 1, 20), 100, 250m, 25_000m),
            Compra(9, new DateTime(2025, 1, 6), 10, 10m, TipoAtivo.Fii, ativoId: 2),
            Venda(10, new DateTime(2025, 1, 20), 10, 28m, 280m, TipoAtivo.Fii, ativoId: 2),
            Compra(11, new DateTime(2025, 1, 8), 10, 10m, ativoId: 3, corretora: "XP"),
            Venda(12, new DateTime(2025, 1, 8), 10, 60m, 600m, ativoId: 3, corretora: "XP")
        };

        var dezembro = Apurar(2024, operacoes);
        Assert.Equal(400m, Linha(dezembro, 12, CestaApuracaoIr.Comum).PrejuizoACompensar);
        Assert.Equal(100m, Linha(dezembro, 12, CestaApuracaoIr.Fii).PrejuizoACompensar);
        Assert.Equal(200m, Linha(dezembro, 12, CestaApuracaoIr.DayTrade).PrejuizoACompensar);

        var janeiro = Apurar(2025, operacoes);
        var acoes = Linha(janeiro, 1, CestaApuracaoIr.Comum);
        Assert.Equal(1_000m, acoes.Resultado);
        Assert.Equal(400m, acoes.PrejuizoCompensado);
        Assert.Equal(600m, acoes.BaseCalculo);
        Assert.Equal(90m, acoes.Imposto);

        var fii = Linha(janeiro, 1, CestaApuracaoIr.Fii);
        Assert.Equal(180m, fii.Resultado);
        Assert.Equal(100m, fii.PrejuizoCompensado);
        Assert.Equal(80m, fii.BaseCalculo);
        Assert.Equal(16m, fii.Imposto);

        var day = Linha(janeiro, 1, CestaApuracaoIr.DayTrade);
        Assert.Equal(500m, day.Resultado);
        Assert.Equal(200m, day.PrejuizoCompensado);
        Assert.Equal(300m, day.BaseCalculo);
        Assert.Equal(60m, day.Imposto);
    }

    [Fact]
    public void PrejuizoDeMesIsentoCompensaGanhoPosterior_EParaQuandoARegraDesliga()
    {
        var operacoes = new[]
        {
            Compra(1, new DateTime(2024, 1, 2), 10, 20m),
            Venda(2, new DateTime(2024, 1, 20), 10, 10m, 100m),
            Compra(3, new DateTime(2024, 3, 1), 10, 100m),
            Venda(4, new DateTime(2024, 3, 20), 10, 2_500m, 25_000m)
        };

        var padrao = Apurar(operacoes);
        Assert.Equal(100m, Linha(padrao, 1, CestaApuracaoIr.Comum).PrejuizoACompensar);
        Assert.Equal(23_900m, Linha(padrao, 3, CestaApuracaoIr.Comum).BaseCalculo);
        Assert.Equal(3_585m, Linha(padrao, 3, CestaApuracaoIr.Comum).Imposto);

        var regras = Regras();
        regras.Single(regra => regra.Codigo == CodigosRegraFiscal.PrejuizoCompensaNoMesIsento).Valor = 0m;
        var desligado = ApuradorIrMensal.Apurar(operacoes, regras, 2024);
        Assert.Equal(0m, Linha(desligado, 1, CestaApuracaoIr.Comum).PrejuizoACompensar);
        Assert.Equal(24_000m, Linha(desligado, 3, CestaApuracaoIr.Comum).BaseCalculo);
        Assert.Equal(3_600m, Linha(desligado, 3, CestaApuracaoIr.Comum).Imposto);
    }

    [Fact]
    public void FiiEFiagro_CompensamNaMesmaCesta_EDayTradeDeFiiNaoSaiDela()
    {
        var apuracao = Apurar(
            Compra(1, new DateTime(2024, 1, 8), 10, 10m, TipoAtivo.Fii),
            Venda(2, new DateTime(2024, 1, 8), 10, 5m, 50m, TipoAtivo.Fii),
            Compra(3, new DateTime(2024, 3, 4), 10, 10m, TipoAtivo.Fii, ativoId: 2),
            Venda(4, new DateTime(2024, 3, 18), 10, 18m, 180m, TipoAtivo.Fii, ativoId: 2));

        var janeiro = Linha(apuracao, 1, CestaApuracaoIr.Fii);
        Assert.Equal(-50m, janeiro.Resultado);
        Assert.Equal(0m, Linha(apuracao, 1, CestaApuracaoIr.DayTrade).Resultado);
        Assert.Equal(0.20m, janeiro.Aliquota);

        var marco = Linha(apuracao, 3, CestaApuracaoIr.Fii);
        Assert.Equal(80m, marco.Resultado);
        Assert.Equal(50m, marco.PrejuizoCompensado);
        Assert.Equal(30m, marco.BaseCalculo);
        Assert.Equal(6m, marco.Imposto);
        Assert.Equal(0m, marco.GanhoIsentoAcoes);
        Assert.Equal(0m, Darf(apuracao, 3).Valor);
        Assert.Equal(6m, Darf(apuracao, 3).SaldoAcumulado);
    }

    [Fact]
    public void EtfEBdrNaoTemIsencao_EDayTradeDeEtfUsaAAliquotaDeDayTrade()
    {
        var bdr = Apurar(
            Compra(1, new DateTime(2024, 2, 1), 1, 80m, TipoAtivo.Bdr),
            Venda(2, new DateTime(2024, 2, 20), 1, 180m, 180m, TipoAtivo.Bdr));
        var comum = Linha(bdr, 2, CestaApuracaoIr.Comum);
        Assert.Equal(100m, comum.BaseCalculo);
        Assert.Equal(15m, comum.Imposto);
        Assert.Equal(0m, comum.VendasAcoesComuns);

        var etf = Apurar(
            Compra(1, new DateTime(2024, 2, 5), 1, 100m, TipoAtivo.Etf, corretora: "Clear"),
            Venda(2, new DateTime(2024, 2, 5), 1, 150m, 150m, TipoAtivo.Etf, corretora: "Clear"));
        Assert.Equal(50m, Linha(etf, 2, CestaApuracaoIr.DayTrade).Resultado);
        Assert.Equal(10m, Linha(etf, 2, CestaApuracaoIr.DayTrade).Imposto);
        Assert.Equal(0m, Linha(etf, 2, CestaApuracaoIr.Comum).Resultado);
    }

    [Fact]
    public void DarfAbaixoDoMinimo_AcumulaParaOMesSeguinte_EPagaQuandoARegraDesliga()
    {
        var operacoes = new[]
        {
            Compra(1, new DateTime(2024, 1, 2), 1, 100m, TipoAtivo.Etf),
            Venda(2, new DateTime(2024, 1, 20), 1, 150m, 150m, TipoAtivo.Etf),
            Compra(3, new DateTime(2024, 2, 4), 1, 100m, TipoAtivo.Etf),
            Venda(4, new DateTime(2024, 2, 18), 1, 120m, 120m, TipoAtivo.Etf)
        };

        var apuracao = Apurar(operacoes);
        Assert.Equal(7.50m, Linha(apuracao, 1, CestaApuracaoIr.Comum).Imposto);
        Assert.Equal(0m, Darf(apuracao, 1).Valor);
        Assert.Equal(7.50m, Darf(apuracao, 1).SaldoAcumulado);
        Assert.Equal(3m, Linha(apuracao, 2, CestaApuracaoIr.Comum).Imposto);
        Assert.Equal(7.50m, Darf(apuracao, 2).AcumuladoAnterior);
        Assert.Equal(10.50m, Darf(apuracao, 2).Valor);
        Assert.Equal(new DateTime(2024, 3, 28), Darf(apuracao, 2).Vencimento);
        Assert.Equal(0m, Darf(apuracao, 2).SaldoAcumulado);

        var regras = Regras();
        regras.Single(regra => regra.Codigo == CodigosRegraFiscal.DarfAcumularAbaixoDoMinimo).Valor = 0m;
        var imediato = ApuradorIrMensal.Apurar(operacoes, regras, 2024);
        Assert.Equal(7.50m, Darf(imediato, 1).Valor);
        Assert.Equal(3m, Darf(imediato, 2).Valor);
    }

    [Fact]
    public void SaldoDeDarfAbaixoDoMinimo_CruzaOAno()
    {
        var apuracao = Apurar(2025,
            Compra(1, new DateTime(2024, 12, 2), 1, 100m, TipoAtivo.Etf),
            Venda(2, new DateTime(2024, 12, 16), 1, 150m, 150m, TipoAtivo.Etf),
            Compra(3, new DateTime(2025, 1, 6), 1, 100m, TipoAtivo.Etf),
            Venda(4, new DateTime(2025, 1, 20), 1, 120m, 120m, TipoAtivo.Etf));

        Assert.Equal(7.50m, Darf(apuracao, 1).AcumuladoAnterior);
        Assert.Equal(10.50m, Darf(apuracao, 1).Valor);
        Assert.Equal(new DateTime(2025, 2, 28), Darf(apuracao, 1).Vencimento);
    }

    [Fact]
    public void IrrfInformado_DeduzOImposto_EAEstimativaFicaDesligada()
    {
        var operacoes = new[]
        {
            Compra(1, new DateTime(2024, 6, 3), 100, 100m),
            Venda(2, new DateTime(2024, 6, 21), 100, 250m, 25_000m)
        };
        var semIrrf = Apurar(operacoes);
        Assert.Equal(0m, Linha(semIrrf, 6, CestaApuracaoIr.Comum).Irrf);
        Assert.Equal(2_250m, Darf(semIrrf, 6).Valor);

        var comIrrf = ApuradorIrMensal.Apurar(
            operacoes,
            Regras(),
            2024,
            [new IrrfManual(1, 2024, 6, CestaApuracaoIr.Comum, 3m)]);
        Assert.Equal(3m, Linha(comIrrf, 6, CestaApuracaoIr.Comum).Irrf);
        Assert.Equal(2_247m, Linha(comIrrf, 6, CestaApuracaoIr.Comum).ImpostoLiquido);
        Assert.Equal(2_247m, Darf(comIrrf, 6).Valor);
    }

    [Fact]
    public void IrrfEstimado_DeduzOQuePassaDeUmReal_ENaoRetemOMiudo()
    {
        var regras = Regras();
        regras.Single(regra => regra.Codigo == CodigosRegraFiscal.IrrfEstimar).Valor = 1m;

        var grande = ApuradorIrMensal.Apurar(
        [
            Compra(1, new DateTime(2024, 2, 1), 1, 1m),
            Venda(2, new DateTime(2024, 2, 20), 1, 60_000m, 60_000m)
        ], regras, 2024);
        var comum = Linha(grande, 2, CestaApuracaoIr.Comum);
        Assert.Equal(8_999.85m, comum.Imposto);
        Assert.Equal(3m, comum.Irrf);
        Assert.Equal(8_996.85m, comum.ImpostoLiquido);

        var miudo = ApuradorIrMensal.Apurar(
        [
            Compra(1, new DateTime(2024, 1, 2), 1, 1m),
            Venda(2, new DateTime(2024, 1, 20), 1, 3_000m, 3_000m)
        ], regras, 2024);
        Assert.Equal(0m, Linha(miudo, 1, CestaApuracaoIr.Comum).Irrf);
        Assert.Equal(2_999m, Linha(miudo, 1, CestaApuracaoIr.Comum).GanhoIsentoAcoes);

        var doisEtf = ApuradorIrMensal.Apurar(
        [
            Compra(1, new DateTime(2024, 4, 1), 1, 1m, TipoAtivo.Etf),
            Venda(2, new DateTime(2024, 4, 10), 1, 12_000m, 12_000m, TipoAtivo.Etf),
            Compra(3, new DateTime(2024, 4, 2), 1, 1m, TipoAtivo.Etf, ativoId: 2),
            Venda(4, new DateTime(2024, 4, 12), 1, 12_000m, 12_000m, TipoAtivo.Etf, ativoId: 2)
        ], regras, 2024);
        Assert.Equal(1.20m, Linha(doisEtf, 4, CestaApuracaoIr.Comum).Irrf);

        var manualVence = ApuradorIrMensal.Apurar(
        [
            Compra(1, new DateTime(2024, 2, 1), 1, 1m),
            Venda(2, new DateTime(2024, 2, 20), 1, 60_000m, 60_000m)
        ], regras, 2024, [new IrrfManual(1, 2024, 2, CestaApuracaoIr.Comum, 0m)]);
        Assert.Equal(0m, Linha(manualVence, 2, CestaApuracaoIr.Comum).Irrf);
    }

    [Fact]
    public void SobraDeIrrfNaoAbateOMesSeguinte_EDayTradeParaEmDezembroQuandoARegraLiga()
    {
        var operacoes = new[]
        {
            Compra(1, new DateTime(2024, 3, 4), 10, 10m, corretora: "XP"),
            Venda(2, new DateTime(2024, 3, 4), 10, 20m, 200m, corretora: "XP"),
            Compra(3, new DateTime(2024, 5, 6), 10, 10m, corretora: "XP"),
            Venda(4, new DateTime(2024, 5, 6), 10, 30m, 300m, corretora: "XP")
        };
        var padrao = ApuradorIrMensal.Apurar(
            operacoes,
            Regras(),
            2024,
            [new IrrfManual(1, 2024, 3, CestaApuracaoIr.DayTrade, 50m)]);
        Assert.Equal(0m, Linha(padrao, 3, CestaApuracaoIr.DayTrade).ImpostoLiquido);
        Assert.Equal(40m, Linha(padrao, 5, CestaApuracaoIr.DayTrade).ImpostoLiquido);

        var regras = Regras();
        regras.Single(regra => regra.Codigo == CodigosRegraFiscal.IrrfCompensaMesesSeguintes).Valor = 1m;
        var noAno = ApuradorIrMensal.Apurar(
            operacoes,
            regras,
            2024,
            [new IrrfManual(1, 2024, 3, CestaApuracaoIr.DayTrade, 50m)]);
        Assert.Equal(10m, Linha(noAno, 5, CestaApuracaoIr.DayTrade).ImpostoLiquido);

        var virada = new[]
        {
            Compra(1, new DateTime(2024, 12, 2), 10, 10m, corretora: "XP"),
            Venda(2, new DateTime(2024, 12, 2), 10, 20m, 200m, corretora: "XP"),
            Compra(3, new DateTime(2025, 1, 6), 10, 10m, corretora: "XP"),
            Venda(4, new DateTime(2025, 1, 6), 10, 30m, 300m, corretora: "XP")
        };
        var janeiro = ApuradorIrMensal.Apurar(
            virada,
            regras,
            2025,
            [new IrrfManual(1, 2024, 12, CestaApuracaoIr.DayTrade, 50m)]);
        Assert.Equal(40m, Linha(janeiro, 1, CestaApuracaoIr.DayTrade).ImpostoLiquido);
        Assert.Equal(0m, Linha(janeiro, 1, CestaApuracaoIr.DayTrade).Irrf);
    }

    [Fact]
    public void Arredondamento_AfastaDoZero()
    {
        var apuracao = Apurar(
            Compra(1, new DateTime(2024, 8, 1), 1, 10m, TipoAtivo.Etf),
            Venda(2, new DateTime(2024, 8, 12), 1, 16.70m, 16.70m, TipoAtivo.Etf));

        Assert.Equal(6.70m, Linha(apuracao, 8, CestaApuracaoIr.Comum).BaseCalculo);
        Assert.Equal(1.01m, Linha(apuracao, 8, CestaApuracaoIr.Comum).Imposto);
    }

    [Fact]
    public void CustoDaVenda_EQuantidadeVezesPrecoMaisCustos()
    {
        var operacoes = new[]
        {
            Compra(1, new DateTime(2024, 9, 2), 10, 10m, custos: 5m, valor: 999m),
            Venda(2, new DateTime(2024, 9, 20), 10, 20m, 30_000m, custos: 3m)
        };
        var calculado = CalculadoraResultadoRealizado.Calcular(
            operacoes.Select(ParaMovimento),
            _ => new RegraResultado(true, 2, 0.99m));
        Assert.Equal(29_892m, Assert.Single(calculado.Vendas).Resultado);

        var apuracao = Apurar(operacoes);
        var comum = Linha(apuracao, 9, CestaApuracaoIr.Comum);
        Assert.Equal(29_892m, comum.BaseCalculo);
        Assert.Equal(4_483.80m, comum.Imposto);
    }

    [Fact]
    public void LimiteEAliquota_MudamQuandoATabelaMuda()
    {
        var operacoes = new[]
        {
            Compra(1, new DateTime(2024, 4, 1), 1, 10m),
            Venda(2, new DateTime(2024, 4, 15), 1, 150m, 150m)
        };
        var isento = Apurar(operacoes);
        Assert.Equal(140m, Linha(isento, 4, CestaApuracaoIr.Comum).GanhoIsentoAcoes);
        Assert.Equal(0m, Linha(isento, 4, CestaApuracaoIr.Comum).Imposto);

        var regras = Regras();
        regras.Single(regra => regra.Codigo == CodigosRegraFiscal.IsencaoVendasAcoes).Valor = 100m;
        regras.Single(regra => regra.Codigo == CodigosRegraFiscal.AliquotaAcao).Valor = 0.10m;
        var tributado = ApuradorIrMensal.Apurar(operacoes, regras, 2024);
        var comum = Linha(tributado, 4, CestaApuracaoIr.Comum);
        Assert.Equal(0m, comum.GanhoIsentoAcoes);
        Assert.Equal(140m, comum.BaseCalculo);
        Assert.Equal(0.10m, comum.Aliquota);
        Assert.Equal(14m, comum.Imposto);
    }

    [Fact]
    public void PerfisNaoSomamOLimiteNemOPrejuizo()
    {
        var apuracao = Apurar(
            Compra(1, new DateTime(2024, 6, 3), 1, 10m, perfilId: 1, perfil: "Ana"),
            Venda(2, new DateTime(2024, 6, 18), 1, 15_000m, 15_000m, perfilId: 1, perfil: "Ana"),
            Compra(3, new DateTime(2024, 6, 3), 1, 10m, perfilId: 2, perfil: "Bob"),
            Venda(4, new DateTime(2024, 6, 18), 1, 15_000m, 15_000m, perfilId: 2, perfil: "Bob"));

        Assert.Equal(2, apuracao.GanhosIsentos.Count);
        Assert.All(apuracao.Meses.Where(linha => linha.Cesta == CestaApuracaoIr.Comum && linha.Mes == 6), linha =>
        {
            Assert.Equal(14_990m, linha.GanhoIsentoAcoes);
            Assert.Equal(0m, linha.Imposto);
        });
        Assert.Equal(2, apuracao.Darfs.Select(linha => linha.PerfilId).Distinct().Count());
    }

    [Fact]
    public void CorretoraDiferenteNaoEhDayTrade_EAMesmaIgnoraMaiuscula()
    {
        var corretoras = Apurar(
            Compra(1, new DateTime(2024, 7, 1), 10, 10m, corretora: "XP"),
            Venda(2, new DateTime(2024, 7, 8), 10, 20m, 200m, corretora: "Clear"));
        Assert.Equal(100m, Linha(corretoras, 7, CestaApuracaoIr.Comum).GanhoIsentoAcoes);
        Assert.Equal(0m, Linha(corretoras, 7, CestaApuracaoIr.DayTrade).Resultado);

        var mesma = Apurar(
            Compra(1, new DateTime(2024, 7, 8), 10, 10m, corretora: "xp"),
            Venda(2, new DateTime(2024, 7, 8), 10, 20m, 200m, corretora: "XP"));
        Assert.Equal(100m, Linha(mesma, 7, CestaApuracaoIr.DayTrade).Resultado);
        Assert.Equal(20m, Linha(mesma, 7, CestaApuracaoIr.DayTrade).Imposto);
    }

    [Fact]
    public void DataDaOperacao_DefineOMesDaApuracao()
    {
        var apuracao = Apurar(
            Compra(1, new DateTime(2024, 1, 2), 100, 100m),
            Venda(2, new DateTime(2024, 1, 31), 100, 250m, 25_000m));

        Assert.Equal(2_250m, Linha(apuracao, 1, CestaApuracaoIr.Comum).Imposto);
        Assert.Equal(0m, Linha(apuracao, 2, CestaApuracaoIr.Comum).Resultado);
        Assert.Equal(new DateTime(2024, 2, 29), Darf(apuracao, 1).Vencimento);
    }

    [Fact]
    public void PrejuizoDeFiiNaoAbateGanhoDeAcao()
    {
        var apuracao = Apurar(
            Compra(1, new DateTime(2024, 5, 2), 10, 20m, TipoAtivo.Fii),
            Venda(2, new DateTime(2024, 5, 20), 10, 10m, 100m, TipoAtivo.Fii),
            Compra(3, new DateTime(2024, 5, 2), 100, 100m, ativoId: 2),
            Venda(4, new DateTime(2024, 5, 21), 100, 250m, 25_000m, ativoId: 2));

        Assert.Equal(15_000m, Linha(apuracao, 5, CestaApuracaoIr.Comum).BaseCalculo);
        Assert.Equal(2_250m, Linha(apuracao, 5, CestaApuracaoIr.Comum).Imposto);
        Assert.Equal(100m, Linha(apuracao, 5, CestaApuracaoIr.Fii).PrejuizoACompensar);
        Assert.Equal(0m, Linha(apuracao, 5, CestaApuracaoIr.Fii).Imposto);
    }

    [Fact]
    public void CodigoDoDarf_VemDaTabela()
    {
        var regras = Regras();
        regras.Single(regra => regra.Codigo == CodigosRegraFiscal.DarfCodigo).Valor = 4600m;
        var apuracao = ApuradorIrMensal.Apurar(
        [
            Compra(1, new DateTime(2024, 1, 2), 100, 100m),
            Venda(2, new DateTime(2024, 1, 20), 100, 250m, 25_000m)
        ], regras, 2024);

        Assert.Equal(4600, Darf(apuracao, 1).CodigoReceita);
    }

    [Fact]
    public void CalculadoraNaoGravaAliquotaIsencaoNemCodigo()
    {
        var fonte = File.ReadAllText(Caminho("OrcaFacil.Core", "Dirpf", "ApuradorIrMensal.cs"));
        Assert.DoesNotContain("0.15", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("0.20", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("0.00005", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("20000", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("6015", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("0.01m", fonte, StringComparison.Ordinal);
    }

    [Fact]
    public void Vencimento_RecuaSabadoEDomingo()
    {
        Assert.Equal(new DateTime(2024, 2, 29), ApuradorIrMensal.Vencimento(2024, 1));
        Assert.Equal(new DateTime(2024, 6, 28), ApuradorIrMensal.Vencimento(2024, 5));
        Assert.Equal(new DateTime(2025, 2, 28), ApuradorIrMensal.Vencimento(2025, 1));
        Assert.Equal(new DateTime(2025, 1, 31), ApuradorIrMensal.Vencimento(2024, 12));
    }

    [Fact]
    public void GanhoDeFevereiroDe2024_VenceEm28DeMarco()
    {
        Assert.Equal(new DateTime(2024, 3, 28), ApuradorIrMensal.Vencimento(2024, 2));
    }

    [Fact]
    public void DuasVendasNoMes_SomamOBruto()
    {
        var apuracao = Apurar(
            Compra(1, new DateTime(2024, 3, 1), 1, 1m),
            Venda(2, new DateTime(2024, 3, 11), 1, 12_000m, 12_000m),
            Compra(3, new DateTime(2024, 3, 1), 1, 1m, ativoId: 2),
            Venda(4, new DateTime(2024, 3, 18), 1, 8_000.01m, 8_000.01m, ativoId: 2));

        var comum = Linha(apuracao, 3, CestaApuracaoIr.Comum);
        Assert.Equal(20_000.01m, comum.VendasAcoesComuns);
        Assert.Equal(0m, comum.GanhoIsentoAcoes);
        Assert.Equal(19_998.01m, comum.BaseCalculo);
        Assert.Equal(2_999.70m, comum.Imposto);
    }

    [Fact]
    public void FiiNaoEntraNoLimiarDasAcoes()
    {
        var apuracao = Apurar(
            Compra(1, new DateTime(2024, 4, 2), 10, 10m),
            Venda(2, new DateTime(2024, 4, 16), 10, 300m, 3_000m),
            Compra(3, new DateTime(2024, 4, 2), 10, 10m, TipoAtivo.Fii, ativoId: 2),
            Venda(4, new DateTime(2024, 4, 16), 10, 1_800m, 18_000m, TipoAtivo.Fii, ativoId: 2));

        var comum = Linha(apuracao, 4, CestaApuracaoIr.Comum);
        Assert.Equal(3_000m, comum.VendasAcoesComuns);
        Assert.Equal(2_900m, comum.GanhoIsentoAcoes);
        Assert.Equal(0m, comum.BaseCalculo);
        Assert.Equal(0m, comum.Imposto);

        var fii = Linha(apuracao, 4, CestaApuracaoIr.Fii);
        Assert.Equal(17_900m, fii.BaseCalculo);
        Assert.Equal(3_580m, fii.Imposto);
    }

    [Fact]
    public void DarfDeExatamenteDezReais_Emite()
    {
        var apuracao = Apurar(
            Compra(1, new DateTime(2024, 4, 2), 1, 100m, TipoAtivo.Etf),
            Venda(2, new DateTime(2024, 4, 18), 1, 166.67m, 166.67m, TipoAtivo.Etf));

        var comum = Linha(apuracao, 4, CestaApuracaoIr.Comum);
        Assert.Equal(66.67m, comum.BaseCalculo);
        Assert.Equal(10.00m, comum.Imposto);
        var darf = Darf(apuracao, 4);
        Assert.Equal(10.00m, darf.Valor);
        Assert.Equal(0m, darf.SaldoAcumulado);
        Assert.Equal(6015, darf.CodigoReceita);
        Assert.Equal(new DateTime(2024, 5, 31), darf.Vencimento);
    }

    [Fact]
    public void VendaAnteriorA2005_NaoImpedeOAnoAtual()
    {
        var ana = new MembroFamilia { Id = 1, Nome = "Ana", Apelido = "Nica", Avatar = "A" };
        var petr = new AtivoFinanceiro { Id = 1, Ticker = "PETR4", Nome = "Petrobras", Tipo = TipoAtivo.Acao };
        OperacaoAtivo Operacao(int id, TipoOperacao tipo, DateTime data, decimal quantidade, decimal preco, decimal valor) => new()
        {
            Id = id,
            AtivoId = petr.Id,
            Ativo = petr,
            MembroFamiliaId = ana.Id,
            MembroFamilia = ana,
            Tipo = tipo,
            Data = data,
            Quantidade = quantidade,
            PrecoUnitario = preco,
            ValorOperacao = valor
        };

        var entidades = new[]
        {
            Operacao(1, TipoOperacao.Compra, new DateTime(2004, 12, 1), 100, 200m, 20_000m),
            Operacao(2, TipoOperacao.Venda, new DateTime(2004, 12, 20), 100, 100m, 10_000m),
            Operacao(3, TipoOperacao.Compra, new DateTime(2024, 3, 1), 100, 100m, 10_000m),
            Operacao(4, TipoOperacao.Venda, new DateTime(2024, 3, 20), 100, 250m, 25_000m)
        };

        var apuracao = ApuradorIrMensal.Apurar(entidades.Select(ApuradorIrMensal.De), Regras(), 2024);
        var marco = Linha(apuracao, 3, CestaApuracaoIr.Comum);
        Assert.Equal(15_000m, marco.BaseCalculo);
        Assert.Equal(2_250m, marco.Imposto);
        Assert.Equal(2_250m, Darf(apuracao, 3).Valor);

        var dirpf = MontadorDirpf.Montar(2024, entidades, [], CatalogoCodigosDirpf.Linhas(), Regras());
        Assert.Empty(dirpf.RendimentosIsentos);
    }

    [Fact]
    public void IrrfQueSobraNoAno_VaiParaADeclaracaoEmDezembro_EDayTradeFicaDeFora()
    {
        var operacoes = new[]
        {
            Compra(1, new DateTime(2024, 1, 8), 1, 100m, TipoAtivo.Etf),
            Venda(2, new DateTime(2024, 1, 22), 1, 200m, 200m, TipoAtivo.Etf),
            Compra(3, new DateTime(2024, 3, 4), 10, 10m, corretora: "XP", ativoId: 2),
            Venda(4, new DateTime(2024, 3, 4), 10, 20m, 200m, corretora: "XP", ativoId: 2)
        };
        var apuracao = ApuradorIrMensal.Apurar(
            operacoes,
            Regras(),
            2024,
            [
                new IrrfManual(1, 2024, 1, CestaApuracaoIr.Comum, 40m),
                new IrrfManual(1, 2024, 3, CestaApuracaoIr.DayTrade, 50m)
            ]);

        Assert.Equal(0m, Linha(apuracao, 1, CestaApuracaoIr.Comum).ImpostoLiquido);
        Assert.Equal(0m, Linha(apuracao, 3, CestaApuracaoIr.DayTrade).ImpostoLiquido);
        Assert.Equal(25m, Linha(apuracao, 12, CestaApuracaoIr.Comum).IrrfALevarParaDeclaracao);
        Assert.Equal(0m, Linha(apuracao, 12, CestaApuracaoIr.Fii).IrrfALevarParaDeclaracao);
        Assert.Equal(0m, Linha(apuracao, 12, CestaApuracaoIr.DayTrade).IrrfALevarParaDeclaracao);
        Assert.Equal(0m, Linha(apuracao, 1, CestaApuracaoIr.Comum).IrrfALevarParaDeclaracao);
    }

    private static ApuracaoIr Apurar(params OperacaoApuracao[] operacoes) => Apurar(2024, operacoes);

    private static ApuracaoIr Apurar(int ano, params OperacaoApuracao[] operacoes)
        => ApuradorIrMensal.Apurar(operacoes, Regras(), ano);

    private static List<RegraFiscal> Regras() => CatalogoRegrasFiscais.Linhas().ToList();

    private static LinhaApuracaoMensal Linha(ApuracaoIr apuracao, int mes, CestaApuracaoIr cesta)
        => apuracao.Meses.Single(linha => linha.Mes == mes && linha.Cesta == cesta && linha.PerfilId == 1);

    private static LinhaDarf Darf(ApuracaoIr apuracao, int mes)
        => apuracao.Darfs.Single(linha => linha.Mes == mes && linha.PerfilId == 1);

    private static OperacaoApuracao Compra(
        int id,
        DateTime data,
        decimal quantidade,
        decimal preco,
        TipoAtivo tipo = TipoAtivo.Acao,
        int ativoId = 1,
        int perfilId = 1,
        string perfil = "Ana",
        string? corretora = null,
        decimal custos = 0m,
        decimal? valor = null)
        => Operacao(id, TipoOperacao.Compra, data, quantidade, preco, valor ?? quantidade * preco, custos, tipo, ativoId, perfilId, perfil, corretora);

    private static OperacaoApuracao Venda(
        int id,
        DateTime data,
        decimal quantidade,
        decimal preco,
        decimal valor,
        TipoAtivo tipo = TipoAtivo.Acao,
        int ativoId = 1,
        int perfilId = 1,
        string perfil = "Ana",
        string? corretora = null,
        decimal custos = 0m)
        => Operacao(id, TipoOperacao.Venda, data, quantidade, preco, valor, custos, tipo, ativoId, perfilId, perfil, corretora);

    private static OperacaoApuracao Operacao(
        int id,
        TipoOperacao tipoOperacao,
        DateTime data,
        decimal quantidade,
        decimal preco,
        decimal valor,
        decimal custos,
        TipoAtivo tipo,
        int ativoId,
        int perfilId,
        string perfil,
        string? corretora)
        => new(id, perfilId, perfil, ativoId, tipo, corretora, tipoOperacao, data, quantidade, preco, valor, custos);

    private static MovimentoPosicao ParaMovimento(OperacaoApuracao operacao) => new()
    {
        Id = operacao.Id,
        Data = operacao.Data,
        Tipo = operacao.TipoOperacao,
        Quantidade = operacao.Quantidade,
        PrecoUnitario = operacao.PrecoUnitario,
        ValorOperacao = operacao.ValorOperacao,
        Custos = operacao.Custos
    };

    private static string Caminho(params string[] partes)
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null && !File.Exists(Path.Combine(diretorio.FullName, "OrcaFacil.slnx")))
            diretorio = diretorio.Parent;

        Assert.NotNull(diretorio);
        return Path.Combine([diretorio!.FullName, .. partes]);
    }
}
