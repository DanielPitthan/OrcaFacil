using System.Globalization;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Cesta;

public static class CestaComparacaoRegras
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    public static ComparativoCesta Montar(IReadOnlyList<CompraComparacao> compras, int mes, int ano)
    {
        var ordenadas = compras.OrderBy(c => c.Data).ThenBy(c => c.Id).ToList();
        var evolucao = CalcularEvolucao(ordenadas, mes, ano);
        var beneficios = CalcularBeneficios(ordenadas);

        if (ordenadas.Count < 2)
        {
            return new ComparativoCesta
            {
                Disponivel = false,
                Mensagem = "Registre a segunda compra para ver onde está mais barato e a economia em valor e percentual.",
                Evolucao = evolucao,
                Beneficios = beneficios
            };
        }

        var precos = UltimosPrecos(ordenadas);
        var locais = precos
            .Select(p => (p.LocalId, p.LocalNome))
            .Distinct()
            .OrderBy(l => l.LocalNome)
            .ToList();
        var produtos = precos
            .Select(p => (p.ProdutoId, p.ProdutoNome))
            .Distinct()
            .OrderBy(p => p.ProdutoNome)
            .ToList();

        var linhas = produtos.Select(produto => MontarLinha(produto.ProdutoId, produto.ProdutoNome, locais, precos)).ToList();
        var comparaveis = linhas.Where(l => l.Precos.All(p => p.Preco is not null)).ToList();
        var totais = MontarTotais(locais, comparaveis);
        var mensagem = MontarMensagem(ordenadas, locais.Count, comparaveis.Count, totais);

        return new ComparativoCesta
        {
            Disponivel = true,
            Mensagem = mensagem,
            Produtos = linhas,
            Locais = totais,
            Beneficios = beneficios,
            Evolucao = evolucao
        };
    }

    public static List<CompraRascunho> Rascunhos(IReadOnlyList<CompraComparacao> compras, int mes, int ano)
    {
        var anterior = new DateTime(ano, mes, 1).AddMonths(-1);
        var locaisDoMes = compras
            .Where(c => c.Data.Month == mes && c.Data.Year == ano)
            .Select(c => c.LocalId)
            .ToHashSet();

        return compras
            .Where(c => c.Data.Month == anterior.Month && c.Data.Year == anterior.Year)
            .GroupBy(c => c.LocalId)
            .Select(g => g.OrderBy(c => c.Data).ThenBy(c => c.Id).Last())
            .Where(c => !locaisDoMes.Contains(c.LocalId))
            .OrderBy(c => c.LocalNome)
            .Select(c => new CompraRascunho(
                c.LocalId,
                c.LocalNome,
                new DateTime(ano, mes, Math.Min(c.Data.Day, DateTime.DaysInMonth(ano, mes))),
                c.ValorBeneficio,
                c.TipoBeneficio,
                c.Itens.Select(i => new ItemRascunho(i.ProdutoId, i.ProdutoNome, i.Preco)).ToList(),
                c.ValorDinheiro,
                c.ValorCartaoCredito))
            .ToList();
    }

    private static EvolucaoCesta CalcularEvolucao(IReadOnlyList<CompraComparacao> compras, int mes, int ano)
    {
        var anterior = new DateTime(ano, mes, 1).AddMonths(-1);
        var totalMes = SomarMes(compras, mes, ano);
        var totalAnterior = SomarMes(compras, anterior.Month, anterior.Year);
        var temAnterior = compras.Any(c => c.Data.Month == anterior.Month && c.Data.Year == anterior.Year);
        var diferenca = totalMes - totalAnterior;
        var percentual = totalAnterior == 0 ? 0 : CestaDespesaRegras.AproveitamentoPercentual(totalAnterior, Math.Abs(diferenca));

        string? mensagem = null;
        if (temAnterior && compras.Any(c => c.Data.Month == mes && c.Data.Year == ano))
        {
            mensagem = diferenca switch
            {
                < 0 => $"Neste mês a cesta custou {Moeda(Math.Abs(diferenca))} a menos que no mês anterior ({Percentual(percentual)} de economia).",
                > 0 => $"Neste mês a cesta custou {Moeda(diferenca)} a mais que no mês anterior ({Percentual(percentual)}).",
                _ => "Neste mês a cesta ficou no mesmo valor do mês anterior."
            };
        }

        return new EvolucaoCesta
        {
            TemMesAnterior = temAnterior,
            TotalMes = totalMes,
            TotalMesAnterior = totalAnterior,
            DiferencaReais = diferenca,
            DiferencaPercentual = percentual,
            Mensagem = mensagem
        };
    }

    private static List<MetricaBeneficio> CalcularBeneficios(IReadOnlyList<CompraComparacao> compras)
    {
        var ultimas = compras
            .GroupBy(c => c.LocalId)
            .Select(g => g.OrderBy(c => c.Data).ThenBy(c => c.Id).Last())
            .Where(c => c.ValorBeneficio > 0 && c.TipoBeneficio is not null)
            .ToList();

        var metricas = ultimas.Select(compra =>
        {
            var total = CestaDespesaRegras.Dinheiro(compra.Itens.Sum(i => i.Preco));
            return new MetricaBeneficio
            {
                LocalId = compra.LocalId,
                LocalNome = compra.LocalNome,
                Tipo = compra.TipoBeneficio!.Value,
                ValorBeneficio = compra.ValorBeneficio,
                TotalCesta = total,
                CustoDeBolso = CestaDespesaRegras.CustoDeBolso(total, compra.ValorBeneficio),
                AproveitamentoPercentual = CestaDespesaRegras.AproveitamentoPercentual(total, compra.ValorBeneficio),
                Rendimento = CestaDespesaRegras.Rendimento(total, compra.ValorBeneficio)
            };
        }).ToList();

        var maior = metricas.Count == 0 ? 0 : metricas.Max(m => m.Rendimento);
        return metricas
            .Select(m => new MetricaBeneficio
            {
                LocalId = m.LocalId,
                LocalNome = m.LocalNome,
                Tipo = m.Tipo,
                ValorBeneficio = m.ValorBeneficio,
                TotalCesta = m.TotalCesta,
                CustoDeBolso = m.CustoDeBolso,
                AproveitamentoPercentual = m.AproveitamentoPercentual,
                Rendimento = m.Rendimento,
                RendeMais = metricas.Count >= 2 && m.Rendimento == maior
            })
            .OrderByDescending(m => m.Rendimento)
            .ThenBy(m => m.LocalNome)
            .ToList();
    }

    private static List<PrecoRegistrado> UltimosPrecos(IReadOnlyList<CompraComparacao> compras)
    {
        var mapa = new Dictionary<(int Produto, int Local), PrecoRegistrado>();
        foreach (var compra in compras)
        {
            foreach (var item in compra.Itens)
            {
                mapa[(item.ProdutoId, compra.LocalId)] = new PrecoRegistrado
                {
                    ProdutoId = item.ProdutoId,
                    ProdutoNome = item.ProdutoNome,
                    LocalId = compra.LocalId,
                    LocalNome = compra.LocalNome,
                    Preco = item.Preco
                };
            }
        }

        return mapa.Values.ToList();
    }

    private static LinhaComparativoProduto MontarLinha(
        int produtoId,
        string produtoNome,
        IReadOnlyList<(int LocalId, string LocalNome)> locais,
        IReadOnlyList<PrecoRegistrado> precos)
    {
        var colunas = locais.Select(local =>
        {
            var registrado = precos.FirstOrDefault(p => p.ProdutoId == produtoId && p.LocalId == local.LocalId);
            return new PrecoNoLocal
            {
                LocalId = local.LocalId,
                LocalNome = local.LocalNome,
                Preco = registrado?.Preco
            };
        }).ToList();

        var comPreco = colunas.Where(p => p.Preco is not null).ToList();
        decimal? economia = null;
        decimal? economiaPct = null;
        if (comPreco.Count >= 2)
        {
            var minimo = comPreco.Min(p => p.Preco!.Value);
            var maximo = comPreco.Max(p => p.Preco!.Value);
            economia = maximo - minimo;
            economiaPct = maximo == 0 ? 0 : CestaDespesaRegras.AproveitamentoPercentual(maximo, economia.Value);
            foreach (var coluna in colunas.Where(p => p.Preco == minimo))
            {
                coluna.MaisBarato = true;
            }
        }

        return new LinhaComparativoProduto
        {
            ProdutoId = produtoId,
            Nome = produtoNome,
            Precos = colunas,
            EconomiaReais = economia,
            EconomiaPercentual = economiaPct
        };
    }

    private static List<TotalLocalCesta> MontarTotais(
        IReadOnlyList<(int LocalId, string LocalNome)> locais,
        IReadOnlyList<LinhaComparativoProduto> comparaveis)
    {
        var totais = locais.Select(local =>
        {
            decimal? total = comparaveis.Count == 0
                ? null
                : comparaveis.Sum(linha => linha.Precos.First(p => p.LocalId == local.LocalId).Preco!.Value);
            return new TotalLocalCesta
            {
                LocalId = local.LocalId,
                Nome = local.LocalNome,
                TotalComparavel = total
            };
        }).ToList();

        var comTotal = totais.Where(t => t.TotalComparavel is not null).ToList();
        if (comTotal.Count < 2)
            return totais;

        var minimo = comTotal.Min(t => t.TotalComparavel!.Value);
        var maximo = comTotal.Max(t => t.TotalComparavel!.Value);
        return totais.Select(t =>
        {
            if (t.TotalComparavel is null)
                return t;

            var economia = maximo - t.TotalComparavel.Value;
            return new TotalLocalCesta
            {
                LocalId = t.LocalId,
                Nome = t.Nome,
                TotalComparavel = t.TotalComparavel,
                MaisBarato = t.TotalComparavel == minimo,
                EconomiaReais = economia,
                EconomiaPercentual = maximo == 0 ? 0 : CestaDespesaRegras.AproveitamentoPercentual(maximo, economia)
            };
        }).ToList();
    }

    private static string MontarMensagem(
        IReadOnlyList<CompraComparacao> compras,
        int quantidadeLocais,
        int produtosComparaveis,
        IReadOnlyList<TotalLocalCesta> totais)
    {
        if (quantidadeLocais >= 2 && produtosComparaveis > 0)
        {
            var ranked = totais.Where(t => t.TotalComparavel is not null).OrderBy(t => t.TotalComparavel).ToList();
            var barato = ranked[0];
            var caro = ranked[^1];
            if (barato.TotalComparavel == caro.TotalComparavel)
                return "Os locais estão empatados no total da cesta comparável.";

            var economia = caro.TotalComparavel!.Value - barato.TotalComparavel!.Value;
            var percentual = CestaDespesaRegras.AproveitamentoPercentual(caro.TotalComparavel.Value, economia);
            return $"O {barato.Nome} está {Percentual(percentual)} mais barato que o {caro.Nome} (economia de {Moeda(economia)} na cesta comparável).";
        }

        if (quantidadeLocais >= 2)
            return "Há compras em mais de um local, mas nenhum produto aparece em todos. Compare o preço de cada produto abaixo.";

        var local = compras.OrderBy(c => c.Data).ThenBy(c => c.Id).GroupBy(c => c.LocalId).First();
        var serie = local.OrderBy(c => c.Data).ThenBy(c => c.Id).ToList();
        var anterior = serie[^2];
        var atual = serie[^1];
        var totalAnterior = CestaDespesaRegras.Dinheiro(anterior.Itens.Sum(i => i.Preco));
        var totalAtual = CestaDespesaRegras.Dinheiro(atual.Itens.Sum(i => i.Preco));
        var diferenca = totalAnterior - totalAtual;
        if (diferenca == 0)
            return $"A compra mais recente no {atual.LocalNome} ficou no mesmo valor da anterior.";

        var pct = totalAnterior == 0 ? 0 : CestaDespesaRegras.AproveitamentoPercentual(totalAnterior, Math.Abs(diferenca));
        return diferenca > 0
            ? $"Na compra mais recente no {atual.LocalNome}, a cesta ficou {Percentual(pct)} mais barata que a anterior (economia de {Moeda(diferenca)})."
            : $"Na compra mais recente no {atual.LocalNome}, a cesta ficou {Percentual(pct)} mais cara que a anterior (aumento de {Moeda(Math.Abs(diferenca))}).";
    }

    private static decimal SomarMes(IReadOnlyList<CompraComparacao> compras, int mes, int ano)
        => CestaDespesaRegras.Dinheiro(compras
            .Where(c => c.Data.Month == mes && c.Data.Year == ano)
            .Sum(c => c.Itens.Sum(i => i.Preco)));

    private static string Moeda(decimal valor) => valor.ToString("C", Cultura);

    private static string Percentual(decimal valor) => $"{valor.ToString("0.##", Cultura)}%";

    private sealed class PrecoRegistrado
    {
        public int ProdutoId { get; init; }
        public string ProdutoNome { get; init; } = string.Empty;
        public int LocalId { get; init; }
        public string LocalNome { get; init; } = string.Empty;
        public decimal Preco { get; init; }
    }
}
