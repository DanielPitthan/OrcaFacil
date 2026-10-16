using System.Globalization;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Lancamentos;

/// <summary>Recorte da listagem mensal. O resumo ignora os filtros da lista.</summary>
public enum FiltroListaLancamento
{
    Todos,
    Despesas,
    Receitas,
    APagar
}

/// <summary>
/// Totais, percentuais, séries acumuladas e grupos por data da tela de lançamentos.
/// O gráfico só recebe <see cref="double"/> na hora de montar as séries do MudChart.
/// </summary>
public sealed class PainelLancamentosMes
{
    public const string Traco = "—";

    public decimal Receitas { get; private init; }
    public decimal Despesas { get; private init; }
    public decimal Pagas { get; private init; }
    public decimal APagar { get; private init; }
    public decimal Sobra { get; private init; }

    public string TextoReceitas { get; private init; } = "";
    public string TextoDespesas { get; private init; } = "";
    public string TextoPagas { get; private init; } = "";
    public string TextoAPagar { get; private init; } = "";
    public string TextoSobra { get; private init; } = "";
    public string NomeMes { get; private init; } = "";

    /// <summary>Uma casa decimal. Nulo quando a divisão não existe.</summary>
    public decimal? PercentualReceitaComprometida { get; private init; }

    /// <summary>Uma casa decimal. Nulo quando não há despesas.</summary>
    public decimal? PercentualDespesasPagas { get; private init; }

    public string TextoReceitaComprometida { get; private init; } = "";
    public string TextoDespesasPagas { get; private init; } = "";

    /// <summary>0–100. Acima de 100% a barra fica cheia.</summary>
    public double ProgressoReceitaComprometida { get; private init; }

    public bool ReceitaComprometidaCritica { get; private init; }
    public double ProgressoDespesasPagas { get; private init; }

    public bool ExibirMarcadorHoje { get; private init; }
    public bool ExibirPrevisto { get; private init; }

    /// <summary>Índice 0 do dia de hoje, só no mês corrente.</summary>
    public int? IndiceHoje { get; private init; }

    /// <summary>Posição de hoje no eixo, de 0 a 1. Nulo fora do mês corrente.</summary>
    public decimal? FracaoMarcadorHoje { get; private init; }

    public IReadOnlyList<string> RotulosEixoX { get; private init; } = [];
    public IReadOnlyList<decimal> ReceitasRealizadas { get; private init; } = [];
    public IReadOnlyList<decimal> ReceitasPrevistas { get; private init; } = [];
    public IReadOnlyList<decimal> DespesasRealizadas { get; private init; } = [];
    public IReadOnlyList<decimal> DespesasPrevistas { get; private init; } = [];
    public IReadOnlyList<decimal> SaldosRealizados { get; private init; } = [];
    public IReadOnlyList<decimal> SaldosPrevistos { get; private init; } = [];
    public int PassoEixoY { get; private init; } = 1;
    public string TextoTotaisFinais { get; private init; } = "";
    public string RotuloAcessivelGrafico { get; private init; } = "";

    public bool MesSemLancamentos { get; private init; }
    public int QuantidadeFiltrada { get; private init; }
    public IReadOnlyList<GrupoDiaLancamentos> Grupos { get; private init; } = [];

