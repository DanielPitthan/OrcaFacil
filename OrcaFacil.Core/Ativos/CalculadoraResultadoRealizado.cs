using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Ativos;

/// <summary>Resultado de uma venda, já arredondado. Zero não vira lançamento.</summary>
public sealed record ResultadoVenda(int Id, DateTime Data, decimal Resultado);

/// <summary>Posição reconstituída e o resultado de cada venda, na mesma ordem do cálculo.</summary>
public sealed class PosicaoComResultado
{
    public required PosicaoCalculada Posicao { get; init; }
    public IReadOnlyList<ResultadoVenda> Vendas { get; init; } = [];
}

/// <summary>
/// Único cálculo de posição e de resultado realizado da renda variável.
/// A ordem é a data e, no mesmo dia, o Id. Operação nova (Id 0) fica por último.
/// Compra: o custo soma quantidade × preço + custos. Venda: o preço médio não muda.
/// O resultado da venda usa o Valor da Operação informado, menos o custo baixado
/// e, se a regra da data mandar, menos os custos.
/// Venda total baixa o custo restante, sem sobra. O arredondamento afasta do zero;
/// o número de casas vem da regra vigente. A alíquota da regra não entra na conta.
/// </summary>
public static class CalculadoraResultadoRealizado
{
    public static PosicaoComResultado Calcular(
        IEnumerable<MovimentoPosicao> operacoes,
        Func<DateTime, RegraResultado>? regraDaData = null)
    {
        decimal quantidade = 0m;
        decimal custo = 0m;
        var vendas = new List<ResultadoVenda>();

        foreach (var operacao in operacoes.OrderBy(o => o.Data.Date).ThenBy(o => o.Id == 0 ? int.MaxValue : o.Id))
        {
            if (operacao.Quantidade <= 0)
                throw new InvalidOperationException("A quantidade deve ser maior que zero.");
            if (operacao.PrecoUnitario <= 0)
                throw new InvalidOperationException("O preço deve ser maior que zero.");
            if (operacao.Custos < 0)
                throw new InvalidOperationException("Os custos não podem ser negativos.");

            if (operacao.Tipo == TipoOperacao.Compra)
            {
                custo += operacao.Quantidade * operacao.PrecoUnitario + operacao.Custos;
                quantidade += operacao.Quantidade;
                continue;
            }

            if (operacao.Quantidade > quantidade)
            {
                throw new InvalidOperationException(
                    $"A venda de {operacao.Quantidade:0.########} em {operacao.Data:dd/MM/yyyy} excede a quantidade em carteira ({quantidade:0.########}).");
            }

            decimal custoBaixado;
            if (operacao.Quantidade == quantidade)
            {
                custoBaixado = custo;
                quantidade = 0m;
                custo = 0m;
            }
            else
            {
                var precoMedio = custo / quantidade;
                custoBaixado = operacao.Quantidade * precoMedio;
                custo -= custoBaixado;
                quantidade -= operacao.Quantidade;
            }

            if (regraDaData is null)
                continue;

            var regra = regraDaData(operacao.Data);
            var deducao = regra.CustosReduzemResultado ? operacao.Custos : 0m;
            if (operacao.ValorOperacao <= 0)
                throw new InvalidOperationException("O valor da operação deve ser maior que zero.");

            var bruto = operacao.ValorOperacao - custoBaixado - deducao;
            vendas.Add(new ResultadoVenda(operacao.Id, operacao.Data.Date, Arredondar(bruto, regra.CasasDecimais)));
        }

        return new PosicaoComResultado
        {
            Posicao = new PosicaoCalculada
            {
                Quantidade = quantidade,
                PrecoMedio = quantidade == 0m ? 0m : custo / quantidade,
                CustoTotal = custo
            },
            Vendas = vendas
        };
    }

    /// <summary>Afasto do zero. A quantidade de casas é a da regra vigente na data da venda.</summary>
    public static decimal Arredondar(decimal valor, int casas)
    {
        if (casas is < 0 or > 8)
            throw new InvalidOperationException("A regra fiscal de casas decimais está fora do intervalo.");

        return Math.Round(valor, casas, MidpointRounding.AwayFromZero);
    }
}
