using System.Globalization;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Noticias;

namespace OrcaFacil.Core.Inicio;

/// <summary>Período do resumo compartilhado pela Home e pelo Dashboard.</summary>
public enum PeriodoResumo
{
    EsteMes,
    DesdeOInicio
}

/// <summary>
/// Resumo, orçamentos, metas, categorias e comparação mensal das duas telas.
/// Dinheiro fica em <see cref="decimal"/>. O gráfico só recebe <see cref="double"/> no componente.
/// </summary>
public sealed class PainelInicio
{
    public const string Traco = "—";
    public const decimal LimiarOrcamentoPadrao = 90m;
    public const int MaximoCategoriasVisiveis = 5;
    public const int MaximoMesesGrafico = 6;

    public PeriodoResumo Periodo { get; private init; }
    public string NomeMes { get; private init; } = "";
    public string RotuloSaldo { get; private init; } = "";
    public decimal Saldo { get; private init; }
    public decimal Receitas { get; private init; }
    public decimal Despesas { get; private init; }
    public decimal Pagas { get; private init; }
    public decimal APagar { get; private init; }
    public bool ExibirPagamento { get; private init; }

    public string TextoSaldo { get; private init; } = "";
    public string TextoReceitas { get; private init; } = "";
    public string TextoDespesas { get; private init; } = "";
    public string TextoPagas { get; private init; } = "";
    public string TextoAPagar { get; private init; } = "";
    public string TextoReceitaComprometida { get; private init; } = "";
    public string TextoDespesasPagas { get; private init; } = "";
    public decimal? PercentualReceitaComprometida { get; private init; }
    public decimal? PercentualDespesasPagas { get; private init; }
    public double ProgressoReceita { get; private init; }
    public bool ReceitaCritica { get; private init; }
    public double ProgressoPagas { get; private init; }
    public string RotuloAcessivelResumo { get; private init; } = "";

    public int OrcamentosNoLimite { get; private init; }
    public string TextoContagemOrcamentos { get; private init; } = "";
    public string TextoResumoOrcamentos { get; private init; } = "";
    public string ResumoAlerta { get; private init; } = "";
    public bool ExibirAlertaOrcamento { get; private init; }
    public IReadOnlyList<LinhaOrcamentoResumo> Orcamentos { get; private init; } = [];

    public IReadOnlyList<LinhaMetaResumo> Metas { get; private init; } = [];
    public LinhaMetaResumo? MetaDestaque { get; private init; }

    public decimal TotalCategorias { get; private init; }
    public string TextoTotalCategorias { get; private init; } = "";
    public IReadOnlyList<FatiaCategoria> CategoriasVisiveis { get; private init; } = [];
    public IReadOnlyList<FatiaCategoria> CategoriasOcultas { get; private init; } = [];
    public FatiaCategoria? Outras { get; private init; }
    public IReadOnlyList<FatiaCategoria> FatiasBarra { get; private init; } = [];
    public string RotuloAcessivelCategorias { get; private init; } = "";

    public IReadOnlyList<MesComparacao> Meses { get; private init; } = [];
    public bool DestacarUltimoRotulo { get; private init; }
    public string? NotaLacuna { get; private init; }
    public int PassoEixoY { get; private init; } = 1;
    public string RotuloAcessivelGrafico { get; private init; } = "";

    public static string NomeDoMes(DateTime hoje, CultureInfo cultura)
    {
        var inicio = new DateTime(hoje.Year, hoje.Month, 1);
        return cultura.TextInfo.ToTitleCase(inicio.ToString("MMMM yyyy", cultura));
    }