    public static PainelLancamentosMes Montar(
        IEnumerable<Transacao> lancamentos,
        DateTime mes,
        DateTime hoje,
        FiltroListaLancamento filtro,
        IReadOnlyCollection<int>? categoriasSelecionadas,
        string? busca,
        CultureInfo cultura)
    {
        var inicio = new DateTime(mes.Year, mes.Month, 1);
        var dias = DateTime.DaysInMonth(inicio.Year, inicio.Month);
        var fim = inicio.AddDays(dias - 1);
        var hojeData = hoje.Date;

        var doMes = lancamentos
            .Where(l => l.Data.Date >= inicio && l.Data.Date <= fim)
            .ToList();

        var receitas = doMes.Where(l => l.Tipo == TipoTransacao.Receita).Sum(l => l.Valor);
        var despesas = doMes.Where(l => l.Tipo == TipoTransacao.Despesa).Sum(l => l.Valor);
        var pagas = doMes.Where(l => l.Tipo == TipoTransacao.Despesa && l.Pago).Sum(l => l.Valor);
        var aPagar = despesas - pagas;
        var sobra = receitas - despesas;

        var percentualReceita = receitas <= 0
            ? (decimal?)null
            : Math.Round(despesas / receitas * 100m, 1, MidpointRounding.AwayFromZero);
        var percentualPagas = despesas <= 0
            ? (decimal?)null
            : Math.Round(pagas / despesas * 100m, 1, MidpointRounding.AwayFromZero);

        var (progressoReceita, critica) = Barra(percentualReceita);
        var (progressoPagas, _) = Barra(percentualPagas);

        var modo = fim < hojeData
            ? ModoMes.Passado
            : inicio > hojeData
                ? ModoMes.Futuro
                : ModoMes.Atual;

        var series = MontarSeries(doMes, inicio, dias, hojeData, modo);
        var rotulos = Rotulos(dias);
        int? indiceHoje = modo == ModoMes.Atual ? hojeData.Day - 1 : null;
        decimal? fracao = indiceHoje is int indice && dias > 1
            ? Math.Round((decimal)indice / (dias - 1), 4, MidpointRounding.AwayFromZero)
            : indiceHoje is null ? null : 0m;

        var nomeMes = cultura.TextInfo.ToTitleCase(inicio.ToString("MMMM yyyy", cultura));
        var textoSobra = sobra < 0
            ? "− " + Math.Abs(sobra).ToString("C", cultura)
            : sobra.ToString("C", cultura);
        var textoTotais =
            $"Receitas {receitas.ToString("C", cultura)} · despesas {despesas.ToString("C", cultura)} · sobra {textoSobra}";

        var filtrados = doMes
            .Where(l => PassaFiltro(l, filtro, categoriasSelecionadas, busca))
            .OrderByDescending(l => l.Data)
            .ThenByDescending(l => l.Id)
            .ToList();

        var grupos = filtrados
            .GroupBy(l => l.Data.Date)
            .Select(g => Grupo(g, cultura))
            .ToList();

        return new PainelLancamentosMes
        {
            Receitas = receitas,
            Despesas = despesas,
            Pagas = pagas,
            APagar = aPagar,
            Sobra = sobra,
            TextoReceitas = receitas.ToString("C", cultura),
            TextoDespesas = despesas.ToString("C", cultura),
            TextoPagas = pagas.ToString("C", cultura),
            TextoAPagar = aPagar.ToString("C", cultura),
            TextoSobra = textoSobra,
            NomeMes = nomeMes,
            PercentualReceitaComprometida = percentualReceita,
            PercentualDespesasPagas = percentualPagas,
            TextoReceitaComprometida = TextoPercentual(percentualReceita, "da receita comprometida", cultura),
            TextoDespesasPagas = TextoPercentual(percentualPagas, "das despesas pagas", cultura),
            ProgressoReceitaComprometida = progressoReceita,
            ReceitaComprometidaCritica = critica,
            ProgressoDespesasPagas = progressoPagas,
            ExibirMarcadorHoje = modo == ModoMes.Atual,
            ExibirPrevisto = modo != ModoMes.Passado,
            IndiceHoje = indiceHoje,
            FracaoMarcadorHoje = fracao,
            RotulosEixoX = rotulos,
            ReceitasRealizadas = series.ReceitasRealizadas,
            ReceitasPrevistas = series.ReceitasPrevistas,
            DespesasRealizadas = series.DespesasRealizadas,
            DespesasPrevistas = series.DespesasPrevistas,
            SaldosRealizados = series.SaldosRealizados,
            SaldosPrevistos = series.SaldosPrevistos,
            PassoEixoY = PassoEixo(series),
            TextoTotaisFinais = textoTotais,
            RotuloAcessivelGrafico = $"Evolução de {nomeMes}. {textoTotais}.",
            MesSemLancamentos = doMes.Count == 0,
            QuantidadeFiltrada = filtrados.Count,
            Grupos = grupos
        };
    }

