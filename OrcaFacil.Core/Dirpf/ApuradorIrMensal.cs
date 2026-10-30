using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Dirpf;

/// <summary>
/// Operação pronta para a apuração. O custo da venda sai da calculadora de resultado.
/// <see cref="Data"/> é o pregão: mês, teto, day trade e vencimento do DARF.
/// </summary>
public sealed record OperacaoApuracao(
    int Id,
    int PerfilId,
    string Perfil,
    int AtivoId,
    TipoAtivo Tipo,
    string? Corretora,
    TipoOperacao TipoOperacao,
    DateTime Data,
    decimal Quantidade,
    decimal PrecoUnitario,
    decimal ValorOperacao,
    decimal Custos,
    string Ticker = "");

/// <summary>
/// IRRF informado para um mês e uma cesta. Quando existe, substitui a estimativa.
/// Sem linha, o padrão é zero.
/// </summary>
public sealed record IrrfManual(int PerfilId, int Ano, int Mes, CestaApuracaoIr Cesta, decimal Valor);

/// <summary>Uma cesta em um mês. Lucro e prejuízo do lançamento não passam por aqui.</summary>
public sealed record LinhaApuracaoMensal(
    int PerfilId,
    string Perfil,
    int Ano,
    int Mes,
    CestaApuracaoIr Cesta,
    decimal Resultado,
    decimal PrejuizoCompensado,
    decimal PrejuizoACompensar,
    decimal BaseCalculo,
    decimal Aliquota,
    decimal Imposto,
    decimal Irrf,
    decimal ImpostoLiquido,
    decimal VendasAcoesComuns,
    decimal GanhoIsentoAcoes,
    decimal IrrfALevarParaDeclaracao);

/// <summary>DARF do mês, já com o saldo abaixo do mínimo. Vencimento só quando há valor a pagar.</summary>
public sealed record LinhaDarf(
    int PerfilId,
    string Perfil,
    int Ano,
    int Mes,
    int CodigoReceita,
    decimal ImpostoDoMes,
    decimal AcumuladoAnterior,
    decimal SaldoAcumulado,
    decimal Valor,
    DateTime? Vencimento);

/// <summary>Soma do ano do ganho isento de ações, para a linha do código da ficha.</summary>
public sealed record GanhoIsentoAnual(int PerfilId, string Perfil, int Ano, decimal Valor);

/// <summary>Apuração de um ano-base, por perfil. Meses anteriores só alimentam o saldo.</summary>
public sealed class ApuracaoIr
{
    public IReadOnlyList<LinhaApuracaoMensal> Meses { get; init; } = [];

    public IReadOnlyList<LinhaDarf> Darfs { get; init; } = [];

    public IReadOnlyList<GanhoIsentoAnual> GanhosIsentos { get; init; } = [];
}

/// <summary>
/// Apura o IR da renda variável mês a mês e monta o DARF.
/// O mês, o teto de vendas de ações, o day trade e o vencimento do DARF usam o pregão
/// (<see cref="OperacaoAtivo.DataPregao"/>).
/// O DARF vence no último dia útil do mês seguinte ao do pregão,
/// no <see cref="CalendarioBancarioNacional"/>.
/// Na importação B3, a liquidação é a data do arquivo e o pregão recua D+2 dias úteis.
/// No lançamento manual, a data digitada é o pregão e também a liquidação.
/// A #30 gravava só a liquidação e fazia o DARF vencer por ela. A #104 altera essa regra.
/// Fonte: Perguntas e Respostas IRPF 2026, pergunta 745,
/// https://www.gov.br/receitafederal/pt-br/centrais-de-conteudo/publicacoes/perguntas-e-respostas/dirpf/p-r-irpf-2026-v1-00-2026-04-23.pdf
/// Fiagro entra como FII: o tipo do ativo não muda e o mesmo dia não vai para day trade.
/// ETF de ações entra na cesta comum, a 15%, sem a isenção de R$ 20 mil.
/// ETF de renda fixa (Lei nº 13.043/2014) não entra nesta apuração nem no DARF:
/// a corretora retém na fonte e a DIRPF leva o líquido para a tributação exclusiva.
/// O resultado da venda é o da <see cref="CalculadoraResultadoRealizado"/>:
/// quantidade × preço + custos na compra, e o valor da operação na venda.
/// O arredondamento afasta do zero, com as casas da regra vigente.
/// Alíquota, isenção, day trade, FII, código e mínimo do DARF vêm da tabela.
/// Meses anteriores a 01/01/2005 ficam de fora: as regras desta apuração começam nessa data.
/// </summary>
public static class ApuradorIrMensal
{
    private static readonly CestaApuracaoIr[] Cestas = [CestaApuracaoIr.Comum, CestaApuracaoIr.DayTrade, CestaApuracaoIr.Fii];

