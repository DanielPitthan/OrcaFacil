using System.Globalization;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Cesta;

public static class CestaDespesaRegras
{
    public const string NomeCategoria = "Cesta Básica";

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    public static decimal Dinheiro(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    public static decimal CustoDeBolso(decimal total, decimal beneficio) => Dinheiro(total) - Dinheiro(beneficio);

    public static decimal AproveitamentoPercentual(decimal total, decimal beneficio)
        => total <= 0 ? 0 : Math.Round(beneficio / total * 100m, 2, MidpointRounding.AwayFromZero);

    /// <summary>Reais de cesta obtidos por real de benefício.</summary>
    public static decimal Rendimento(decimal total, decimal beneficio)
        => beneficio <= 0 ? 0 : Math.Round(total / beneficio, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// O saldo é o que sobra depois do benefício. Se dinheiro e cartão vierem zerados, o saldo fica em dinheiro.
    /// </summary>
    public static PagamentoSaldo NormalizarPagamento(decimal saldo, decimal dinheiro, decimal cartao)
    {
        saldo = Dinheiro(saldo);
        dinheiro = Dinheiro(dinheiro);
        cartao = Dinheiro(cartao);

        if (saldo <= 0)
            return new PagamentoSaldo(0, 0);

        if (dinheiro == 0 && cartao == 0)
            return new PagamentoSaldo(saldo, 0);

        if (dinheiro < 0 || cartao < 0)
            throw new InvalidOperationException("Os valores do pagamento não podem ser negativos.");

        if (dinheiro + cartao != saldo)
            throw new InvalidOperationException("Dinheiro e cartão de crédito precisam somar o saldo da compra.");

        return new PagamentoSaldo(dinheiro, cartao);
    }

    public static void ValidarBeneficio(decimal total, decimal beneficio, TipoCartaoBeneficio? tipo)
    {
        if (beneficio < 0)
            throw new InvalidOperationException("O valor do benefício não pode ser negativo.");

        if (beneficio > total)
            throw new InvalidOperationException("O benefício não pode ser maior que o total da cesta.");

        if (beneficio > 0 && tipo is null)
            throw new InvalidOperationException("Informe o tipo do cartão de benefício.");
    }

    public static List<ParcelaRateio> Ratear(IReadOnlyList<ItemPrecoCategoria> itens, decimal beneficio)
    {
        var total = itens.Sum(i => i.Preco);
        var liquido = Dinheiro(total - beneficio);
        if (itens.Count == 0 || liquido <= 0 || total <= 0)
            return new List<ParcelaRateio>();

        var parcelas = itens.Select(item => new ParcelaRateio
        {
            Nome = item.Nome,
            CategoriaId = item.CategoriaId,
            Valor = Dinheiro(liquido * item.Preco / total)
        }).ToList();

        var diferenca = liquido - parcelas.Sum(p => p.Valor);
        if (diferenca != 0)
        {
            var indice = 0;
            for (var i = 1; i < itens.Count; i++)
            {
                if (itens[i].Preco > itens[indice].Preco)
                    indice = i;
            }

            parcelas[indice].Valor = Dinheiro(parcelas[indice].Valor + diferenca);
        }

        while (parcelas.Count > 1 && parcelas.Any(p => p.Valor < 0.01m))
        {
            var pequena = parcelas.OrderBy(p => p.Valor).First();
            parcelas.Remove(pequena);
            var maior = parcelas.OrderByDescending(p => p.Valor).First();
            maior.Valor = Dinheiro(maior.Valor + pequena.Valor);
        }

        return parcelas;
    }

    public static DespesaMontada Montar(
        string localNome,
        IReadOnlyList<ItemPrecoCategoria> itens,
        decimal beneficio,
        TipoCartaoBeneficio? tipo,
        int categoriaCestaId,
        decimal dinheiro = 0,
        decimal cartao = 0)
    {
        var total = Dinheiro(itens.Sum(i => i.Preco));
        beneficio = Dinheiro(beneficio);
        ValidarBeneficio(total, beneficio, tipo);
        var pagamento = NormalizarPagamento(CustoDeBolso(total, beneficio), dinheiro, cartao);

        var rateio = Ratear(itens, beneficio);
        var valor = Dinheiro(rateio.Sum(p => p.Valor));
        var linhas = rateio
            .Where(p => p.CategoriaId != categoriaCestaId && p.Valor >= 0.01m)
            .ToList();

        return new DespesaMontada
        {
            Gerar = valor >= 0.01m,
            Valor = valor,
            Descricao = Descricao(localNome),
            Observacao = MontarObservacao(total, beneficio, tipo, pagamento.Dinheiro, pagamento.Cartao),
            Itens = linhas,
            Dinheiro = pagamento.Dinheiro,
            Cartao = pagamento.Cartao
        };
    }

    public static string Descricao(string localNome)
    {
        var texto = $"Cesta básica — {localNome.Trim()}";
        return texto.Length <= 120 ? texto : texto[..120];
    }

    public static string MontarObservacao(
        decimal total,
        decimal beneficio,
        TipoCartaoBeneficio? tipo,
        decimal dinheiro = 0,
        decimal cartao = 0)
    {
        var linhas = new List<string> { $"Total da cesta: {total.ToString("C", Cultura)}" };
        if (beneficio > 0 && tipo is not null)
        {
            linhas.Add($"{tipo.Value.Rotulo()}: {beneficio.ToString("C", Cultura)} (desconto)");
            linhas.Add($"Custo de bolso: {CustoDeBolso(total, beneficio).ToString("C", Cultura)}");
            linhas.Add($"Aproveitamento do benefício: {AproveitamentoPercentual(total, beneficio).ToString("0.##", Cultura)}%");
        }

        if (dinheiro > 0)
            linhas.Add($"Dinheiro: {dinheiro.ToString("C", Cultura)}");
        if (cartao > 0)
            linhas.Add($"Cartão de crédito: {cartao.ToString("C", Cultura)}");

        var texto = string.Join('\n', linhas);
        return texto.Length <= 500 ? texto : texto[..500];
    }
}