    private static SeriesMes MontarSeries(
        IReadOnlyList<Transacao> doMes,
        DateTime inicio,
        int dias,
        DateTime hoje,
        ModoMes modo)
    {
        var receitaDia = new decimal[dias];
        var despesaDia = new decimal[dias];
        foreach (var lancamento in doMes)
        {
            var indice = lancamento.Data.Day - 1;
            if (lancamento.Tipo == TipoTransacao.Receita)
                receitaDia[indice] += lancamento.Valor;
            else
                despesaDia[indice] += lancamento.Valor;
        }

        var receitasRealizadas = new decimal[dias];
        var receitasPrevistas = new decimal[dias];
        var despesasRealizadas = new decimal[dias];
        var despesasPrevistas = new decimal[dias];
        var saldosRealizados = new decimal[dias];
        var saldosPrevistos = new decimal[dias];

        decimal receitaPrevista = 0;
        decimal despesaPrevista = 0;
        decimal receitaRealizada = 0;
        decimal despesaRealizada = 0;

        for (var i = 0; i < dias; i++)
        {
            receitaPrevista += receitaDia[i];
            despesaPrevista += despesaDia[i];

            var dia = inicio.AddDays(i);
            var contaComoRealizado = modo == ModoMes.Passado || (modo == ModoMes.Atual && dia <= hoje);
            if (contaComoRealizado)
            {
                receitaRealizada = receitaPrevista;
                despesaRealizada = despesaPrevista;
            }

            receitasRealizadas[i] = receitaRealizada;
            despesasRealizadas[i] = despesaRealizada;
            receitasPrevistas[i] = receitaPrevista;
            despesasPrevistas[i] = despesaPrevista;
            saldosRealizados[i] = receitaRealizada - despesaRealizada;
            saldosPrevistos[i] = receitaPrevista - despesaPrevista;
        }

        return new SeriesMes(
            receitasRealizadas,
            receitasPrevistas,
            despesasRealizadas,
            despesasPrevistas,
            saldosRealizados,
            saldosPrevistos);
    }

    private static string[] Rotulos(int dias)
    {
        var rotulos = new string[dias];
        for (var dia = 1; dia <= dias; dia++)
        {
            var mostra = dia == 1 || dia == 5 || dia == 10 || dia == 15 || dia == 20 || dia == 25 || dia == dias;
            rotulos[dia - 1] = mostra ? dia.ToString(CultureInfo.InvariantCulture) : "";
        }

        return rotulos;
    }

