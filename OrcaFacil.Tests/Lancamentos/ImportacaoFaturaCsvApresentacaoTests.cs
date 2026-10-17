using System.Globalization;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Lancamentos;
using OrcaFacil.Core.Services;

namespace OrcaFacil.Tests.Lancamentos;

public class ImportacaoFaturaCsvApresentacaoTests
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    [Fact]
    public void BytesDoModelo_PublicaOArquivoQueOParserAceitaSemImportarExemplo()
    {
        Assert.Equal("orcafacil-modelo-fatura-v1.csv", FaturaCsvLayout.NomeArquivoModelo);

        var bytes = ImportacaoFaturaCsvApresentacao.BytesDoModelo();

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
        var leitura = FaturaCsv.Ler(bytes, []);
        Assert.True(leitura.ArquivoAceito);
        Assert.Empty(leitura.Linhas);
        Assert.Empty(leitura.Erros);
        Assert.Contains("OrcaFacil;Fatura;1", FaturaCsvLayout.GerarModelo(), StringComparison.Ordinal);
    }

    [Fact]
    public void SelecaoInicial_MarcaSoAsLinhasNovas()
    {
        var previa = Previa(Linha(3, 10m, "A", duplicada: false), Linha(4, 5m, "A", duplicada: true));

        var selecao = ImportacaoFaturaCsvApresentacao.SelecaoInicial(previa);

        Assert.Equal([3], selecao);
        Assert.DoesNotContain(4, selecao);
    }

    [Fact]
    public void Alternar_NaoMudaOConjuntoOriginal()
    {
        var original = new HashSet<int> { 3 };

        var marcada = ImportacaoFaturaCsvApresentacao.Alternar(original, 4);
        var limpa = ImportacaoFaturaCsvApresentacao.Alternar(marcada, 3);

        Assert.Equal([3], original);
        Assert.Equal([3, 4], marcada.Order());
        Assert.Equal([4], limpa);
    }

    [Fact]
    public void Montar_EstornoVerdeETotaisDaSelecao()
    {
        var compra = Linha(3, 100m, "H1", duplicada: false, descricao: "Supermercado", descritivo: "Compras da semana");
        var estorno = Linha(4, 18m, "H2", duplicada: false, estorno: true, descricao: "Farmácia");
        var previa = Previa(compra, estorno);
        var selecao = ImportacaoFaturaCsvApresentacao.SelecaoInicial(previa);

        var vista = ImportacaoFaturaCsvApresentacao.Montar(previa, selecao, PtBr);

        Assert.Null(vista.Bloqueio);
        Assert.True(vista.PodeConfirmar);
        Assert.Equal(100m, vista.Totais.Compras);
        Assert.Equal(18m, vista.Totais.Estornos);
        Assert.Equal(82m, vista.Totais.Liquido);
        Assert.Equal("R$ 100,00", vista.Totais.ComprasTexto);
        Assert.Equal("+ R$ 18,00", vista.Totais.EstornosTexto);
        Assert.True(vista.Totais.EstornosEmCredito);
        Assert.Equal("R$ 82,00", vista.Totais.LiquidoTexto);
        Assert.Equal("2 de 2 linhas selecionadas", vista.TextoSelecao);

        var credito = Assert.Single(vista.Linhas, l => l.EhEstorno);
        Assert.Equal("+ R$ 18,00", credito.Valor);
        Assert.Equal("Farmácia", credito.Titulo);
        Assert.Contains("Estorno", credito.Subtitulo, StringComparison.Ordinal);
        Assert.Contains("Linha 4", credito.Subtitulo, StringComparison.Ordinal);
        Assert.False(credito.Duplicata);

        var semEstorno = ImportacaoFaturaCsvApresentacao.Montar(
            previa, ImportacaoFaturaCsvApresentacao.Alternar(selecao, 4), PtBr);
        Assert.Equal(0m, semEstorno.Totais.Estornos);
        Assert.Equal("R$ 0,00", semEstorno.Totais.EstornosTexto);
        Assert.False(semEstorno.Totais.EstornosEmCredito);
        Assert.Equal(100m, semEstorno.Totais.Liquido);
    }

    [Fact]
    public void Montar_ArredondaComoOServico()
    {
        var previa = Previa(Linha(3, 0.005m, "H", duplicada: false));

        var vista = ImportacaoFaturaCsvApresentacao.Montar(
            previa, ImportacaoFaturaCsvApresentacao.SelecaoInicial(previa), PtBr);

        Assert.Equal(0.01m, vista.Totais.Compras);
        Assert.Equal(0.01m, vista.Totais.Liquido);
    }

    [Fact]
    public void Montar_ErroDeDataBloqueiaEListaNumeroColunaMensagem()
    {
        var erro = new FaturaCsv.ErroLinha(4, FaturaCsvLayout.ColunaData, "Use a data no formato dd/MM/yyyy.");
        var previa = Previa(new[] { Linha(3, 10m, "H", duplicada: false) }, new[] { erro }, mesFechado: true);

        var vista = ImportacaoFaturaCsvApresentacao.Montar(
            previa, ImportacaoFaturaCsvApresentacao.SelecaoInicial(previa), PtBr);

        Assert.False(vista.PodeConfirmar);
        Assert.Equal(ImportacaoFaturaCsvApresentacao.MensagemLinhasComErro, vista.Bloqueio!.Texto);
        Assert.Equal(TomMensagemImportacaoFatura.Erro, vista.Bloqueio.Tom);
        var linha = Assert.Single(vista.Erros);
        Assert.Equal(4, linha.Numero);
        Assert.Equal("Data", linha.Coluna);
        Assert.Equal("Use a data no formato dd/MM/yyyy.", linha.Mensagem);
        Assert.Equal("Linha 4 · Data: Use a data no formato dd/MM/yyyy.", linha.Texto);
    }

    [Fact]
    public void Montar_CabecalhoOuVersaoBloqueiaComAMensagemDoArquivo()
    {
        const string erro = "A versão 2 do arquivo não é suportada. Nada foi importado. Baixe o modelo da versão 1.";
        var previa = new PreviaImportacaoFaturaCsv(false, erro, true, [], [], 0, 0, 0);

        var vista = ImportacaoFaturaCsvApresentacao.Montar(previa, new HashSet<int>(), PtBr);

        Assert.False(vista.PodeConfirmar);
        Assert.Equal(erro, vista.Bloqueio!.Texto);
        Assert.Equal(TomMensagemImportacaoFatura.Erro, vista.Bloqueio.Tom);
        Assert.Empty(vista.Linhas);
    }

    [Fact]
    public void Montar_MesFechadoBloqueiaMesmoComLinhaNova()
    {
        var previa = Previa([Linha(3, 10m, "H", duplicada: false)], [], mesFechado: true);
        var aberta = Previa(Linha(3, 10m, "H", duplicada: false));

        var pelaPrevia = ImportacaoFaturaCsvApresentacao.Montar(
            previa, ImportacaoFaturaCsvApresentacao.SelecaoInicial(previa), PtBr);
        var pelaTela = ImportacaoFaturaCsvApresentacao.Montar(
            aberta,
            ImportacaoFaturaCsvApresentacao.SelecaoInicial(aberta),
            PtBr,
            mesFechadoNaTela: true);

        Assert.Equal(ImportacaoFaturaCsvApresentacao.MensagemMesFechado, pelaPrevia.Bloqueio!.Texto);
        Assert.Equal(TomMensagemImportacaoFatura.Aviso, pelaPrevia.Bloqueio.Tom);
        Assert.False(pelaPrevia.PodeConfirmar);
        Assert.Equal(ImportacaoFaturaCsvApresentacao.MensagemMesFechado, pelaTela.Bloqueio!.Texto);
    }

    [Fact]
    public void Montar_ModeloSemLinhaValidaBloqueia()
    {
        var vista = ImportacaoFaturaCsvApresentacao.Montar(Previa(), new HashSet<int>(), PtBr);

        Assert.False(vista.PodeConfirmar);
        Assert.Equal(ImportacaoFaturaCsvApresentacao.MensagemSemLinhas, vista.Bloqueio!.Texto);
    }

    [Fact]
    public void Montar_NadaSelecionadoBloqueiaQuandoHaLinhaNova()
    {
        var previa = Previa(Linha(3, 10m, "H", duplicada: false), Linha(4, 5m, "H", duplicada: true));

        var vista = ImportacaoFaturaCsvApresentacao.Montar(previa, new HashSet<int>(), PtBr);

        Assert.False(vista.PodeConfirmar);
        Assert.Equal(ImportacaoFaturaCsvApresentacao.MensagemNadaSelecionado, vista.Bloqueio!.Texto);
    }

    [Fact]
    public void DuplicataDaFatura_ComeçaDesmarcadaEReimportarNaoPedeALinha()
    {
        var previa = Previa(Linha(3, 10m, "H", duplicada: true, descricao: "Supermercado"));
        var selecao = ImportacaoFaturaCsvApresentacao.SelecaoInicial(previa);

        var vista = ImportacaoFaturaCsvApresentacao.Montar(previa, selecao, PtBr);
        var pedido = ImportacaoFaturaCsvApresentacao.MontarPedido(previa, selecao);

        var linha = Assert.Single(vista.Linhas);
        Assert.True(linha.Duplicata);
        Assert.False(linha.Selecionada);
        Assert.Equal("Ignorar", linha.EscolhaDuplicata);
        Assert.Contains("Ignorar a duplicata da linha 3", linha.Aria, StringComparison.Ordinal);
        Assert.True(vista.PodeConfirmar);
        Assert.Null(vista.AvisoDuplicata);
        Assert.NotNull(pedido.LinhasEscolhidas);
        Assert.Empty(pedido.LinhasEscolhidas);
        Assert.True(pedido.IgnorarDuplicatas);
        Assert.Equal(1, ImportacaoFaturaCsvApresentacao.ContarDuplicatasIgnoradas(previa, selecao));
    }

    [Fact]
    public void DuplicataMarcadaParaIncluir_RecusaOPedidoInteiro()
    {
        var previa = Previa(
            Linha(3, 10m, "NOVA", duplicada: false),
            Linha(4, 8m, "JA", duplicada: true));
        var selecao = ImportacaoFaturaCsvApresentacao.SelecaoInicial(previa);
        selecao = ImportacaoFaturaCsvApresentacao.Alternar(selecao, 4);

        var vista = ImportacaoFaturaCsvApresentacao.Montar(previa, selecao, PtBr);
        var pedido = ImportacaoFaturaCsvApresentacao.MontarPedido(previa, selecao);

        Assert.True(vista.PodeConfirmar);
        Assert.Equal(ImportacaoFaturaCsvApresentacao.MensagemIncluirDuplicata, vista.AvisoDuplicata);
        Assert.Equal("Incluir", vista.Linhas[1].EscolhaDuplicata);
        Assert.False(pedido.IgnorarDuplicatas);
        Assert.Equal([3, 4], pedido.LinhasEscolhidas!.Order());
    }

    [Fact]
    public void DuplicataDoProprioArquivo_ViraLinhaNovaSeAPrimeiraSairDaSelecao()
    {
        var previa = Previa(
            Linha(3, 10m, "H", duplicada: false, descricao: "Padaria"),
            Linha(4, 10m, "H", duplicada: true, descricao: "Padaria"));
        var soASegunda = ImportacaoFaturaCsvApresentacao.Alternar(
            ImportacaoFaturaCsvApresentacao.Alternar(ImportacaoFaturaCsvApresentacao.SelecaoInicial(previa), 3),
            4);

        var vista = ImportacaoFaturaCsvApresentacao.Montar(previa, soASegunda, PtBr);
        var pedido = ImportacaoFaturaCsvApresentacao.MontarPedido(previa, soASegunda);

        Assert.False(vista.Linhas[1].Duplicata);
        Assert.True(vista.Linhas[1].Selecionada);
        Assert.Null(vista.Linhas[1].EscolhaDuplicata);
        Assert.True(pedido.IgnorarDuplicatas);
        Assert.Equal([4], pedido.LinhasEscolhidas);
        Assert.Equal(0, ImportacaoFaturaCsvApresentacao.ContarDuplicatasIgnoradas(previa, soASegunda));
        Assert.Equal(10m, vista.Totais.Compras);
    }

    [Fact]
    public void Resumo_ContaItensGravadosEDuplicatasDeixadasDeFora()
    {
        var previa = Previa(Linha(3, 10m, "NOVA", duplicada: false), Linha(4, 8m, "JA", duplicada: true));
        var selecao = ImportacaoFaturaCsvApresentacao.SelecaoInicial(previa);
        var resultado = new ResultadoImportacaoFaturaCsv(1, 0, 10m, 0m, 10m, 100m);

        Assert.Equal("1 item importado, 1 duplicata ignorada", ImportacaoFaturaCsvApresentacao.Resumo(1, 1));
        Assert.Equal("0 itens importados, 3 duplicatas ignoradas", ImportacaoFaturaCsvApresentacao.Resumo(0, 3));
        Assert.Equal(
            "1 item importado, 1 duplicata ignorada",
            ImportacaoFaturaCsvApresentacao.ResumoDaConfirmacao(resultado, previa, selecao));
    }

    [Fact]
    public void GuardaToque_IgnoraOSegundoToqueAteLiberar()
    {
        var toque = new GuardaToque();

        Assert.True(toque.Tentar());
        Assert.True(toque.EmAndamento);
        Assert.False(toque.Tentar());
        toque.Liberar();
        Assert.False(toque.EmAndamento);
        Assert.True(toque.Tentar());
    }

    [Fact]
    public void AplicarNaFatura_AtualizaValorEItensEMantemOResto()
    {
        var modelo = new TransacaoFormModel
        {
            Descricao = "Fatura Itaú",
            Valor = 500m,
            Itens = [new ItemLancamentoFormModel { Id = 9, Descricao = "Rascunho", Valor = 1m, CategoriaId = 2, Ordem = 0 }]
        };
        var itens = new[]
        {
            new ItemLancamento { Id = 2, Descricao = "Netflix", Valor = 39.90m, CategoriaId = 4, Ordem = 2, Descritivo = "Streaming" },
            new ItemLancamento
            {
                Id = 1,
                Descricao = "Farmácia",
                Valor = 18m,
                EhEstorno = true,
                CategoriaId = 3,
                Ordem = 1
            }
        };

        ImportacaoFaturaCsvApresentacao.AplicarNaFatura(modelo, 482m, itens);

        Assert.Equal("Fatura Itaú", modelo.Descricao);
        Assert.Equal(482m, modelo.Valor);
        Assert.Equal([1, 2], modelo.Itens.Select(i => i.Id));
        Assert.True(modelo.Itens[0].EhEstorno);
        Assert.Equal(18m, modelo.Itens[0].Valor);
        Assert.Equal("Streaming", modelo.Itens[1].Descritivo);
        Assert.NotSame(itens[0], modelo.Itens[0]);
    }

    [Fact]
    public void LinhaParcelada_MostraAParcelaNoSubtitulo()
    {
        var linha = new LinhaPreviaImportacaoFaturaCsv(
            5, new DateTime(2026, 3, 10), "Notebook", "Loja", 350m, false, 7, "Compras",
            NaturezaItemLancamento.Parcelada, 2, 12, "PARC", false);
        var previa = Previa(linha);

        var vista = ImportacaoFaturaCsvApresentacao.Montar(
            previa, ImportacaoFaturaCsvApresentacao.SelecaoInicial(previa), PtBr);

        var item = Assert.Single(vista.Linhas);
        Assert.Equal("Loja", item.Titulo);
        Assert.Equal("Linha 5 · 10/03/2026 · Compras · Parcela 2/12", item.Subtitulo);
        Assert.Equal("R$ 350,00", item.Valor);
    }

    private static PreviaImportacaoFaturaCsv Previa(
        params LinhaPreviaImportacaoFaturaCsv[] linhas)
        => Previa(linhas, []);

    private static PreviaImportacaoFaturaCsv Previa(
        IReadOnlyList<LinhaPreviaImportacaoFaturaCsv> linhas,
        IReadOnlyList<FaturaCsv.ErroLinha> erros,
        bool mesFechado = false)
        => new(true, null, mesFechado, linhas, erros, 0, 0, 0);

    private static LinhaPreviaImportacaoFaturaCsv Linha(
        int numero,
        decimal valor,
        string hash,
        bool duplicada,
        bool estorno = false,
        string descricao = "Item",
        string? descritivo = null)
        => new(
            numero,
            new DateTime(2026, 10, 16),
            descricao,
            descritivo ?? descricao,
            valor,
            estorno,
            1,
            "Alimentação",
            NaturezaItemLancamento.Avulsa,
            null,
            null,
            hash,
            duplicada);
}

