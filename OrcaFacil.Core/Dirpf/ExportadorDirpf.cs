using ClosedXML.Excel;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Planilha;

namespace OrcaFacil.Core.Dirpf;

/// <summary>
/// xlsx da DIRPF de um ano-base. As abas nascem só aqui: a planilha geral não as recebe.
/// Largura fixa, sem medir o conteúdo. O arquivo não é a planilha de todos os dados.
/// </summary>
public static class ExportadorDirpf
{
    public const string AbaBens = "Bens e Direitos";
    public const string AbaRendimentosIsentos = "Rendimentos isentos";

    public static IReadOnlyList<string> NomesDeAba { get; } = [AbaBens, AbaRendimentosIsentos];

    public static void Exportar(AppDbContext contexto, int anoBase, Stream destino)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(destino);

        var dirpf = MontadorDirpf.Montar(contexto, anoBase);
        using var pasta = new XLWorkbook();
        GravarBens(pasta, dirpf.Bens);
        GravarIsentos(pasta, dirpf.RendimentosIsentos);
        pasta.SaveAs(destino);
        if (destino.CanSeek)
            destino.Position = 0;
    }

    private static void GravarBens(XLWorkbook pasta, IReadOnlyList<LinhaBemDirpf> linhas)
    {
        var aba = PlanilhaPtBr.CriarAba(pasta, AbaBens);
        PlanilhaPtBr.GravarCabecalho(aba,
        [
            "Ano-base", "Perfil", "Ticker", "Nome", "Tipo", "Grupo", "Código",
            "Descrição do código", "CNPJ", "Quantidade", "Custo de aquisição", "Preço médio", "Discriminação"
        ]);
        ExigirTeto(aba.Name, linhas.Count);

        for (var i = 0; i < linhas.Count; i++)
        {
            var linha = linhas[i];
            var numero = i + 2;
            PlanilhaPtBr.GravarNumero(PlanilhaPtBr.Celula(aba, numero, 1), linha.AnoBase);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 2), linha.Perfil);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 3), linha.Ticker);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 4), linha.Nome);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 5), linha.Tipo);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 6), linha.Grupo);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 7), linha.Codigo);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 8), linha.DescricaoCodigo);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 9), linha.Cnpj);
            PlanilhaPtBr.GravarNumero(PlanilhaPtBr.Celula(aba, numero, 10), linha.Quantidade);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, numero, 11), linha.CustoAquisicao);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, numero, 12), linha.PrecoMedio);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 13), linha.Discriminacao);
        }
    }

    private static void GravarIsentos(XLWorkbook pasta, IReadOnlyList<LinhaRendimentoIsentoDirpf> linhas)
    {
        var aba = PlanilhaPtBr.CriarAba(pasta, AbaRendimentosIsentos);
        PlanilhaPtBr.GravarCabecalho(aba,
        [
            "Ano-base", "Perfil", "Ticker", "Nome", "Natureza", "Código",
            "Descrição do código", "CNPJ", "Valor", "Descrição"
        ]);
        ExigirTeto(aba.Name, linhas.Count);

        for (var i = 0; i < linhas.Count; i++)
        {
            var linha = linhas[i];
            var numero = i + 2;
            PlanilhaPtBr.GravarNumero(PlanilhaPtBr.Celula(aba, numero, 1), linha.AnoBase);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 2), linha.Perfil);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 3), linha.Ticker);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 4), linha.Nome);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 5), linha.Natureza);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 6), linha.Codigo);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 7), linha.DescricaoCodigo);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 8), linha.Cnpj);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, numero, 9), linha.Valor);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 10), linha.Descricao);
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