    public static PainelInicio Montar(EntradaPainelInicio entrada, CultureInfo cultura)
    {
        var hoje = entrada.Hoje.Date;
        var nomeMes = NomeDoMes(hoje, cultura);
        var esteMes = entrada.Periodo == PeriodoResumo.EsteMes;
        var receitas = esteMes ? entrada.Mes.TotalReceitas : entrada.Acumulado.TotalReceitas;
        var despesas = esteMes ? entrada.Mes.TotalDespesas : entrada.Acumulado.TotalDespesas;
        var pagas = esteMes ? entrada.Mes.TotalDespesasPagas : 0m;
        var aPagar = esteMes ? entrada.Mes.TotalDespesasAPagar : 0m;
        var saldo = receitas - despesas;

        var percentualReceita = receitas <= 0
            ? (decimal?)null
            : Math.Round(despesas / receitas * 100m, 1, MidpointRounding.AwayFromZero);
        var percentualPagas = !esteMes || despesas <= 0
            ? (decimal?)null
            : Math.Round(pagas / despesas * 100m, 1, MidpointRounding.AwayFromZero);
        var (progressoReceita, critica) = Barra(percentualReceita);
        var (progressoPagas, _) = Barra(percentualPagas);

        var textoSaldo = TextoMoeda(saldo, cultura, sinalNegativo: true);
        var rotuloSaldo = esteMes ? "Saldo do mês" : "Saldo acumulado";
        var orcamentos = MontarOrcamentos(entrada.Alertas, entrada.LimiarOrcamento, cultura);
        var metas = MontarMetas(entrada.Metas, cultura);
        var categorias = MontarCategorias(entrada.Categorias, cultura);
        var meses = MontarMeses(entrada.Tendencia, hoje, cultura);

        return new PainelInicio
        {
            Periodo = entrada.Periodo,
            NomeMes = nomeMes,
            RotuloSaldo = rotuloSaldo,
            Saldo = saldo,
            Receitas = receitas,
            Despesas = despesas,
            Pagas = pagas,
            APagar = aPagar,
            ExibirPagamento = esteMes,
            TextoSaldo = textoSaldo,
            TextoReceitas = receitas.ToString("C", cultura),
            TextoDespesas = despesas.ToString("C", cultura),
            TextoPagas = pagas.ToString("C", cultura),
            TextoAPagar = aPagar.ToString("C", cultura),
            TextoReceitaComprometida = percentualReceita is null
                ? Traco
                : $"{percentualReceita.Value.ToString("0.0", cultura)}% da receita comprometida",
            TextoDespesasPagas = percentualPagas is null
                ? Traco
                : $"{percentualPagas.Value.ToString("0.0", cultura)}% das despesas pagas",
            PercentualReceitaComprometida = percentualReceita,
            PercentualDespesasPagas = percentualPagas,
            ProgressoReceita = progressoReceita,
            ReceitaCritica = critica,
            ProgressoPagas = progressoPagas,
            RotuloAcessivelResumo = $"{rotuloSaldo} {textoSaldo}. Receitas {receitas.ToString("C", cultura)}. Despesas {despesas.ToString("C", cultura)}.",
            OrcamentosNoLimite = orcamentos.Count,
            TextoContagemOrcamentos = TextoContagem(orcamentos.Count),
            TextoResumoOrcamentos = orcamentos.Count == 1
                ? "1 orçamento no limite"
                : $"{orcamentos.Count} orçamentos no limite",
            ResumoAlerta = string.Join(" · ", orcamentos.Select(o => o.TextoAlerta)),
            ExibirAlertaOrcamento = orcamentos.Count > 0,
            Orcamentos = orcamentos,
            Metas = metas,
            MetaDestaque = EscolherDestaque(metas),
            TotalCategorias = categorias.Total,
            TextoTotalCategorias = categorias.Total.ToString("C", cultura),
            CategoriasVisiveis = categorias.Visiveis,
            CategoriasOcultas = categorias.Ocultas,
            Outras = categorias.Outras,
            FatiasBarra = categorias.Barra,
            RotuloAcessivelCategorias = categorias.Acessivel,
            Meses = meses.Meses,
            DestacarUltimoRotulo = meses.DestacarUltimo,
            NotaLacuna = meses.Nota,
            PassoEixoY = meses.Passo,
            RotuloAcessivelGrafico = meses.Acessivel
        };
    }