public class ImportacaoFaturaCsvTelaArquivosTests
{
    [Fact]
    public void FaturaAbreATelaComModeloPreviaEConfirmacao()
    {
        var raiz = EncontrarRaiz();
        var painel = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Pages", "Lancamentos", "FaturaCartaoPainel.razor"));
        var lista = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Shared", "ItemList.razor"));
        var dialog = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Pages", "Lancamentos", "ImportacaoFaturaCsvDialog.razor"));
        var css = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Pages", "Lancamentos", "ImportacaoFaturaCsvDialog.razor.css"));
        var tema = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "wwwroot", "css", "orcafacil-theme.css"));

        Assert.Contains("OnImportar=\"AbrirImportacaoAsync\"", painel, StringComparison.Ordinal);
        Assert.Contains("ImportacaoFaturaCsvApresentacao.TextoAcao", painel, StringComparison.Ordinal);
        Assert.Contains("ImportacaoFaturaCsvDialog", painel, StringComparison.Ordinal);
        Assert.Contains("OnImportar", lista, StringComparison.Ordinal);
        Assert.Contains("of-lista-acao-larga", lista, StringComparison.Ordinal);
        Assert.Contains("FilePicker.Default.PickAsync", dialog, StringComparison.Ordinal);
        Assert.Contains("BytesDoModelo", dialog, StringComparison.Ordinal);
        Assert.Contains("FaturaCsvLayout.NomeArquivoModelo", dialog, StringComparison.Ordinal);
        Assert.Contains("CompartilharAsync", dialog, StringComparison.Ordinal);
        Assert.Contains("TextoConfirmar", dialog, StringComparison.Ordinal);
        Assert.Contains("GuardaToque", dialog, StringComparison.Ordinal);
        Assert.Contains("var(--of-error)", css, StringComparison.Ordinal);
        Assert.Contains("var(--of-success)", css, StringComparison.Ordinal);
        Assert.Contains("var(--of-warning)", css, StringComparison.Ordinal);
        Assert.Contains("of-import-titulo", tema, StringComparison.Ordinal);
        Assert.Contains("var(--of-background)", tema, StringComparison.Ordinal);
    }

    private static string EncontrarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "OrcaFacil", "Components"))
                && Directory.Exists(Path.Combine(dir.FullName, "OrcaFacil.Tests")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar a raiz do repositório.");
    }
}