    public static ApuracaoIr Apurar(AppDbContext contexto, int ano, IEnumerable<IrrfManual>? irrf = null)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        MontadorDirpf.ExigirAno(ano);

        var operacoes = contexto.OperacoesAtivo.AsNoTracking()
            .Include(operacao => operacao.Ativo)
            .Include(operacao => operacao.MembroFamilia)
            .AsEnumerable()
            .Select(De);
        var regras = contexto.RegrasFiscais.AsNoTracking().ToList();
        return Apurar(operacoes, regras, ano, irrf);
    }

    public static ApuracaoIr Apurar(
        IEnumerable<OperacaoApuracao> operacoes,
        IEnumerable<RegraFiscal> regras,
        int ano,
        IEnumerable<IrrfManual>? irrf = null)
    {
        ArgumentNullException.ThrowIfNull(operacoes);
        ArgumentNullException.ThrowIfNull(regras);
        MontadorDirpf.ExigirAno(ano);

        var manuais = Indexar(irrf);
        var corte = new DateTime(ano, 12, 31);
        var consideradas = operacoes.Where(operacao => operacao.Data.Date <= corte).ToList();
        var tabela = new TabelaRegrasFiscais(regras);
        var vendas = Classificar(consideradas, tabela);
        if (vendas.Count == 0)
            return new ApuracaoIr();

        var meses = new List<LinhaApuracaoMensal>();
        var darfs = new List<LinhaDarf>();
        var ganhos = new List<GanhoIsentoAnual>();
        foreach (var perfil in vendas.GroupBy(venda => venda.PerfilId))
        {
            var nome = perfil.First().Perfil;
            if (perfil.Any(venda => venda.Perfil != nome))
                throw new InvalidOperationException($"O perfil {perfil.Key} está com nomes divergentes.");

            ApurarPerfil(perfil.Key, nome, perfil.ToList(), tabela, ano, manuais, meses, darfs, ganhos);
        }

        return new ApuracaoIr
        {
            Meses = OrdenarMeses(meses),
            Darfs = darfs.OrderBy(linha => linha.Perfil, Comparador()).ThenBy(linha => linha.Ano).ThenBy(linha => linha.Mes).ToList(),
            GanhosIsentos = ganhos.OrderBy(linha => linha.Perfil, Comparador()).ToList()
        };
    }

    public static OperacaoApuracao De(OperacaoAtivo operacao)
    {
        ArgumentNullException.ThrowIfNull(operacao);
        if (operacao.Ativo is null)
            throw new InvalidOperationException($"A apuração precisa do ativo {operacao.AtivoId}.");
        if (operacao.MembroFamilia is null)
            throw new InvalidOperationException($"A apuração precisa do perfil {operacao.MembroFamiliaId}.");

        var liquidacao = operacao.Data.Date;
        var pregao = operacao.DataPregao.Date;
        if (pregao == default)
            pregao = liquidacao;

        return new OperacaoApuracao(
            operacao.Id,
            operacao.MembroFamiliaId,
            TextoPerfil(operacao.MembroFamilia),
            operacao.AtivoId,
            operacao.Ativo.Tipo,
            operacao.Corretora,
            operacao.Tipo,
            pregao,
            operacao.Quantidade,
            operacao.PrecoUnitario,
            operacao.ValorOperacao,
            operacao.Custos,
            operacao.Ativo.Ticker);
    }

    /// <summary>
    /// Último dia útil do mês seguinte ao do pregão.
    /// A apuração passa o mês do pregão, não o da liquidação.
    /// Sábado, domingo e feriado recuam.
    /// </summary>
    public static DateTime Vencimento(int anoGanho, int mesGanho)
    {
        var mes = mesGanho == 12 ? 1 : mesGanho + 1;
        var ano = mesGanho == 12 ? anoGanho + 1 : anoGanho;
        return CalendarioBancarioNacional.UltimoDiaUtil(ano, mes);
    }

    private static void ApurarPerfil(
        int perfilId,
        string perfil,
        List<VendaClassificada> vendas,
        TabelaRegrasFiscais tabela,
        int ano,
        IReadOnlyDictionary<(int Perfil, int Ano, int Mes, CestaApuracaoIr Cesta), decimal> manuais,
        List<LinhaApuracaoMensal> meses,
        List<LinhaDarf> darfs,
        List<GanhoIsentoAnual> ganhos)
    {
        var prejuizo = Zerar();
        var creditoIrrf = Zerar();
        var saldoDeclaracao = Zerar();
        decimal acumuladoDarf = 0m;
        var doAno = new List<LinhaApuracaoMensal>();
        var darfsDoAno = new List<LinhaDarf>();
        var primeira = vendas.Min(venda => venda.Data.Date);
        var inicio = new DateTime(primeira.Year, primeira.Month, 1);
        var janeiro = new DateTime(ano, 1, 1);
        if (inicio > janeiro)
            inicio = janeiro;

        // Sem regra de apuração antes da Lei nº 11.033/2004. O janeiro do ano pedido
        // já foi escolhido acima quando ele é anterior à primeira venda.
        if (inicio < CatalogoRegrasFiscais.InicioLei11033)
            inicio = CatalogoRegrasFiscais.InicioLei11033;

        for (var cursor = inicio; cursor <= new DateTime(ano, 12, 1); cursor = cursor.AddMonths(1))
        {
            var fim = new DateTime(cursor.Year, cursor.Month, DateTime.DaysInMonth(cursor.Year, cursor.Month));
            var parametros = tabela.ParaApuracao(fim);
            if (cursor.Month == 1 && !parametros.CompensaPrejuizoEntreAnos)
            {
                prejuizo = Zerar();
            }

            // O valor a levar para a declaração é do ano-calendário. Janeiro zera
            // antes de somar o IRRF do mês, senão a sobra do ano anterior reaparece
            // em dezembro.
            if (cursor.Month == 1)
                saldoDeclaracao = Zerar();

            var doMes = vendas.Where(venda => venda.Data.Year == cursor.Year && venda.Data.Month == cursor.Month).ToList();
            ExigirRegrasEstaveis(tabela, parametros, fim, doMes);

            var vendasAcoes = doMes.Where(venda => venda.Tipo == TipoAtivo.Acao).Sum(venda => venda.ValorComum);
            var resultadoAcoes = doMes.Where(venda => venda.Tipo == TipoAtivo.Acao).Sum(venda => venda.ResultadoComum);
            var resultadoOutros = doMes.Where(venda => venda.Tipo is TipoAtivo.Etf or TipoAtivo.Bdr).Sum(venda => venda.ResultadoComum);
            var resultadoDay = doMes.Where(venda => venda.Tipo != TipoAtivo.Fii).Sum(venda => venda.ResultadoDayTrade);
            var resultadoFii = doMes.Where(venda => venda.Tipo == TipoAtivo.Fii).Sum(venda => venda.ResultadoComum);
            var mesIsento = vendasAcoes > 0m && vendasAcoes <= parametros.LimiteVendasAcoes;

            decimal ganhoIsento = 0m;
            var resultadoComum = resultadoOutros;
            if (mesIsento)
            {
                if (resultadoAcoes > 0m)
                    ganhoIsento = resultadoAcoes;
                else if (resultadoAcoes < 0m && parametros.CompensaPrejuizoNoMesIsento)
                    resultadoComum += resultadoAcoes;
            }
            else
            {
                resultadoComum += resultadoAcoes;
            }

            var aliquotaComum = AliquotaComum(tabela, fim, doMes, mesIsento);
            var irrf = IrrfDoMes(perfilId, cursor.Year, cursor.Month, doMes, parametros, manuais);
            var linhas = new Dictionary<CestaApuracaoIr, LinhaApuracaoMensal>();
            foreach (var cesta in Cestas)
            {
                var resultado = cesta switch
                {
                    CestaApuracaoIr.Comum => resultadoComum,
                    CestaApuracaoIr.DayTrade => resultadoDay,
                    CestaApuracaoIr.Fii => resultadoFii,
                    _ => throw new InvalidOperationException($"Cesta de apuração desconhecida: {cesta}.")
                };
                var aliquota = cesta switch
                {
                    CestaApuracaoIr.Comum => aliquotaComum,
                    CestaApuracaoIr.DayTrade => parametros.AliquotaDayTrade,
                    CestaApuracaoIr.Fii => tabela.Para(fim, TipoAtivo.Fii).Aliquota,
                    _ => throw new InvalidOperationException($"Cesta de apuração desconhecida: {cesta}.")
                };

                var saldoAntes = prejuizo[cesta];
                decimal compensado;
                decimal baseCalculo;
                if (resultado < 0m)
                {
                    compensado = 0m;
                    baseCalculo = 0m;
                    prejuizo[cesta] = saldoAntes - resultado;
                }
                else
                {
                    compensado = Math.Min(saldoAntes, resultado);
                    baseCalculo = resultado - compensado;
                    prejuizo[cesta] = saldoAntes - compensado;
                }

                var imposto = baseCalculo == 0m
                    ? 0m
                    : CalculadoraResultadoRealizado.Arredondar(baseCalculo * aliquota, parametros.CasasDecimais);
                var entrada = parametros.CompensaIrrfMesesSeguintes ? creditoIrrf[cesta] : 0m;
                var disponivel = irrf[cesta] + entrada;
                var usado = Math.Min(disponivel, imposto);
                creditoIrrf[cesta] = parametros.CompensaIrrfMesesSeguintes ? disponivel - usado : 0m;
                if (cesta == CestaApuracaoIr.DayTrade && cursor.Month == 12)
                    creditoIrrf[cesta] = 0m;

                // Art. 2º, § 7º, da Lei nº 11.033/2004: o saldo do IRRF do § 1º
                // (operações comuns e FII, não o day trade) que não foi deduzido
                // no mês nem nos seguintes vai para a declaração. O day trade
                // para em dezembro e não entra nesse saldo.
                if (cesta != CestaApuracaoIr.DayTrade)
                {
                    if (parametros.CompensaIrrfMesesSeguintes)
                        saldoDeclaracao[cesta] = creditoIrrf[cesta];
                    else
                        saldoDeclaracao[cesta] += disponivel - usado;
                }

                var aLevar = cursor.Month == 12 && cesta != CestaApuracaoIr.DayTrade
                    ? saldoDeclaracao[cesta]
                    : 0m;

                linhas[cesta] = new LinhaApuracaoMensal(
                    perfilId,
                    perfil,
                    cursor.Year,
                    cursor.Month,
                    cesta,
                    resultado,
                    compensado,
                    prejuizo[cesta],
                    baseCalculo,
                    aliquota,
                    imposto,
                    irrf[cesta],
                    imposto - usado,
                    cesta == CestaApuracaoIr.Comum ? vendasAcoes : 0m,
                    cesta == CestaApuracaoIr.Comum ? ganhoIsento : 0m,
                    aLevar);
            }

            var impostoMes = Cestas.Sum(cesta => linhas[cesta].ImpostoLiquido);
            var anterior = acumuladoDarf;
            var total = anterior + impostoMes;
            decimal valorDarf;
            DateTime? vencimento = null;
            if (total > 0m && (!parametros.AcumularAbaixoDoMinimo || total >= parametros.ValorMinimoDarf))
            {
                valorDarf = total;
                acumuladoDarf = 0m;
                vencimento = Vencimento(cursor.Year, cursor.Month);
            }
            else
            {
                valorDarf = 0m;
                acumuladoDarf = total;
            }

            if (cursor.Year != ano)
                continue;

            doAno.AddRange(Cestas.Select(cesta => linhas[cesta]));
            darfsDoAno.Add(new LinhaDarf(
                perfilId,
                perfil,
                cursor.Year,
                cursor.Month,
                parametros.CodigoDarf,
                impostoMes,
                anterior,
                acumuladoDarf,
                valorDarf,
                vencimento));
        }

        if (!doAno.Any(Interessante) && !darfsDoAno.Any(linha => linha.Valor > 0m || linha.AcumuladoAnterior > 0m || linha.SaldoAcumulado > 0m))
            return;

        meses.AddRange(doAno);
        darfs.AddRange(darfsDoAno);
        var ganho = doAno.Where(linha => linha.Cesta == CestaApuracaoIr.Comum).Sum(linha => linha.GanhoIsentoAcoes);
        if (ganho == 0m)
            return;

        var casas = tabela.ParaApuracao(new DateTime(ano, 12, 31)).CasasDecimais;
        var arredondado = CalculadoraResultadoRealizado.Arredondar(ganho, casas);
        if (arredondado != 0m)
            ganhos.Add(new GanhoIsentoAnual(perfilId, perfil, ano, arredondado));
    }

    private static bool Interessante(LinhaApuracaoMensal linha)
        => linha.Resultado != 0m
            || linha.PrejuizoCompensado != 0m
            || linha.PrejuizoACompensar != 0m
            || linha.Imposto != 0m
            || linha.Irrf != 0m
            || linha.VendasAcoesComuns != 0m
            || linha.GanhoIsentoAcoes != 0m;

    private static Dictionary<CestaApuracaoIr, decimal> Zerar()
        => Cestas.ToDictionary(cesta => cesta, _ => 0m);

    private static decimal AliquotaComum(
        TabelaRegrasFiscais tabela,
        DateTime fim,
        IReadOnlyList<VendaClassificada> doMes,
        bool mesIsento)
    {
        var tipos = new List<TipoAtivo>();
        if (!mesIsento && doMes.Any(venda => venda.Tipo == TipoAtivo.Acao && (venda.ResultadoComum != 0m || venda.ValorComum != 0m)))
            tipos.Add(TipoAtivo.Acao);
        if (doMes.Any(venda => venda.Tipo == TipoAtivo.Etf && (venda.ResultadoComum != 0m || venda.ValorComum != 0m)))
            tipos.Add(TipoAtivo.Etf);
        if (doMes.Any(venda => venda.Tipo == TipoAtivo.Bdr && (venda.ResultadoComum != 0m || venda.ValorComum != 0m)))
            tipos.Add(TipoAtivo.Bdr);

        if (tipos.Count == 0)
            return tabela.Para(fim, TipoAtivo.Acao).Aliquota;

        var aliquotas = tipos.Select(tipo => tabela.Para(fim, tipo).Aliquota).Distinct().ToList();
        if (aliquotas.Count > 1)
            throw new InvalidOperationException(
                $"As alíquotas das operações comuns divergem em {fim:MM/yyyy}.");

        return aliquotas[0];
    }

    private static Dictionary<CestaApuracaoIr, decimal> IrrfDoMes(
        int perfilId,
        int ano,
        int mes,
        IReadOnlyList<VendaClassificada> doMes,
        ParametrosApuracaoIr parametros,
        IReadOnlyDictionary<(int Perfil, int Ano, int Mes, CestaApuracaoIr Cesta), decimal> manuais)
    {
        var irrf = Zerar();
        if (parametros.EstimarIrrf)
        {
            decimal exatoComum = 0m;
            decimal exatoFii = 0m;
            foreach (var venda in doMes)
            {
                var bruto = venda.ValorComum * parametros.AliquotaIrrfComum;
                if (venda.Tipo == TipoAtivo.Fii)
                    exatoFii += bruto;
                else
                    exatoComum += bruto;
            }

            if (exatoComum + exatoFii > parametros.ValorMinimoRetencaoIrrf)
            {
                irrf[CestaApuracaoIr.Comum] = CalculadoraResultadoRealizado.Arredondar(exatoComum, parametros.CasasDecimais);
                irrf[CestaApuracaoIr.Fii] = CalculadoraResultadoRealizado.Arredondar(exatoFii, parametros.CasasDecimais);
            }

            foreach (var dia in doMes.Where(venda => venda.Tipo != TipoAtivo.Fii).GroupBy(venda => venda.ChaveDia))
            {
                var liquido = dia.Sum(venda => venda.ResultadoDayTrade);
                if (liquido > 0m)
                    irrf[CestaApuracaoIr.DayTrade] += CalculadoraResultadoRealizado.Arredondar(
                        liquido * parametros.AliquotaIrrfDayTrade, parametros.CasasDecimais);
            }
        }

        foreach (var cesta in Cestas)
        {
            if (!manuais.TryGetValue((perfilId, ano, mes, cesta), out var informado))
                continue;

            irrf[cesta] = CalculadoraResultadoRealizado.Arredondar(informado, parametros.CasasDecimais);
        }

        return irrf;
    }

    private static void ExigirRegrasEstaveis(
        TabelaRegrasFiscais tabela,
        ParametrosApuracaoIr fim,
        DateTime dataFim,
        IReadOnlyList<VendaClassificada> doMes)
    {
        foreach (var data in doMes.Select(venda => venda.Data.Date).Distinct())
        {
            var outra = tabela.ParaApuracao(data);
            if (outra.CasasDecimais != fim.CasasDecimais
                || outra.LimiteVendasAcoes != fim.LimiteVendasAcoes
                || outra.AliquotaDayTrade != fim.AliquotaDayTrade
                || outra.AliquotaIrrfComum != fim.AliquotaIrrfComum
                || outra.AliquotaIrrfDayTrade != fim.AliquotaIrrfDayTrade
                || outra.ValorMinimoRetencaoIrrf != fim.ValorMinimoRetencaoIrrf
                || outra.CodigoDarf != fim.CodigoDarf
                || outra.ValorMinimoDarf != fim.ValorMinimoDarf
                || outra.AcumularAbaixoDoMinimo != fim.AcumularAbaixoDoMinimo
                || outra.EstimarIrrf != fim.EstimarIrrf
                || outra.CompensaIrrfMesesSeguintes != fim.CompensaIrrfMesesSeguintes
                || outra.CompensaPrejuizoNoMesIsento != fim.CompensaPrejuizoNoMesIsento)
            {
                throw new InvalidOperationException($"A regra fiscal mudou dentro de {dataFim:MM/yyyy}.");
            }

            foreach (var tipo in doMes.Select(venda => venda.Tipo).Distinct())
            {
                if (tabela.Para(data, tipo).Aliquota != tabela.Para(dataFim, tipo).Aliquota)
                    throw new InvalidOperationException($"A alíquota de {tipo} mudou dentro de {dataFim:MM/yyyy}.");
            }
        }
    }

    private static List<VendaClassificada> Classificar(IReadOnlyList<OperacaoApuracao> operacoes, TabelaRegrasFiscais tabela)
    {
        var saida = new List<VendaClassificada>();
        foreach (var grupo in operacoes.GroupBy(operacao => (operacao.PerfilId, operacao.AtivoId)))
        {
            var lista = grupo.ToList();
            var amostra = lista[0];
            if (lista.Any(operacao => operacao.Tipo != amostra.Tipo
                    || operacao.Perfil != amostra.Perfil
                    || !string.Equals(operacao.Ticker, amostra.Ticker, StringComparison.Ordinal)))
                throw new InvalidOperationException($"O ativo {amostra.AtivoId} do perfil {amostra.PerfilId} está com dados divergentes.");

            // Tributação exclusiva na fonte. Enquanto o papel ainda está como ação, continua na apuração.
            if (amostra.Tipo == TipoAtivo.Etf && CatalogoEtfConhecido.EhRendaFixa(amostra.Ticker))
                continue;

            if (lista.Select(operacao => operacao.Id).Distinct().Count() != lista.Count)
                throw new InvalidOperationException($"O ativo {amostra.AtivoId} tem operações com o mesmo id.");

            var calculado = CalculadoraResultadoRealizado.Calcular(
                lista.Select(ParaMovimento),
                data => tabela.Para(data, amostra.Tipo));
            var porId = calculado.Vendas.ToDictionary(venda => venda.Id);
            var dayTrade = QuantidadeDayTrade(lista, amostra.Tipo);

            foreach (var venda in lista.Where(operacao => operacao.TipoOperacao == TipoOperacao.Venda))
            {
                if (!porId.TryGetValue(venda.Id, out var resultado))
                    throw new InvalidOperationException($"A venda {venda.Id} ficou sem resultado.");

                var casas = tabela.Para(venda.Data, amostra.Tipo).CasasDecimais;
                var qtdDay = amostra.Tipo == TipoAtivo.Fii ? 0m : dayTrade[venda.Id];
                var (resultadoDay, resultadoComum) = Dividir(resultado.Resultado, qtdDay, venda.Quantidade, casas);
                var (_, valorComum) = Dividir(venda.ValorOperacao, qtdDay, venda.Quantidade, casas);
                saida.Add(new VendaClassificada(
                    amostra.PerfilId,
                    amostra.Perfil,
                    amostra.AtivoId,
                    amostra.Tipo,
                    venda.Data.Date,
                    resultadoDay,
                    resultadoComum,
                    valorComum,
                    $"{amostra.AtivoId}|{Corretora(venda.Corretora)}|{venda.Data:yyyyMMdd}"));
            }
        }

        return saida;
    }

    private static Dictionary<int, decimal> QuantidadeDayTrade(IReadOnlyList<OperacaoApuracao> lista, TipoAtivo tipo)
    {
        var mapa = new Dictionary<int, decimal>();
        foreach (var dia in lista.GroupBy(operacao => (operacao.Data.Date, Corretora(operacao.Corretora))))
        {
            var vendas = dia.Where(operacao => operacao.TipoOperacao == TipoOperacao.Venda).OrderBy(operacao => operacao.Id).ToList();
            if (tipo == TipoAtivo.Fii)
            {
                foreach (var venda in vendas)
                    mapa[venda.Id] = 0m;
                continue;
            }

            var compras = dia.Where(operacao => operacao.TipoOperacao == TipoOperacao.Compra).Sum(operacao => operacao.Quantidade);
            var restante = Math.Min(compras, vendas.Sum(venda => venda.Quantidade));
            foreach (var venda in vendas)
            {
                var quantidade = Math.Min(venda.Quantidade, restante);
                mapa[venda.Id] = quantidade;
                restante -= quantidade;
            }
        }

        return mapa;
    }

    private static (decimal Parte, decimal Resto) Dividir(decimal total, decimal quantidadeParte, decimal quantidadeTotal, int casas)
    {
        var cheio = CalculadoraResultadoRealizado.Arredondar(total, casas);
        if (quantidadeParte == 0m)
            return (0m, cheio);
        if (quantidadeParte == quantidadeTotal)
            return (cheio, 0m);

        var parte = CalculadoraResultadoRealizado.Arredondar(total * quantidadeParte / quantidadeTotal, casas);
        if (Math.Abs(parte) > Math.Abs(cheio))
            return (cheio, 0m);

        return (parte, cheio - parte);
    }

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

    private static IReadOnlyDictionary<(int Perfil, int Ano, int Mes, CestaApuracaoIr Cesta), decimal> Indexar(IEnumerable<IrrfManual>? irrf)
    {
        var mapa = new Dictionary<(int, int, int, CestaApuracaoIr), decimal>();
        if (irrf is null)
            return mapa;

        foreach (var item in irrf)
        {
            if (item.Valor < 0m)
                throw new InvalidOperationException("O IRRF informado não pode ser negativo.");
            if (item.Mes is < 1 or > 12)
                throw new InvalidOperationException("O mês do IRRF informado deve estar entre 1 e 12.");
            if (!mapa.TryAdd((item.PerfilId, item.Ano, item.Mes, item.Cesta), item.Valor))
                throw new InvalidOperationException("Há dois IRRF informados para o mesmo mês e a mesma cesta.");
        }

        return mapa;
    }

    private static List<LinhaApuracaoMensal> OrdenarMeses(List<LinhaApuracaoMensal> meses)
        => meses
            .OrderBy(linha => linha.Perfil, Comparador())
            .ThenBy(linha => linha.Ano)
            .ThenBy(linha => linha.Mes)
            .ThenBy(linha => linha.Cesta)
            .ToList();

    private static StringComparer Comparador()
        => StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), ignoreCase: true);

    private static string Corretora(string? corretora)
        => string.IsNullOrWhiteSpace(corretora) ? "" : corretora.Trim().ToUpperInvariant();

    private static string TextoPerfil(MembroFamilia membro)
    {
        if (string.IsNullOrWhiteSpace(membro.Apelido))
            return membro.Nome;

        return $"{membro.Nome} ({membro.Apelido})";
    }

    private sealed record VendaClassificada(
        int PerfilId,
        string Perfil,
        int AtivoId,
        TipoAtivo Tipo,
        DateTime Data,
        decimal ResultadoDayTrade,
        decimal ResultadoComum,
        decimal ValorComum,
        string ChaveDia);
}