    public static string TempoRelativo(DateTimeOffset? publicada, DateTimeOffset agora)
    {
        if (publicada is null)
            return "";

        var delta = agora - publicada.Value;
        if (delta < TimeSpan.FromMinutes(1))
            return "agora";
        if (delta < TimeSpan.FromHours(1))
        {
            var minutos = Math.Max(1, (int)delta.TotalMinutes);
            return minutos == 1 ? "há 1 min" : $"há {minutos} min";
        }
        if (delta < TimeSpan.FromDays(1))
        {
            var horas = Math.Max(1, (int)delta.TotalHours);
            return horas == 1 ? "há 1 hora" : $"há {horas} horas";
        }
        if (delta < TimeSpan.FromDays(7))
        {
            var dias = Math.Max(1, (int)delta.TotalDays);
            return dias == 1 ? "há 1 dia" : $"há {dias} dias";
        }

        return publicada.Value.ToLocalTime().ToString("dd/MM HH:mm");
    }

    public static IReadOnlyList<LinhaNoticia> FormatarNoticias(IEnumerable<Noticia> noticias, DateTimeOffset agora)
        => noticias
            .Select(n => new LinhaNoticia(n.Veiculo, TempoRelativo(n.PublicadaEm, agora), n.Titulo, n.Link))
            .ToList();

    /// <summary>
    /// Reparte cada despesa nas categorias dos itens. A soma das categorias de um lançamento
    /// fica igual ao valor dele: itens que passam do total voltam inteiros para a categoria do lançamento.
    /// </summary>
    public static IReadOnlyList<EntradaCategoria> Classificar(IEnumerable<LancamentoCategoria> lancamentos)
    {
        var somas = new Dictionary<string, decimal>(StringComparer.CurrentCultureIgnoreCase);
        var nomes = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);

        void Somar(string? nome, decimal valor)
        {
            if (valor == 0)
                return;

            var chave = string.IsNullOrWhiteSpace(nome) ? "Sem categoria" : nome.Trim();
            nomes.TryAdd(chave, chave);
            somas.TryGetValue(chave, out var atual);
            somas[chave] = atual + valor;
        }

        foreach (var lancamento in lancamentos)
        {
            var itens = lancamento.Itens;
            if (itens is null || itens.Count == 0)
            {
                Somar(lancamento.Categoria, lancamento.Valor);
                continue;
            }

            var somaItens = itens.Sum(i => i.Total);
            if (somaItens > lancamento.Valor)
            {
                Somar(lancamento.Categoria, lancamento.Valor);
                continue;
            }

            foreach (var item in itens)
                Somar(item.Nome, item.Total);
            Somar(lancamento.Categoria, lancamento.Valor - somaItens);
        }

