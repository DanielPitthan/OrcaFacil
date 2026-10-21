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
    public const string AbaRendimentosExclusivos = "Rendimentos exclusivos";
    public const string AbaRendaFixa = "Renda fixa";

    public static IReadOnlyList<string> NomesDeAba { get; } =
        [AbaBens, AbaRendimentosIsentos, AbaRendimentosExclusivos, AbaRendaFixa];

    public static void Exportar(AppDbContext contexto, int anoBase, Stream destino)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(destino);

        var dirpf = MontadorDirpf.Montar(contexto, anoBase);
        using var pasta = new XLWorkbook();
        GravarBens(pasta, dirpf.Bens);
        GravarIsentos(pasta, dirpf.RendimentosIsentos);
        GravarExclusivos(pasta, dirpf.RendimentosExclusivos);
        GravarRendaFixa(pasta, dirpf.RendaFixa);
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

    private static void GravarExclusivos(XLWorkbook pasta, IReadOnlyList<LinhaRendimentoExclusivoDirpf> linhas)
    {
        var aba = PlanilhaPtBr.CriarAba(pasta, AbaRendimentosExclusivos);
        PlanilhaPtBr.GravarCabecalho(aba,
        [
            "Ano-base", "Perfil", "Código", "Nome", "Natureza", "Código DIRPF",
            "Descrição do código", "CNPJ", "Valor líquido", "IR retido", "Descrição"
        ]);
        ExigirTeto(aba.Name, linhas.Count);

        for (var i = 0; i < linhas.Count; i++)
        {
            var linha = linhas[i];
            var numero = i + 2;
            PlanilhaPtBr.GravarNumero(PlanilhaPtBr.Celula(aba, numero, 1), linha.AnoBase);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 2), linha.Perfil);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 3), linha.CodigoAtivo);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 4), linha.Nome);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 5), linha.Natureza);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 6), linha.Codigo);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 7), linha.DescricaoCodigo);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 8), linha.Cnpj);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, numero, 9), linha.ValorLiquido);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, numero, 10), linha.IrRetido);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 11), linha.Descricao);
        }
    }

    private static void GravarRendaFixa(XLWorkbook pasta, IReadOnlyList<LinhaRendaFixaDirpf> linhas)
    {
        var aba = PlanilhaPtBr.CriarAba(pasta, AbaRendaFixa);
        PlanilhaPtBr.GravarCabecalho(aba,
        [
            "Ano-base", "Perfil", "Código do título", "Nome", "Tipo", "Corretora", "Grupo", "Código",
            "Descrição do código", "CNPJ", "Quantidade", "Custo de aquisição", "Preço médio",
            "IR retido no ano", "Rendimento líquido no ano", "Discriminação"
        ]);
        ExigirTeto(aba.Name, linhas.Count);

        for (var i = 0; i < linhas.Count; i++)
        {
            var linha = linhas[i];
            var numero = i + 2;
            PlanilhaPtBr.GravarNumero(PlanilhaPtBr.Celula(aba, numero, 1), linha.AnoBase);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 2), linha.Perfil);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 3), linha.Codigo);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 4), linha.Nome);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 5), linha.Tipo);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 6), linha.Corretora);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 7), linha.Grupo);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 8), linha.CodigoDirpf);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 9), linha.DescricaoCodigo);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 10), linha.Cnpj);
            PlanilhaPtBr.GravarNumero(PlanilhaPtBr.Celula(aba, numero, 11), linha.Quantidade);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, numero, 12), linha.CustoAquisicao);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, numero, 13), linha.PrecoMedio);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, numero, 14), linha.IrRetidoNoAno);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, numero, 15), linha.RendimentoLiquidoNoAno);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, numero, 16), linha.Discriminacao);
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
