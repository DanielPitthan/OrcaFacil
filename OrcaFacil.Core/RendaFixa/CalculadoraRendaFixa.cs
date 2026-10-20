using System.Globalization;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.RendaFixa;

/// <summary>Operação já pronta para o cálculo, sem dependência do banco.</summary>
public sealed class MovimentoRendaFixa
{
    public int Id { get; init; }
    public DateTime Data { get; init; }
    public TipoOperacaoRendaFixa Tipo { get; init; }
    public decimal Quantidade { get; init; }

    /// <summary>Valor da operação. É a base do custo, não quantidade × preço.</summary>
    public decimal Valor { get; init; }

    public decimal Custos { get; init; }
    public decimal IrRetido { get; init; }

    /// <summary>Entra só na mensagem quando o resgate passa da posição.</summary>
    public string Codigo { get; init; } = string.Empty;
}

/// <summary>Resultado de um resgate depois do arredondamento a 2 casas. Zero não vira lançamento.</summary>
public sealed record ResultadoResgateRendaFixa(int Id, DateTime Data, decimal Resultado, decimal CustoBaixado);

/// <summary>Posição reconstituída e o resultado de cada resgate, na ordem em que foi processado.</summary>
public sealed class PosicaoRendaFixaCalculada
{
    public decimal Quantidade { get; init; }
    public decimal CustoMedio { get; init; }
    public decimal CustoTotal { get; init; }
    public IReadOnlyList<ResultadoResgateRendaFixa> Resgates { get; init; } = [];
}

/// <summary>
/// Reconstrói a posição na ordem da data. No mesmo dia a aplicação vem antes do resgate; o Id desempata.
/// O custo médio é a soma do valor das aplicações dividida pela quantidade. O resgate parcial baixa
/// essa soma na proporção da quantidade. O resgate total zera o custo restante, sem sobra de arredondamento.
/// Resultado = valor − custo baixado − custos − IR retido, em 2 casas, afastando do zero.
/// Nenhuma alíquota entra aqui: o IR é o valor informado na operação. IR regressivo, se vier, sai da tabela de vigência.
/// </summary>
public static class CalculadoraRendaFixa
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public static PosicaoRendaFixaCalculada Calcular(IEnumerable<MovimentoRendaFixa> operacoes)
    {
        decimal quantidade = 0m;
        decimal custo = 0m;
        var resgates = new List<ResultadoResgateRendaFixa>();

        foreach (var operacao in Ordenar(operacoes))
        {
            Validar(operacao);

            if (operacao.Tipo == TipoOperacaoRendaFixa.Aplicacao)
            {
                custo += operacao.Valor;
                quantidade += operacao.Quantidade;
                continue;
            }

            if (operacao.Quantidade > quantidade)
            {
                var codigo = string.IsNullOrWhiteSpace(operacao.Codigo) ? "do título" : operacao.Codigo;
                throw new InvalidOperationException(
                    $"O resgate de {Formatar(operacao.Quantidade)} em {operacao.Data:dd/MM/yyyy} excede a posição do título {codigo} ({Formatar(quantidade)}).");
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
                custoBaixado = custo * operacao.Quantidade / quantidade;
                custo -= custoBaixado;
                quantidade -= operacao.Quantidade;
            }

            var bruto = operacao.Valor - custoBaixado - operacao.Custos - operacao.IrRetido;
            resgates.Add(new ResultadoResgateRendaFixa(
                operacao.Id,
                operacao.Data.Date,
                Arredondar(bruto),
                custoBaixado));
        }

        return new PosicaoRendaFixaCalculada
        {
            Quantidade = quantidade,
            CustoMedio = quantidade == 0m ? 0m : custo / quantidade,
            CustoTotal = custo,
            Resgates = resgates
        };
    }

    /// <summary>2 casas, afastando do zero. 0,005 vira 0,01; resultado 0,00 não gera lançamento.</summary>
    public static decimal Arredondar(decimal valor)
        => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    private static IEnumerable<MovimentoRendaFixa> Ordenar(IEnumerable<MovimentoRendaFixa> operacoes)
        => operacoes
            .OrderBy(o => o.Data.Date)
            .ThenBy(o => o.Tipo == TipoOperacaoRendaFixa.Aplicacao ? 0 : 1)
            .ThenBy(o => o.Id == 0 ? int.MaxValue : o.Id);

    private static void Validar(MovimentoRendaFixa operacao)
    {
        if (operacao.Quantidade <= 0)
            throw new InvalidOperationException("A quantidade deve ser maior que zero.");
        if (operacao.Valor <= 0)
            throw new InvalidOperationException("O valor deve ser maior que zero.");
        if (operacao.Custos < 0)
            throw new InvalidOperationException("Os custos não podem ser negativos.");
        if (operacao.IrRetido < 0)
            throw new InvalidOperationException("O IR retido não pode ser negativo.");
    }

    private static string Formatar(decimal valor)
        => valor.ToString("0.########", PtBr);
}