        return somas.Select(kv => new EntradaCategoria(nomes[kv.Key], kv.Value)).ToList();
    }

    public static string FormatarMarca(decimal valor, CultureInfo cultura)
    {
        var abs = Math.Abs(valor);
        if (abs >= 1_000_000m)
            return (valor / 1_000_000m).ToString("0.#", cultura) + "M";
        if (abs >= 1000m)
            return (valor / 1000m).ToString("0.#", cultura) + "k";
        return valor.ToString("0", cultura);
    }

    private static List<LinhaOrcamentoResumo> MontarOrcamentos(
        IEnumerable<AlertaOrcamentoDto> alertas,
        decimal limiar,
        CultureInfo cultura)
    {
        return alertas
            .Where(a => a.ValorLimite > 0 && a.PercentualUsado >= limiar)
            .OrderByDescending(a => a.PercentualUsado)
            .ThenBy(a => a.CategoriaNome, StringComparer.CurrentCulture)
            .Select(a =>
            {
                var estourou = a.ValorGasto > a.ValorLimite;
                var percentual = a.PercentualUsado;
                var textoPercentual = TextoPercentualCurto(percentual, cultura);
                var chip = estourou ? $"{textoPercentual} · estourou" : textoPercentual;
                var diferenca = Math.Abs(a.ValorLimite - a.ValorGasto);
                var complemento = estourou
                    ? $"{diferenca.ToString("C", cultura)} acima"
                    : $"restam {diferenca.ToString("C", cultura)}";
                var detalhe = $"{a.ValorGasto.ToString("C", cultura)} de {a.ValorLimite.ToString("C", cultura)} · {complemento}";
                var alerta = estourou
                    ? $"{a.CategoriaNome} estourou ({textoPercentual})"
                    : $"{a.CategoriaNome} em {textoPercentual}";
                var (progresso, _) = Barra(percentual);
                return new LinhaOrcamentoResumo(
                    a.CategoriaNome,
                    percentual,
                    textoPercentual,
                    chip,
                    estourou,
                    progresso,
                    detalhe,
                    alerta);
            })
            .ToList();
    }

    private static List<LinhaMetaResumo> MontarMetas(IEnumerable<Meta> metas, CultureInfo cultura)
    {
        return metas
            .OrderByDescending(m => m.PercentualConcluido)
            .ThenBy(m => m.Nome, StringComparer.CurrentCulture)
            .Select(m =>
            {
                var percentual = m.PercentualConcluido;
                var (progresso, _) = Barra(percentual);
                return new LinhaMetaResumo(
                    m.Id,
                    m.Nome,
                    percentual,
                    TextoPercentualCurto(percentual, cultura),
                    progresso,
                    m.Concluida);
            })
            .ToList();
    }

    /// <summary>
    /// Sem flag de meta principal no cadastro: a linha compacta é a aberta mais perto de concluir.
    /// Se todas estão concluídas, fica a de maior percentual.
    /// </summary>
    private static LinhaMetaResumo? EscolherDestaque(IReadOnlyList<LinhaMetaResumo> metas)
    {
        if (metas.Count == 0)
            return null;

        return metas.FirstOrDefault(m => !m.Concluida) ?? metas[0];
    }

    private static CategoriasMontadas MontarCategorias(IEnumerable<EntradaCategoria> categorias, CultureInfo cultura)
    {
        var itens = categorias
            .Where(c => c.Total != 0)
            .GroupBy(c => string.IsNullOrWhiteSpace(c.Nome) ? "Sem categoria" : c.Nome.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .Select(g => (Nome: g.First().Nome.Trim() is { Length: > 0 } nome ? nome : "Sem categoria", Total: g.Sum(c => c.Total)))
            .OrderByDescending(c => c.Total)
            .ThenBy(c => c.Nome, StringComparer.CurrentCulture)
            .ToList();

        var total = itens.Sum(c => c.Total);
        if (itens.Count == 0 || total == 0)
        {
            return new CategoriasMontadas(0, [], [], null, [], "Sem despesas por categoria.");
        }

        var maior = itens.Max(c => c.Total);
        var visiveisOrigem = itens.Take(MaximoCategoriasVisiveis).ToList();
        var ocultasOrigem = itens.Skip(MaximoCategoriasVisiveis).ToList();

        FatiaCategoria Fatia((string Nome, decimal Total) item, int indice, bool outras, int quantidade)
        {
            var percentual = Math.Round(item.Total / total * 100m, 1, MidpointRounding.AwayFromZero);
            var relativa = maior <= 0 ? 0d : (double)Math.Round(item.Total / maior * 100m, 2, MidpointRounding.AwayFromZero);
            return new FatiaCategoria(
                item.Nome,
                item.Total,
                item.Total.ToString("C", cultura),
                percentual,
                TextoPercentualCurto(percentual, cultura),
                relativa,
                indice,
                outras,
                quantidade);
        }

        var visiveis = visiveisOrigem.Select((item, indice) => Fatia(item, indice, false, 1)).ToList();
        var ocultas = ocultasOrigem.Select(item => Fatia(item, -1, false, 1)).ToList();
        FatiaCategoria? outras = null;
        if (ocultasOrigem.Count > 0)
        {
            var soma = ocultasOrigem.Sum(c => c.Total);
            outras = Fatia(($"Outras {ocultasOrigem.Count} {(ocultasOrigem.Count == 1 ? "categoria" : "categorias")}", soma), -1, true, ocultasOrigem.Count);
        }

        var barraOrigem = visiveis.Concat(outras is null ? [] : new[] { outras }).ToList();
        var barra = AjustarLargura(barraOrigem, total);
        var acessivel = "Despesas por categoria. " + string.Join(" ", barra.Select(f => $"{f.Nome} {f.TextoPercentual}."));
        return new CategoriasMontadas(total, visiveis, ocultas, outras, barra, acessivel);
    }

    private static List<FatiaCategoria> AjustarLargura(List<FatiaCategoria> fatias, decimal total)
    {
        if (fatias.Count == 0 || total == 0)
            return fatias;

        var larguras = new decimal[fatias.Count];
        decimal usado = 0;
        for (var i = 0; i < fatias.Count - 1; i++)
        {
            larguras[i] = Math.Round(fatias[i].Total / total * 100m, 2, MidpointRounding.AwayFromZero);
            usado += larguras[i];
        }

        larguras[^1] = 100m - usado;
        return fatias.Select((f, i) => f with { LarguraPilha = (double)larguras[i] }).ToList();
    }

    private static MesesMontados MontarMeses(IEnumerable<TendenciaMensalDto> tendencia, DateTime hoje, CultureInfo cultura)
    {
        var janela = tendencia
            .OrderBy(t => t.Ano)
            .ThenBy(t => t.Mes)
            .TakeLast(MaximoMesesGrafico)
            .ToList();

        var comDados = janela
            .Where(t => t.TotalReceitas != 0 || t.TotalDespesas != 0)
            .Select(t => new MesComparacao(
                t.Mes,
                t.Ano,
                RotuloMes(t.Mes, t.Ano, cultura),
                t.TotalReceitas,
                t.TotalDespesas,
                t.Mes == hoje.Month && t.Ano == hoje.Year))
            .ToList();

        var nota = NotaDeLacunas(janela, cultura);
        var maximo = comDados.Count == 0
            ? 0m
            : comDados.Max(m => Math.Max(m.Receitas, m.Despesas));
        var passo = PassoEixo(maximo);
        var destacar = comDados.Count > 0 && comDados[^1].Atual;
        var acessivel = comDados.Count == 0
            ? "Sem lançamentos nos últimos meses."
            : "Receitas e despesas. " + string.Join(" ", comDados.Select(m =>
                $"{m.Rotulo}: receitas {m.Receitas.ToString("C", cultura)}, despesas {m.Despesas.ToString("C", cultura)}."));

        return new MesesMontados(comDados, destacar, nota, passo, acessivel);
    }

    private static string? NotaDeLacunas(IReadOnlyList<TendenciaMensalDto> janela, CultureInfo cultura)
    {
        var notas = new List<string>();
        var i = 0;
        while (i < janela.Count)
        {
            var vazio = janela[i].TotalReceitas == 0 && janela[i].TotalDespesas == 0;
            if (!vazio)
            {
                i++;
                continue;
            }

            var inicio = i;
            while (i < janela.Count && janela[i].TotalReceitas == 0 && janela[i].TotalDespesas == 0)
                i++;

            var primeiro = janela[inicio];
            var ultimo = janela[i - 1];
            var rotuloInicio = RotuloMes(primeiro.Mes, primeiro.Ano, cultura);
            var rotuloFim = RotuloMes(ultimo.Mes, ultimo.Ano, cultura);
            notas.Add(inicio == i - 1
                ? $"Sem lançamentos em {rotuloInicio}"
                : $"Sem lançamentos de {rotuloInicio} a {rotuloFim}");
        }

        return notas.Count == 0 ? null : string.Join(" · ", notas);
    }

    public static int PassoEixo(decimal maximo)
    {
        if (maximo <= 0)
            return 1;

        var passo = TetoBonito((double)maximo / 4d);
        while (passo > 0 && (double)maximo / passo > 4d)
            passo = ProximoBonito(passo);

        return Math.Max(1, passo);
    }

    private static int TetoBonito(double valor)
    {
        if (valor <= 1)
            return 1;

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(valor)));
        var normalizado = valor / magnitude;
        var bonito = normalizado <= 1 ? 1d : normalizado <= 2 ? 2d : normalizado <= 5 ? 5d : 10d;
        var passo = bonito * magnitude;
        if (passo > int.MaxValue)
            return int.MaxValue;
        return Math.Max(1, (int)Math.Round(passo, MidpointRounding.AwayFromZero));
    }

    private static int ProximoBonito(int passo)
    {
        if (passo >= int.MaxValue / 2)
            return int.MaxValue;

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(passo)));
        var normalizado = passo / magnitude;
        double proximo;
        if (normalizado < 2)
            proximo = 2 * magnitude;
        else if (normalizado < 5)
            proximo = 5 * magnitude;
        else
            proximo = 10 * magnitude;

        return proximo > int.MaxValue ? int.MaxValue : (int)Math.Round(proximo, MidpointRounding.AwayFromZero);
    }

    private static string RotuloMes(int mes, int ano, CultureInfo cultura)
        => new DateTime(ano, mes, 1).ToString("MMM/yy", cultura).Replace(".", "");

    private static string TextoContagem(int quantidade)
        => quantidade == 1 ? "1 no limite" : $"{quantidade} no limite";

    private static string TextoPercentualCurto(decimal percentual, CultureInfo cultura)
    {
        var arred = Math.Round(percentual, 1, MidpointRounding.AwayFromZero);
        return arred == decimal.Truncate(arred)
            ? arred.ToString("0", cultura) + "%"
            : arred.ToString("0.0", cultura) + "%";
    }

    private static string TextoMoeda(decimal valor, CultureInfo cultura, bool sinalNegativo)
    {
        if (sinalNegativo && valor < 0)
            return "− " + Math.Abs(valor).ToString("C", cultura);
        return valor.ToString("C", cultura);
    }

    private static (double Visual, bool Critico) Barra(decimal? percentual)
    {
        if (percentual is null || percentual <= 0)
            return (0d, false);
        if (percentual >= 100m)
            return (100d, percentual > 100m);
        return ((double)percentual.Value, false);
    }

    private sealed record CategoriasMontadas(
        decimal Total,
        IReadOnlyList<FatiaCategoria> Visiveis,
        IReadOnlyList<FatiaCategoria> Ocultas,
        FatiaCategoria? Outras,
        IReadOnlyList<FatiaCategoria> Barra,
        string Acessivel);

    private sealed record MesesMontados(
        IReadOnlyList<MesComparacao> Meses,
        bool DestacarUltimo,
        string? Nota,
        int Passo,
        string Acessivel);
}

