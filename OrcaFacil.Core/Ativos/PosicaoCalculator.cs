using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Ativos;

/// <summary>
/// Reconstrói a posição na ordem data e, no mesmo dia, Id.
/// Compra: custo inclui preço e custos; preço médio = custo / quantidade.
/// Venda: só reduz a quantidade; o preço médio permanece. Venda acima da quantidade naquela data é recusada.
/// </summary>
public static class PosicaoCalculator
{
    public static PosicaoCalculada Calcular(IEnumerable<MovimentoPosicao> operacoes)
    {
        decimal quantidade = 0m;
        decimal custo = 0m;

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

            if (operacao.Quantidade == quantidade)
            {
                quantidade = 0m;
                custo = 0m;
                continue;
            }

            var precoMedio = custo / quantidade;
            custo -= operacao.Quantidade * precoMedio;
            quantidade -= operacao.Quantidade;
        }

        return new PosicaoCalculada
        {
            Quantidade = quantidade,
            PrecoMedio = quantidade == 0m ? 0m : custo / quantidade,
            CustoTotal = custo
        };
    }
}
