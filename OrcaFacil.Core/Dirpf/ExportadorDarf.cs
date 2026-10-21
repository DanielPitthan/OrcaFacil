using ClosedXML.Excel;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Planilha;

namespace OrcaFacil.Core.Dirpf;

/// <summary>
/// xlsx da apuração mensal e dos DARFs de um ano-base.
/// As abas nascem só aqui. Largura fixa, sem medir o conteúdo.
/// </summary>
public static class ExportadorDarf
{
    public const string AbaApuracao = "Apuração mensal";
    public const string AbaDarfs = "DARFs";

    public static IReadOnlyList<string> NomesDeAba { get; } = [AbaApuracao, AbaDarfs];

    public static void Exportar(
        AppDbContext contexto,
        int anoBase,
        Stream destino,
        IEnumerable<IrrfManual>? irrf = null)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(destino);

        var apuracao = ApuradorIrMensal.Apurar(contexto, anoBase, irrf);
        using var pasta = new XLWorkbook();
        GravarApuracao(pasta, apuracao.Meses);
        GravarDarfs(pasta, apuracao.Darfs);
        pasta.SaveAs(destino);
        if (destino.CanSeek)
            destino.Position = 0;
    }

    private static void GravarApuracao(XLWorkbook pasta, IReadOnlyList<LinhaApuracaoMensal> linhas)
    {
        var aba = PlanilhaPtBr.CriarAba(pasta, AbaApuracao);
        PlanilhaPtBr.GravarCabecalho(aba,
        [
            "Ano", "Mês", "Perfil", "Cesta", "Resultado", "Prejuízo compensado",
            "Prejuízo a compensar", "Base de cálculo", "Alíquota", "Imposto", "IRRF",
            "Imposto líquido", "Vendas de ações", "Ganho isento de ações",
            "Valor a levar para a declaração"
        ]);
        ExigirTeto(aba.Name, linhas.Count);

        for (var i = 0; i < linhas.Count; i++)
        {
            var linha = linhas[i];
            var numero = i + 2;
            PlanilhaPtBr.GravarNumero(PlanilhaPtBr.Celula(aba, numero, 1), linha.Ano);
            PlanilhaPtBr.GravarNumero(PlanilhaPtBr.Celula(aba, numero, 2), linha.Mes);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 3), linha.Perfil);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 4), linha.Cesta.Rotulo());
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, numero, 5), linha.Resultado);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, numero, 6), linha.PrejuizoCompensado);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, numero, 7), linha.PrejuizoACompensar);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, numero, 8), linha.BaseCalculo);
            PlanilhaPtBr.GravarNumero(PlanilhaPtBr.Celula(aba, numero, 9), linha.Aliquota);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, numero, 10), linha.Imposto);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, numero, 11), linha.Irrf);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, numero, 12), linha.ImpostoLiquido);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, numero, 13), linha.VendasAcoesComuns);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, numero, 14), linha.GanhoIsentoAcoes);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, numero, 15), linha.IrrfALevarParaDeclaracao);
        }
    }

    private static void GravarDarfs(XLWorkbook pasta, IReadOnlyList<LinhaDarf> linhas)
    {
        var aba = PlanilhaPtBr.CriarAba(pasta, AbaDarfs);
        PlanilhaPtBr.GravarCabecalho(aba,
        [
            "Ano", "Mês", "Perfil", "Código", "Imposto do mês", "Acumulado anterior",
            "Saldo acumulado", "Valor do DARF", "Vencimento"
        ]);
        ExigirTeto(aba.Name, linhas.Count);

        for (var i = 0; i < linhas.Count; i++)
        {
            var linha = linhas[i];
            var numero = i + 2;
            PlanilhaPtBr.GravarNumero(PlanilhaPtBr.Celula(aba, numero, 1), linha.Ano);
            PlanilhaPtBr.GravarNumero(PlanilhaPtBr.Celula(aba, numero, 2), linha.Mes);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 3), linha.Perfil);
            PlanilhaPtBr.GravarNumero(PlanilhaPtBr.Celula(aba, numero, 4), linha.CodigoReceita);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, numero, 5), linha.ImpostoDoMes);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, numero, 6), linha.AcumuladoAnterior);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, numero, 7), linha.SaldoAcumulado);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, numero, 8), linha.Valor);
            PlanilhaPtBr.GravarData(PlanilhaPtBr.Celula(aba, numero, 9), linha.Vencimento);
        }
    }

    private static void ExigirTeto(string aba, int linhas)
    {
        var teto = PlanilhaPtBr.MaximoLinhas - 1;
        if (linhas > teto)
            throw new InvalidOperationException(
                $"A aba {aba} tem {linhas} linhas de dados. O Excel aceita no máximo {teto} além do cabeçalho.");
    }
}