    private static int PassoEixo(SeriesMes series)
    {
        var maximo = series.ReceitasRealizadas
            .Concat(series.ReceitasPrevistas)
            .Concat(series.DespesasRealizadas)
            .Concat(series.DespesasPrevistas)
            .Concat(series.SaldosRealizados)
            .Concat(series.SaldosPrevistos)
            .Select(Math.Abs)
            .DefaultIfEmpty(0m)
            .Max();

        if (maximo <= 0)
            return 1;

        var alvo = (double)maximo / 4d;
        if (alvo < 1)
            return 1;

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(alvo)));
        var normalizado = alvo / magnitude;
        var bonito = normalizado <= 1 ? 1d : normalizado <= 2 ? 2d : normalizado <= 5 ? 5d : 10d;
        return Math.Max(1, (int)Math.Round(bonito * magnitude, MidpointRounding.AwayFromZero));
    }

    private static bool PassaFiltro(
        Transacao lancamento,
        FiltroListaLancamento filtro,
        IReadOnlyCollection<int>? categorias,
        string? busca)
    {
        if (!string.IsNullOrWhiteSpace(busca)
            && lancamento.Descricao.Contains(busca.Trim(), StringComparison.OrdinalIgnoreCase) == false)
            return false;

        if (categorias is { Count: > 0 } && categorias.Contains(lancamento.CategoriaId) == false)
            return false;

        return filtro switch
        {
            FiltroListaLancamento.Despesas => lancamento.Tipo == TipoTransacao.Despesa,
            FiltroListaLancamento.Receitas => lancamento.Tipo == TipoTransacao.Receita,
            FiltroListaLancamento.APagar => lancamento.Tipo == TipoTransacao.Despesa && !lancamento.Pago,
            _ => true
        };
    }

    private static GrupoDiaLancamentos Grupo(IGrouping<DateTime, Transacao> grupo, CultureInfo cultura)
    {
        var linhas = grupo.Select(l => Linha(l, cultura)).ToList();
        var liquido = linhas.Sum(l => l.Tipo == TipoTransacao.Receita ? l.Valor : -l.Valor);
        var dia = cultura.DateTimeFormat.GetAbbreviatedDayName(grupo.Key.DayOfWeek).TrimEnd('.');
        var mes = cultura.DateTimeFormat.GetAbbreviatedMonthName(grupo.Key.Month).TrimEnd('.');
        var cabecalho = $"{dia}, {grupo.Key.Day} {mes}".ToUpper(cultura);
        var texto = liquido < 0
            ? "− " + Math.Abs(liquido).ToString("C", cultura)
            : liquido > 0
                ? "+ " + liquido.ToString("C", cultura)
                : liquido.ToString("C", cultura);

        return new GrupoDiaLancamentos(grupo.Key, cabecalho, liquido, texto, linhas);
    }

    private static LinhaLancamentoMes Linha(Transacao lancamento, CultureInfo cultura)
    {
        var categoria = lancamento.Categoria?.Nome ?? "";
        var ehCartao = string.Equals(categoria, "Cartão de Crédito", StringComparison.OrdinalIgnoreCase);
        var quantidade = lancamento.Itens?.Count ?? 0;
        var soma = lancamento.Itens?.Sum(i => i.Valor) ?? 0m;
        var itens = quantidade == 1 ? "1 item" : $"{quantidade} itens";
        var subtitulo = ehCartao ? $"Cartão de crédito · {itens}" : categoria;
        string? classificados = ehCartao && soma < lancamento.Valor
            ? soma.ToString("C", cultura) + " classificados"
            : null;

        var pago = lancamento.Tipo == TipoTransacao.Receita || lancamento.Pago;
        var textoValor = lancamento.Tipo == TipoTransacao.Receita
            ? "+ " + lancamento.Valor.ToString("C", cultura)
            : "− " + lancamento.Valor.ToString("C", cultura);

        return new LinhaLancamentoMes(
            lancamento.Id,
            lancamento.Descricao,
            subtitulo,
            classificados,
            ehCartao,
            lancamento.Valor,
            lancamento.Tipo,
            pago,
            lancamento.Tipo == TipoTransacao.Despesa,
            textoValor,
            pago ? "✓ Pago" : "A pagar",
            lancamento.Categoria?.Icone ?? "🏷️",
            lancamento.Categoria?.CorHex ?? "");
    }

    private static string TextoPercentual(decimal? percentual, string complemento, CultureInfo cultura)
        => percentual is null
            ? Traco
            : $"{percentual.Value.ToString("0.0", cultura)}% {complemento}";

    private static (double Visual, bool Critico) Barra(decimal? percentual)
    {
        if (percentual is null || percentual <= 0)
            return (0d, false);

        if (percentual >= 100m)
            return (100d, percentual > 100m);

        return ((double)percentual.Value, false);
    }

    private enum ModoMes
    {
        Passado,
        Atual,
        Futuro
    }

    private sealed record SeriesMes(
        decimal[] ReceitasRealizadas,
        decimal[] ReceitasPrevistas,
        decimal[] DespesasRealizadas,
        decimal[] DespesasPrevistas,
        decimal[] SaldosRealizados,
        decimal[] SaldosPrevistos);
}

public sealed record GrupoDiaLancamentos(
    DateTime Data,
    string Cabecalho,
    decimal TotalLiquido,
    string TextoTotal,
    IReadOnlyList<LinhaLancamentoMes> Linhas);

public sealed record LinhaLancamentoMes(
    int Id,
    string Titulo,
    string Subtitulo,
    string? Classificados,
    bool EhFaturaCartao,
    decimal Valor,
    TipoTransacao Tipo,
    bool Pago,
    bool PodeAlternarPago,
    string TextoValor,
    string TextoStatus,
    string Icone,
    string CorHex);
