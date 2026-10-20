using System.Globalization;
using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.RendaFixa;

namespace OrcaFacil.Core.B3;

/// <summary>
/// Meses em que a importação gera lançamento: venda e resgate com resultado
/// diferente de zero, e todo provento. Compra e aplicação ficam de fora.
/// A conta usa só as linhas gravadas, na mesma ordem da gravação, com custos
/// e IR zerados e a regra fiscal de sistema (2 casas).
/// </summary>
public sealed class DestinoLancamentosImportacao
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private DestinoLancamentosImportacao(int quantidade, IReadOnlyList<DateTime> meses)
    {
        Quantidade = quantidade;
        Meses = meses;
        MesMaisRecente = meses.Count == 0 ? null : meses[^1];
        Texto = MontarTexto(quantidade, meses);
        Rota = MesMaisRecente is DateTime mes
            ? $"/lancamentos?mes={mes:yyyy-MM}"
            : "/ativos";
    }

    public int Quantidade { get; }

    /// <summary>Primeiro dia de cada mês, do mais antigo ao mais recente, sem repetir.</summary>
    public IReadOnlyList<DateTime> Meses { get; }

    public DateTime? MesMaisRecente { get; }

    public string Texto { get; }

    public string Rota { get; }

    public static DestinoLancamentosImportacao Calcular(IEnumerable<LinhaPreviaExtratoB3> gravadas)
    {
        ArgumentNullException.ThrowIfNull(gravadas);

        var linhas = gravadas
            .Where(linha => linha.Comando is not null)
            .OrderBy(linha => linha.Comando!.Data)
            .ThenBy(linha => Prioridade(linha.Comando!))
            .ThenBy(linha => linha.Numero)
            .ToList();

        var datas = new List<DateTime>();
        foreach (var linha in linhas)
        {
            if (linha.Comando!.Destino == DestinoImportacaoB3.Provento)
                datas.Add(linha.Comando.Data.Date);
        }

        datas.AddRange(DatasDeVenda(linhas));
        datas.AddRange(DatasDeResgate(linhas));

        var meses = datas
            .Select(data => new DateTime(data.Year, data.Month, 1))
            .Distinct()
            .OrderBy(mes => mes)
            .ToList();

        return new DestinoLancamentosImportacao(datas.Count, meses);
    }

    private static List<DateTime> DatasDeVenda(IReadOnlyList<LinhaPreviaExtratoB3> linhas)
    {
        var datas = new List<DateTime>();
        var regras = new TabelaRegrasFiscais(CatalogoRegrasFiscais.Linhas());
        foreach (var grupo in linhas
            .Where(linha => linha.Comando!.Destino == DestinoImportacaoB3.OperacaoAtivo)
            .GroupBy(linha => linha.Comando!.Ticker, StringComparer.OrdinalIgnoreCase))
        {
            var comandos = grupo.Select(linha => linha.Comando!).ToList();
            var tipo = comandos[0].TipoAtivo;
            var movimentos = comandos.Select((comando, indice) => new MovimentoPosicao
            {
                Id = indice + 1,
                Data = comando.Data,
                Tipo = comando.TipoOperacao,
                Quantidade = comando.Quantidade,
                PrecoUnitario = comando.PrecoUnitario,
                ValorOperacao = comando.Valor,
                Custos = 0
            }).ToList();

            IReadOnlyList<ResultadoVenda> vendas;
            try
            {
                vendas = CalculadoraResultadoRealizado
                    .Calcular(movimentos, data => regras.Para(data, tipo))
                    .Vendas;
            }
            catch (InvalidOperationException)
            {
                datas.AddRange(comandos
                    .Where(comando => comando.TipoOperacao == TipoOperacao.Venda)
                    .Select(comando => comando.Data.Date));
                continue;
            }

            datas.AddRange(vendas.Where(venda => venda.Resultado != 0m).Select(venda => venda.Data.Date));
        }

        return datas;
    }

    private static List<DateTime> DatasDeResgate(IReadOnlyList<LinhaPreviaExtratoB3> linhas)
    {
        var datas = new List<DateTime>();
        foreach (var grupo in linhas
            .Where(linha => linha.Comando!.Destino == DestinoImportacaoB3.RendaFixa)
            .GroupBy(linha => (linha.Comando!.Ticker, linha.Comando.Corretora)))
        {
            var comandos = grupo.Select(linha => linha.Comando!).ToList();
            var movimentos = comandos.Select((comando, indice) => new MovimentoRendaFixa
            {
                Id = indice + 1,
                Data = comando.Data,
                Tipo = comando.TipoRendaFixa,
                Quantidade = comando.Quantidade,
                Valor = comando.Valor,
                Custos = 0,
                IrRetido = 0,
                Codigo = comando.Ticker
            }).ToList();

            IReadOnlyList<ResultadoResgateRendaFixa> resgates;
            try
            {
                resgates = CalculadoraRendaFixa.Calcular(movimentos).Resgates;
            }
            catch (InvalidOperationException)
            {
                datas.AddRange(comandos
                    .Where(comando => comando.TipoRendaFixa == TipoOperacaoRendaFixa.Resgate)
                    .Select(comando => comando.Data.Date));
                continue;
            }

            datas.AddRange(resgates.Where(resgate => resgate.Resultado != 0m).Select(resgate => resgate.Data.Date));
        }

        return datas;
    }

    private static int Prioridade(ComandoImportacaoB3 comando) => comando.Destino switch
    {
        DestinoImportacaoB3.OperacaoAtivo when comando.TipoOperacao == TipoOperacao.Compra => 0,
        DestinoImportacaoB3.RendaFixa when comando.TipoRendaFixa == TipoOperacaoRendaFixa.Aplicacao => 0,
        DestinoImportacaoB3.Provento => 2,
        _ => 1
    };

    private static string MontarTexto(int quantidade, IReadOnlyList<DateTime> meses)
    {
        if (quantidade == 0)
            return "Nenhum lançamento foi gerado.";

        var substantivo = quantidade == 1 ? "lançamento" : "lançamentos";
        return $"{quantidade} {substantivo} em {Juntar(meses)}.";
    }

    private static string Juntar(IReadOnlyList<DateTime> meses)
    {
        var textos = meses.Select(FormatarMes).ToList();
        if (textos.Count <= 1)
            return textos.Count == 0 ? "" : textos[0];

        return string.Join(", ", textos.Take(textos.Count - 1)) + " e " + textos[^1];
    }

    private static string FormatarMes(DateTime mes)
    {
        var nome = PtBr.DateTimeFormat.AbbreviatedMonthNames[mes.Month - 1].TrimEnd('.');
        return $"{nome}/{mes:yyyy}";
    }
}