public sealed record EntradaPainelInicio(
    PeriodoResumo Periodo,
    ResumoMensalDto Mes,
    ResumoAcumuladoDto Acumulado,
    IReadOnlyList<EntradaCategoria> Categorias,
    IReadOnlyList<TendenciaMensalDto> Tendencia,
    IReadOnlyList<AlertaOrcamentoDto> Alertas,
    IReadOnlyList<Meta> Metas,
    DateTime Hoje,
    decimal LimiarOrcamento = PainelInicio.LimiarOrcamentoPadrao);

public readonly record struct EntradaCategoria(string Nome, decimal Total);

public sealed record LancamentoCategoria(string Categoria, decimal Valor, IReadOnlyList<EntradaCategoria>? Itens);

public sealed record LinhaOrcamentoResumo(
    string Nome,
    decimal Percentual,
    string TextoPercentual,
    string TextoChip,
    bool Estourou,
    double Progresso,
    string TextoDetalhe,
    string TextoAlerta);

public sealed record LinhaMetaResumo(
    int Id,
    string Nome,
    decimal Percentual,
    string TextoPercentual,
    double Progresso,
    bool Concluida);

public sealed record FatiaCategoria(
    string Nome,
    decimal Total,
    string TextoTotal,
    decimal Percentual,
    string TextoPercentual,
    double LarguraRelativa,
    int IndicePaleta,
    bool EhAgrupamento,
    int Quantidade)
{
    public double LarguraPilha { get; init; }
}

public sealed record MesComparacao(
    int Mes,
    int Ano,
    string Rotulo,
    decimal Receitas,
    decimal Despesas,
    bool Atual);

public sealed record LinhaNoticia(string Veiculo, string Tempo, string Titulo, string Link);
