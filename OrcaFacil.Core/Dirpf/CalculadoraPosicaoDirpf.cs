using OrcaFacil.Core;
using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Dirpf;

/// <summary>
/// Posição de Bens e Direitos em uma data de corte.
/// A compra soma quantidade × preço unitário e os custos, como o lançamento travado.
/// A venda parcial baixa a quantidade pelo custo médio e não altera o médio.
/// A venda total zera o custo, sem sobra. Custos da venda não mudam o que ficou.
/// A ordem é a data e, no mesmo dia, o Id. Operação depois do corte fica de fora.
/// O arredondamento da ficha acontece depois, com as casas da regra vigente.
/// </summary>
public static class CalculadoraPosicaoDirpf
{
    public static PosicaoCalculada Ate(IEnumerable<MovimentoPosicao> operacoes, DateTime dataCorte)
    {
        var dia = dataCorte.Date;
        decimal quantidade = 0m;
        decimal custo = 0m;

        foreach (var operacao in OrdemPorDataEId.Ordenar(
                     operacoes.Where(item => item.Data.Date <= dia),
                     item => item.Data,
                     item => item.Id))
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

            if (operacao.Tipo != TipoOperacao.Venda)
                throw new InvalidOperationException($"Operação desconhecida: {operacao.Tipo}.");

            if (operacao.Quantidade > quantidade)
            {
                throw new InvalidOperationException(
                    $"A venda de {operacao.Quantidade} em {operacao.Data:dd/MM/yyyy} excede a quantidade em carteira ({quantidade}).");
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
