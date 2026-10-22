using System.Globalization;
using OrcaFacil.Core.Exportacao;
using OrcaFacil.Core.Planilha;

namespace OrcaFacil.Tests.Exportacao;

public class BotaoExportarPlanilhaTests
{
    private const string Nota = "O arquivo exportado não é criptografado e contém os dados financeiros em claro.";

    [Fact]
    public void BotaoExportarParaExcelFicaNoPainelComCarregamentoEErro()
    {
        var painel = File.ReadAllText(CaminhoPainel());
        var codigo = painel.IndexOf("@code", StringComparison.Ordinal);
        var nota = painel.IndexOf(Nota, StringComparison.Ordinal);

        Assert.Contains("Exportar para Excel", painel, StringComparison.Ordinal);
        Assert.Contains("Exportando…", painel, StringComparison.Ordinal);
        Assert.Contains("Disabled=\"@Orquestrador.Exportando\"", painel, StringComparison.Ordinal);
        Assert.Contains("MudProgressLinear", painel, StringComparison.Ordinal);
        Assert.Contains("MudAlert", painel, StringComparison.Ordinal);
        Assert.Contains("role=\"note\"", painel, StringComparison.Ordinal);
        Assert.Contains("id=\"aviso-planilha-clara\"", painel, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"aviso-planilha-clara\"", painel, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Exportando a planilha\"", painel, StringComparison.Ordinal);
        Assert.Contains("static readonly OrquestradorExportacaoPlanilha Orquestrador", painel, StringComparison.Ordinal);
        Assert.Contains("Orquestrador.Aviso", painel, StringComparison.Ordinal);
        Assert.Contains("Orquestrador.Exportando", painel, StringComparison.Ordinal);
        Assert.Contains("new AcompanhamentoExportacaoPlanilha(Orquestrador, () => InvokeAsync(StateHasChanged))", painel, StringComparison.Ordinal);
        Assert.Contains("_acompanhamento?.Dispose()", painel, StringComparison.Ordinal);
        Assert.Contains("Orquestrador.FolhaAberta", painel, StringComparison.Ordinal);
        Assert.Contains("catch (ObjectDisposedException)", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("private bool _exportando", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("private string? _aviso", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("private bool _folha", painel, StringComparison.Ordinal);
        Assert.True(nota >= 0 && codigo > nota, "A nota precisa aparecer na tela, antes do código.");
        Assert.Contains("FileSystem.CacheDirectory", painel, StringComparison.Ordinal);
        Assert.Contains("IDbContextFactory<AppDbContext>", painel, StringComparison.Ordinal);
        Assert.Contains("ExportadorPlanilha.Exportar", painel, StringComparison.Ordinal);
        Assert.Contains("ICompartilhadorArquivoLocal", painel, StringComparison.Ordinal);
        Assert.Contains("CompartilharAsync", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("Share.Default", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("ex.Message", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("ex.ToString()", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("ApagarSeForDesteApp", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("ApagarAnteriores", painel, StringComparison.Ordinal);
        Assert.Equal(1, Contar(painel, "exportado"));

        var compartilhador = File.ReadAllText(Path.Combine(
            EncontrarRaiz(), "OrcaFacil", "Services", "Espelho", "AdaptadoresPastaEspelhoMaui.cs"));
        Assert.Contains("new ShareFile(caminho, ArquivoExportacaoPlanilha.Mime)", compartilhador, StringComparison.Ordinal);
        Assert.Contains("return new ShareFile(caminho);", compartilhador, StringComparison.Ordinal);
    }

    [Fact]
    public void SegundoBotaoGeraDirpfDoAnoAnterior_SemMexerNoExcel()
    {
        var painel = File.ReadAllText(CaminhoPainel());
        var excel = Trecho(painel, "<MudButton", "<MudNumericField");

        Assert.Contains("OnClick=\"ExportarAsync\"", excel, StringComparison.Ordinal);
        Assert.Contains("Exportar para Excel", excel, StringComparison.Ordinal);
        Assert.Contains("Exportando…", excel, StringComparison.Ordinal);
        Assert.Contains("Disabled=\"@Orquestrador.Exportando\"", excel, StringComparison.Ordinal);
        Assert.DoesNotContain("ExportadorDirpf", excel, StringComparison.Ordinal);
        Assert.DoesNotContain("Gerar DIRPF", excel, StringComparison.Ordinal);
        Assert.DoesNotContain("OrquestradorDirpf", excel, StringComparison.Ordinal);

        Assert.Contains("Label=\"Ano-base\"", painel, StringComparison.Ordinal);
        Assert.Contains("AnoBaseDirpf.Anterior", painel, StringComparison.Ordinal);
        Assert.Contains("OnClick=\"ExportarDirpfAsync\"", painel, StringComparison.Ordinal);
        Assert.Contains("Gerar DIRPF", painel, StringComparison.Ordinal);
        Assert.Contains("ExportadorDirpf.Exportar", painel, StringComparison.Ordinal);
        Assert.Contains("OrquestradorDirpf", painel, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Gerando a DIRPF\"", painel, StringComparison.Ordinal);
        Assert.Equal(1, Contar(painel, "exportado"));

        var gravarExcel = Trecho(painel, "private void Gravar(", "private async Task<DesfechoCompartilhamentoPlanilha> AbrirFolha");
        Assert.Contains("ExportadorPlanilha.Exportar", gravarExcel, StringComparison.Ordinal);
        Assert.DoesNotContain("ExportadorDirpf", gravarExcel, StringComparison.Ordinal);
    }

    [Fact]
    public void TerceiroBotaoGeraDarfs_SemMexerNoExcelNemNaDirpf()
    {
        var painel = File.ReadAllText(CaminhoPainel());
        var excel = Trecho(painel, "<MudButton", "<MudNumericField");
        var dirpf = Trecho(painel, "OnClick=\"ExportarDirpfAsync\"", "OnClick=\"ExportarDarfAsync\"");

        Assert.Contains("OnClick=\"ExportarAsync\"", excel, StringComparison.Ordinal);
        Assert.DoesNotContain("Gerar DARFs", excel, StringComparison.Ordinal);
        Assert.Contains("Gerar DIRPF", dirpf, StringComparison.Ordinal);
        Assert.DoesNotContain("ExportadorDarf", dirpf, StringComparison.Ordinal);
        Assert.Contains("OnClick=\"ExportarDarfAsync\"", painel, StringComparison.Ordinal);
        Assert.Contains("Gerar DARFs", painel, StringComparison.Ordinal);
        Assert.Contains("ExportadorDarf.Exportar", painel, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Gerando os DARFs\"", painel, StringComparison.Ordinal);
        Assert.Contains("OrquestradorDarf", painel, StringComparison.Ordinal);
        Assert.Equal(1, Contar(painel, "exportado"));

        var gravarDirpf = Trecho(painel, "private void GravarDirpf", "private async Task<DesfechoCompartilhamentoPlanilha> AbrirDirpf");
        Assert.Contains("ExportadorDirpf.Exportar", gravarDirpf, StringComparison.Ordinal);
        Assert.DoesNotContain("ExportadorDarf", gravarDirpf, StringComparison.Ordinal);

        var gravarDarf = Trecho(painel, "private void GravarDarf", "private async Task<DesfechoCompartilhamentoPlanilha> AbrirDarf");
        var indiceAno = gravarDarf.IndexOf("ExportadorDarf.ExigirAno", StringComparison.Ordinal);
        var indiceArquivo = gravarDarf.IndexOf("new FileStream", StringComparison.Ordinal);
        Assert.True(indiceAno >= 0 && indiceArquivo > indiceAno);
        Assert.Equal(6, Contar(painel, "MudAlert"));
    }

    [Fact]
    public void GerarDarfsPedeOIrrfSemMudarOPainel()
    {
        var painel = File.ReadAllText(CaminhoPainel());
        var visual = painel[..painel.IndexOf("@code", StringComparison.Ordinal)];
        Assert.DoesNotContain("IrrfDarfDialog", visual, StringComparison.Ordinal);
        Assert.DoesNotContain("IRRF", visual, StringComparison.Ordinal);
        Assert.Equal(3, Contar(visual, "<MudButton"));
        Assert.Equal(6, Contar(painel, "MudAlert"));
        Assert.Contains("Gerar DARFs", visual, StringComparison.Ordinal);

        var exportar = Trecho(painel, "private async Task ExportarDarfAsync()", "private async Task<bool> ConfirmarIrrfAsync");
        var confirmar = Trecho(painel, "private async Task<bool> ConfirmarIrrfAsync", "private async Task ExecutarGeracaoDarfAsync");
        var gravar = Trecho(painel, "private void GravarDarf", "private async Task<DesfechoCompartilhamentoPlanilha> AbrirDarf");

        Assert.Contains("PreparoGeracaoDarf.DecidirAsync(ano, () => ConfirmarIrrfAsync(ano))", exportar, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowAsync", exportar, StringComparison.Ordinal);
        Assert.Contains("if (!decisao.GerarArquivo)", exportar, StringComparison.Ordinal);
        Assert.Contains("if (decisao.Aviso is not null)", exportar, StringComparison.Ordinal);
        Assert.Equal(2, Contar(exportar, "ExecutarGeracaoDarfAsync(ano)"));
        var indiceAviso = exportar.IndexOf("if (decisao.Aviso is not null)", StringComparison.Ordinal);
        var indiceReturn = exportar.IndexOf("return;", indiceAviso, StringComparison.Ordinal);
        var indiceGerar = exportar.IndexOf("await ExecutarGeracaoDarfAsync(ano);", indiceReturn, StringComparison.Ordinal);
        Assert.True(indiceAviso >= 0 && indiceReturn > indiceAviso && indiceGerar > indiceReturn);

        Assert.Contains("ShowAsync<IrrfDarfDialog>", confirmar, StringComparison.Ordinal);
        Assert.Contains("CapturaIrrfDarf.Montar", confirmar, StringComparison.Ordinal);
        Assert.Contains("return false;", confirmar, StringComparison.Ordinal);
        Assert.DoesNotContain("ExportadorDarf.Exportar", confirmar, StringComparison.Ordinal);
        Assert.DoesNotContain("OrquestradorDarf.ExportarAsync", confirmar, StringComparison.Ordinal);
        Assert.True(confirmar.IndexOf("ShowAsync<IrrfDarfDialog>", StringComparison.Ordinal)
            < confirmar.IndexOf("return false;", StringComparison.Ordinal));

        var indiceAno = gravar.IndexOf("ExportadorDarf.ExigirAno", StringComparison.Ordinal);
        var indiceSalvar = gravar.IndexOf("CapturaIrrfDarf.Salvar", StringComparison.Ordinal);
        var indiceListar = gravar.IndexOf("CapturaIrrfDarf.Listar", StringComparison.Ordinal);
        var indiceArquivo = gravar.IndexOf("new FileStream", StringComparison.Ordinal);
        var indiceExportar = gravar.IndexOf("ExportadorDarf.Exportar(contexto, anoBase, destino, irrf)", StringComparison.Ordinal);
        Assert.True(indiceAno >= 0
            && indiceSalvar > indiceAno
            && indiceListar > indiceSalvar
            && indiceArquivo > indiceListar
            && indiceExportar > indiceArquivo);

        var dialogo = File.ReadAllText(Path.Combine(
            EncontrarRaiz(), "OrcaFacil", "Components", "Pages", "Configuracoes", "IrrfDarfDialog.razor"));
        Assert.Contains("MudDialog.Cancel()", dialogo, StringComparison.Ordinal);
        Assert.Contains("Nenhuma venda neste ano-base.", dialogo, StringComparison.Ordinal);
        Assert.Contains("CapturaIrrfDarf.NomeDoMes", dialogo, StringComparison.Ordinal);
        Assert.Contains("campo.Cesta.Rotulo()", dialogo, StringComparison.Ordinal);
        Assert.DoesNotContain("ExportadorDarf", dialogo, StringComparison.Ordinal);
        Assert.DoesNotContain("FileStream", dialogo, StringComparison.Ordinal);
    }

    [Fact]
    public void PainelFechaOArquivoAntesDeCompartilhar()
    {
        var painel = File.ReadAllText(CaminhoPainel());
        var gravar = Trecho(painel, "private void Gravar", "private async Task<DesfechoCompartilhamentoPlanilha> AbrirFolha");
        var exportar = Trecho(painel, "private async Task ExportarAsync", "private async Task AvisarFalhaAsync");
        var abrir = Trecho(painel, "private async Task<DesfechoCompartilhamentoPlanilha> AbrirFolha", "private ConfiguracaoExportavel MontarConfiguracao");

        var flush = gravar.IndexOf("destino.Flush()", StringComparison.Ordinal);
        var fecha = gravar.LastIndexOf('}');
        Assert.True(flush >= 0 && fecha > flush);
        Assert.Contains("using (var destino", gravar, StringComparison.Ordinal);
        Assert.DoesNotContain("CompartilharAsync", gravar, StringComparison.Ordinal);

        var indiceMontar = exportar.IndexOf("MontarConfiguracao,", StringComparison.Ordinal);
        var indiceCache = exportar.IndexOf("() => FileSystem.CacheDirectory", StringComparison.Ordinal);
        var indiceGravar = exportar.IndexOf("Gravar,", StringComparison.Ordinal);
        var indiceAbrir = exportar.IndexOf("AbrirFolha", StringComparison.Ordinal);
        Assert.True(indiceMontar >= 0 && indiceCache > indiceMontar && indiceGravar > indiceCache && indiceAbrir > indiceGravar);
        Assert.DoesNotContain("MontarConfiguracao()", exportar, StringComparison.Ordinal);
        Assert.Contains("CompartilharAsync", abrir, StringComparison.Ordinal);
        Assert.Contains("DesfechoCompartilhamentoPlanilha.FolhaAberta", abrir, StringComparison.Ordinal);
        Assert.Contains("CodigoFalhaPastaEspelho.Cancelado", abrir, StringComparison.Ordinal);
    }

    [Fact]
    public void AberturaEResumeLimpamSoPeloPadrao()
    {
        var app = File.ReadAllText(Path.Combine(EncontrarRaiz(), "OrcaFacil", "App.xaml.cs"));

        Assert.Contains("janela.Created += (_, _) => Varredura.Disparar(LerCache);", app, StringComparison.Ordinal);
        Assert.Contains("janela.Activated += (_, _) => Varredura.Disparar(LerCache);", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Resumed", app, StringComparison.Ordinal);
        Assert.DoesNotContain("await", app, StringComparison.Ordinal);
        Assert.DoesNotContain(".Wait(", app, StringComparison.Ordinal);
        Assert.DoesNotContain(".Result", app, StringComparison.Ordinal);
        Assert.Contains("Log.Error", app, StringComparison.Ordinal);
        Assert.Contains("VarreduraPlanilhaExportada.TipoDaFalha(", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Log.Error(excecao", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Log.Error(ex", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.Delete", app, StringComparison.Ordinal);

        var varredura = File.ReadAllText(Path.Combine(
            EncontrarRaiz(), "OrcaFacil.Core", "Exportacao", "VarreduraPlanilhaExportada.cs"));
        Assert.Contains("Task.Run", varredura, StringComparison.Ordinal);
        Assert.Contains("ApagarVencidos", varredura, StringComparison.Ordinal);
        Assert.Contains("Interlocked.CompareExchange", varredura, StringComparison.Ordinal);
    }

    [Fact]
    public void PainelMontaODtoSemSegredo()
    {
        var painel = File.ReadAllText(CaminhoPainel());
        var montar = Trecho(painel, "private ConfiguracaoExportavel MontarConfiguracao", null);

        Assert.Contains("Tema = Configuracao.Tema", montar, StringComparison.Ordinal);
        Assert.Contains("NotificacoesAtivas = Configuracao.NotificacoesAtivas", montar, StringComparison.Ordinal);
        Assert.Contains("SomAtivo = Configuracao.SomAtivo", montar, StringComparison.Ordinal);
        Assert.Contains("GraficoLancamentosExpandido = Configuracao.GraficoLancamentosExpandido", montar, StringComparison.Ordinal);
        Assert.Contains("PrazosAviso = Configuracao.PrazosAviso", montar, StringComparison.Ordinal);
        Assert.Contains("Url = Configuracao.UrlInteligencia", montar, StringComparison.Ordinal);
        Assert.Contains("Modelo = Configuracao.ModeloInteligencia", montar, StringComparison.Ordinal);
        Assert.Contains("ProvedorInteligencia", montar, StringComparison.Ordinal);
        Assert.Contains("ChipsInteligencia.Texto", montar, StringComparison.Ordinal);

        Assert.DoesNotContain("SecureStorage", painel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("senha", painel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Espelho.Pasta.Uri", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("Ia.ChaveApi", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("ChaveApi", painel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CofreChave", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("chave", painel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConfiguracoesIncluiOPainelSoNaAbaArmazenamento()
    {
        var raiz = EncontrarRaiz();
        var configuracoes = File.ReadAllText(Path.Combine(
            raiz, "OrcaFacil", "Components", "Pages", "Configuracoes", "Configuracoes.razor"));
        var armazenamentoPanel = File.ReadAllText(Path.Combine(
            raiz, "OrcaFacil", "Components", "Pages", "Configuracoes", "ArmazenamentoPanel.razor"));

        var armazenamento = BlocoAba(configuracoes, "Armazenamento");
        var notificacoes = BlocoAba(configuracoes, "Notificações");
        var inteligencia = BlocoAba(configuracoes, "Inteligência artificial");

        Assert.Contains("<ArmazenamentoPanel />", armazenamento, StringComparison.Ordinal);
        Assert.Contains("<ExportarPlanilhaPanel />", armazenamento, StringComparison.Ordinal);
        Assert.DoesNotContain("ExportarPlanilhaPanel", notificacoes, StringComparison.Ordinal);
        Assert.DoesNotContain("ExportarPlanilhaPanel", inteligencia, StringComparison.Ordinal);
        Assert.DoesNotContain("ExportarPlanilhaPanel", armazenamentoPanel, StringComparison.Ordinal);
        Assert.Equal(1, Contar(configuracoes, "<ExportarPlanilhaPanel />"));
    }

    private static string CaminhoPainel()
        => Path.Combine(EncontrarRaiz(), "OrcaFacil", "Components", "Pages", "Configuracoes", "ExportarPlanilhaPanel.razor");

    private static string Trecho(string fonte, string inicioMarca, string? fimMarca)
    {
        var inicio = fonte.IndexOf(inicioMarca, StringComparison.Ordinal);
        Assert.True(inicio >= 0, inicioMarca);
        var fim = fimMarca is null
            ? fonte.Length
            : fonte.IndexOf(fimMarca, inicio + inicioMarca.Length, StringComparison.Ordinal);
        Assert.True(fim > inicio);
        return fonte[inicio..fim];
    }

    private static string BlocoAba(string configuracoes, string titulo)
    {
        var marca = $"Text=\"{titulo}\"";
        var inicio = configuracoes.IndexOf(marca, StringComparison.Ordinal);
        Assert.True(inicio >= 0, titulo);
        var fim = configuracoes.IndexOf("</MudTabPanel>", inicio, StringComparison.Ordinal);
        Assert.True(fim > inicio);
        return configuracoes[inicio..fim];
    }

    private static int Contar(string texto, string trecho)
    {
        var total = 0;
        var indice = 0;
        while (true)
        {
            indice = texto.IndexOf(trecho, indice, StringComparison.Ordinal);
            if (indice < 0)
                return total;
            total++;
            indice += trecho.Length;
        }
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

public class ArquivoExportacaoPlanilhaTests
{
    private static readonly DateTime Instante = new(2026, 10, 19, 16, 30, 7);
    private const string Sufixo = "ab12cd34";

    [Fact]
    public void NomeSegueAnoMesDiaEHora()
    {
        var nome = ArquivoExportacaoPlanilha.Nome(Instante, Sufixo);
        var aleatorio = ArquivoExportacaoPlanilha.NovoSufixo();

        Assert.Equal("orcafacil-20261019-163007-ab12cd34.xlsx", nome);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ArquivoExportacaoPlanilha.Mime);
        Assert.True(ArquivoExportacaoPlanilha.SufixoValido(aleatorio));
        Assert.Equal(8, aleatorio.Length);
        Assert.True(ArquivoExportacaoPlanilha.NomeEhDesteApp(nome));
        Assert.True(ArquivoExportacaoPlanilha.NomeEhDesteApp(
            ArquivoExportacaoPlanilha.Nome(Instante, aleatorio)));
        Assert.True(ArquivoExportacaoPlanilha.NomeEhDesteApp("orcafacil-20261019-163007.xlsx"));
        Assert.False(ArquivoExportacaoPlanilha.NomeEhDesteApp("orcafacil-2026101a-163007.xlsx"));
        Assert.False(ArquivoExportacaoPlanilha.NomeEhDesteApp("orcafacil-20261019-16300.xlsx"));
        Assert.False(ArquivoExportacaoPlanilha.NomeEhDesteApp("orcafacil-20261019-1630077.xlsx"));
        Assert.False(ArquivoExportacaoPlanilha.NomeEhDesteApp("orcafacil-20261019-163007-AB12CD34.xlsx"));
        Assert.False(ArquivoExportacaoPlanilha.NomeEhDesteApp("orcafacil-20261019-163007-ab12cd3g.xlsx"));
        Assert.False(ArquivoExportacaoPlanilha.NomeEhDesteApp("orcafacil-20261019-163007.XLSX"));
        Assert.False(ArquivoExportacaoPlanilha.NomeEhDesteApp("orcafacil-relatorio-20261019-163007.csv"));
        Assert.False(ArquivoExportacaoPlanilha.NomeEhDesteApp("orcafacil-espelho.xlsx"));
        Assert.False(ArquivoExportacaoPlanilha.NomeEhDesteApp("orcafacil-20261019-163007-ab12cd34.xlsx.xlsx"));
        Assert.False(ArquivoExportacaoPlanilha.NomeEhDesteApp("../orcafacil-20261019-163007-ab12cd34.xlsx"));
        Assert.False(ArquivoExportacaoPlanilha.NomeEhDesteApp(null));
        Assert.Throws<ArgumentException>(() => ArquivoExportacaoPlanilha.Nome(Instante, "curto"));
    }

    [Fact]
    public void CaminhoFicaNoTopoDoCache_ERecusaARaizDoVolume()
    {
        var raiz = Directory.CreateTempSubdirectory("orca-planilha-caminho-");
        try
        {
            var cache = Path.Combine(raiz.FullName, "cache");
            Directory.CreateDirectory(cache);
            var nome = ArquivoExportacaoPlanilha.Nome(Instante, Sufixo);

            var caminho = ArquivoExportacaoPlanilha.Caminho(cache + Path.DirectorySeparatorChar, Instante, Sufixo);

            Assert.Equal(Path.GetFullPath(Path.Combine(cache, nome)), caminho);
            Assert.Equal(nome, Path.GetFileName(caminho));
            Assert.Throws<InvalidOperationException>(() => ArquivoExportacaoPlanilha.Caminho("/", Instante, Sufixo));
            Assert.Throws<ArgumentException>(() => ArquivoExportacaoPlanilha.Caminho("   ", Instante, Sufixo));
            Assert.Throws<ArgumentException>(() => ArquivoExportacaoPlanilha.Caminho(cache, Instante, "AB12CD34"));
        }
        finally
        {
            raiz.Delete(recursive: true);
        }
    }

    [Fact]
    public void ApagaSoOXlsxDesteAppNoTopoDoCache()
    {
        var raiz = Directory.CreateTempSubdirectory("orca-planilha-limpeza-");
        try
        {
            var cache = Path.Combine(raiz.FullName, "cache");
            var espelho = Path.Combine(cache, "orcafacil-espelho");
            var fora = Path.Combine(raiz.FullName, "fora");
            Directory.CreateDirectory(espelho);
            Directory.CreateDirectory(fora);

            var antigo = Path.Combine(cache, ArquivoExportacaoPlanilha.Nome(new DateTime(2026, 10, 18, 9, 0, 0), "11223344"));
            var semSufixo = Path.Combine(cache, "orcafacil-20261018-090000.xlsx");
            var comLetra = Path.Combine(cache, "orcafacil-2026101a-090000.xlsx");
            var tamanhoErrado = Path.Combine(cache, "orcafacil-20261019-09000.xlsx");
            var atual = ArquivoExportacaoPlanilha.Caminho(cache, Instante, Sufixo);
            var csv = Path.Combine(cache, "orcafacil-relatorio-20261019-120000.csv");
            var outroXlsx = Path.Combine(cache, "orcafacil-espelho.xlsx");
            var maiusculo = Path.Combine(cache, "orcafacil-20261019-163007-AB12CD34.xlsx");
            var aninhado = Path.Combine(espelho, ArquivoExportacaoPlanilha.Nome(Instante, "aabbccdd"));
            var estagio = Path.Combine(espelho, "estagio.bin");
            var pastaComNome = Path.Combine(cache, ArquivoExportacaoPlanilha.Nome(new DateTime(2026, 10, 17, 1, 2, 3), "abcd1234"));
            var dentroDaPasta = Path.Combine(pastaComNome, "dentro.txt");
            var alvoFora = Path.Combine(fora, ArquivoExportacaoPlanilha.Nome(Instante, "deadbeef"));
            Directory.CreateDirectory(pastaComNome);

            File.WriteAllText(antigo, "antigo");
            File.WriteAllText(semSufixo, "antigo-sem-sufixo");
            File.WriteAllText(comLetra, "letra");
            File.WriteAllText(tamanhoErrado, "tamanho");
            File.WriteAllText(atual, "atual");
            File.WriteAllText(csv, "csv");
            File.WriteAllText(outroXlsx, "outro");
            File.WriteAllText(maiusculo, "MAIUSCULO");
            File.WriteAllText(aninhado, "aninhado");
            File.WriteAllText(estagio, "estagio");
            File.WriteAllText(dentroDaPasta, "dentro");
            File.WriteAllText(alvoFora, "fora");

            var apagados = ArquivoExportacaoPlanilha.ApagarAnteriores(cache);

            Assert.Equal(3, apagados);
            Assert.False(File.Exists(antigo));
            Assert.False(File.Exists(atual));
            Assert.False(File.Exists(semSufixo));
            Assert.Equal("letra", File.ReadAllText(comLetra));
            Assert.Equal("tamanho", File.ReadAllText(tamanhoErrado));
            Assert.Equal("csv", File.ReadAllText(csv));
            Assert.Equal("outro", File.ReadAllText(outroXlsx));
            Assert.Equal("MAIUSCULO", File.ReadAllText(maiusculo));
            Assert.Equal("aninhado", File.ReadAllText(aninhado));
            Assert.Equal("estagio", File.ReadAllText(estagio));
            Assert.Equal("dentro", File.ReadAllText(dentroDaPasta));
            Assert.Equal("fora", File.ReadAllText(alvoFora));
            Assert.False(ArquivoExportacaoPlanilha.ApagarSeForDesteApp(alvoFora, cache));
            Assert.False(ArquivoExportacaoPlanilha.ApagarSeForDesteApp(
                Path.Combine(cache, "..", "fora", ArquivoExportacaoPlanilha.Nome(Instante, "deadbeef")),
                cache));
            Assert.Equal("fora", File.ReadAllText(alvoFora));
            Assert.Equal(0, ArquivoExportacaoPlanilha.ApagarAnteriores(Path.Combine(cache, "nao-existe")));
            Assert.Equal(0, ArquivoExportacaoPlanilha.ApagarAnteriores("/"));
            Assert.Equal(0, ArquivoExportacaoPlanilha.ApagarAnteriores(null));
        }
        finally
        {
            raiz.Delete(recursive: true);
        }
    }

    [Fact]
    public void AtalhoComONomeDaPlanilha_NaoApagaOAlvoForaDoCache()
    {
        var raiz = Directory.CreateTempSubdirectory("orca-planilha-atalho-");
        try
        {
            var cache = Path.Combine(raiz.FullName, "cache");
            var fora = Path.Combine(raiz.FullName, "fora");
            Directory.CreateDirectory(cache);
            Directory.CreateDirectory(fora);
            var alvo = Path.Combine(fora, "dados.txt");
            File.WriteAllText(alvo, "fica");
            var link = Path.Combine(cache, ArquivoExportacaoPlanilha.Nome(Instante, Sufixo));
            File.CreateSymbolicLink(link, alvo);

            var apagados = ArquivoExportacaoPlanilha.ApagarAnteriores(cache);

            Assert.Equal(0, apagados);
            Assert.Equal("fica", File.ReadAllText(alvo));
            Assert.True(File.Exists(link));
        }
        finally
        {
            raiz.Delete(recursive: true);
        }
    }

    [Fact]
    public void ArquivoReservadoNaoEntraNaLimpeza()
    {
        var raiz = Directory.CreateTempSubdirectory("orca-planilha-reserva-");
        try
        {
            var cache = Path.Combine(raiz.FullName, "cache");
            Directory.CreateDirectory(cache);
            var atual = ArquivoExportacaoPlanilha.Caminho(cache, Instante, Sufixo);
            var outro = ArquivoExportacaoPlanilha.Caminho(cache, Instante.AddHours(-1), "11223344");
            File.WriteAllText(atual, "atual");
            File.WriteAllText(outro, "outro");
            ArquivoExportacaoPlanilha.Reservar(atual);
            try
            {
                var apagados = ArquivoExportacaoPlanilha.ApagarAnteriores(cache);

                Assert.Equal(1, apagados);
                Assert.Equal("atual", File.ReadAllText(atual));
                Assert.False(File.Exists(outro));
            }
            finally
            {
                ArquivoExportacaoPlanilha.Liberar(atual);
            }

            Assert.Equal(1, ArquivoExportacaoPlanilha.ApagarAnteriores(cache));
            Assert.False(File.Exists(atual));
        }
        finally
        {
            raiz.Delete(recursive: true);
        }
    }

    [Fact]
    public void MargemDeDezMinutos_DosDoisLados_ENomeAntigo()
    {
        var raiz = Directory.CreateTempSubdirectory("orca-planilha-margem-");
        try
        {
            var cache = Path.Combine(raiz.FullName, "cache");
            var fora = Path.Combine(raiz.FullName, "fora");
            var espelho = Path.Combine(cache, "orcafacil-espelho");
            Directory.CreateDirectory(cache);
            Directory.CreateDirectory(fora);
            Directory.CreateDirectory(espelho);

            var agora = new DateTimeOffset(2026, 10, 19, 16, 30, 7, TimeSpan.Zero);
            var fica = Path.Combine(cache, "orcafacil-20261019-163000-ab12cd34.xlsx");
            var sai = Path.Combine(cache, "orcafacil-20261019-120000-11223344.xlsx");
            var antigoFica = Path.Combine(cache, "orcafacil-20261019-162000.xlsx");
            var antigoSai = Path.Combine(cache, "orcafacil-20261018-090000.xlsx");
            var comLetra = Path.Combine(cache, "orcafacil-2026101a-090000.xlsx");
            var tamanhoErrado = Path.Combine(cache, "orcafacil-20261019-09000.xlsx");
            var csv = Path.Combine(cache, "orcafacil-relatorio-20261019-120000.csv");
            var aninhado = Path.Combine(espelho, "orcafacil-20200101-000000.xlsx");
            var alvoFora = Path.Combine(fora, "orcafacil-20200101-000000.xlsx");

            Gravar(fica, agora, TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));
            Gravar(sai, agora, TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));
            Gravar(antigoFica, agora, TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));
            Gravar(antigoSai, agora, TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));
            Gravar(comLetra, agora, TimeSpan.FromHours(2));
            Gravar(tamanhoErrado, agora, TimeSpan.FromHours(2));
            Gravar(csv, agora, TimeSpan.FromHours(2));
            Gravar(aninhado, agora, TimeSpan.FromHours(2));
            Gravar(alvoFora, agora, TimeSpan.FromHours(2));

            var apagados = ArquivoExportacaoPlanilha.ApagarVencidos(cache, agora);

            Assert.Equal(2, apagados);
            Assert.Equal("x", File.ReadAllText(fica));
            Assert.False(File.Exists(sai));
            Assert.Equal("x", File.ReadAllText(antigoFica));
            Assert.False(File.Exists(antigoSai));
            Assert.Equal("x", File.ReadAllText(comLetra));
            Assert.Equal("x", File.ReadAllText(tamanhoErrado));
            Assert.Equal("x", File.ReadAllText(csv));
            Assert.Equal("x", File.ReadAllText(aninhado));
            Assert.Equal("x", File.ReadAllText(alvoFora));

            Assert.Equal(2, ArquivoExportacaoPlanilha.ApagarAnteriores(cache));
            Assert.False(File.Exists(fica));
            Assert.False(File.Exists(antigoFica));
            Assert.Equal("x", File.ReadAllText(comLetra));
            Assert.Equal("x", File.ReadAllText(tamanhoErrado));
            Assert.Equal("x", File.ReadAllText(csv));
        }
        finally
        {
            raiz.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    [InlineData(1, false)]
    public void ApagarVencidos_ExatamenteDezMinutos_Permanece(int segundosAlemDaMargem, bool permanece)
    {
        Assert.Equal(TimeSpan.FromMinutes(10), ArquivoExportacaoPlanilha.MargemRetencao);

        var raiz = Directory.CreateTempSubdirectory("orca-planilha-limite-");
        try
        {
            var cache = Path.Combine(raiz.FullName, "cache");
            Directory.CreateDirectory(cache);

            var bruto = new DateTimeOffset(2026, 10, 22, 16, 13, 7, 456, TimeSpan.Zero);
            var agora = new DateTimeOffset(
                bruto.Year, bruto.Month, bruto.Day, bruto.Hour, bruto.Minute, bruto.Second, TimeSpan.Zero);
            var caminho = Path.Combine(cache, "orcafacil-20261022-160307-ab12cd34.xlsx");
            var escrito = (agora - ArquivoExportacaoPlanilha.MargemRetencao - TimeSpan.FromSeconds(segundosAlemDaMargem)).UtcDateTime;
            File.WriteAllText(caminho, "x");
            File.SetLastWriteTimeUtc(caminho, escrito);

            var lido = File.GetLastWriteTimeUtc(caminho);
            Assert.Equal(escrito, lido);
            if (segundosAlemDaMargem == 0)
                Assert.Equal(ArquivoExportacaoPlanilha.MargemRetencao, agora - new DateTimeOffset(DateTime.SpecifyKind(lido, DateTimeKind.Utc)));

            var apagados = ArquivoExportacaoPlanilha.ApagarVencidos(cache, agora);

            if (permanece)
            {
                Assert.Equal(0, apagados);
                Assert.Equal("x", File.ReadAllText(caminho));
            }
            else
            {
                Assert.Equal(1, apagados);
                Assert.False(File.Exists(caminho));
            }
        }
        finally
        {
            raiz.Delete(recursive: true);
        }
    }

    private static void Gravar(string caminho, DateTimeOffset agora, TimeSpan idade)
    {
        File.WriteAllText(caminho, "x");
        File.SetLastWriteTimeUtc(caminho, (agora - idade).UtcDateTime);
    }
}

public class MensagemExportacaoPlanilhaTests
{
    [Fact]
    public void AbaAcimaDoTeto_MostraAMensagemDoExportador()
    {
        var teto = PlanilhaPtBr.MaximoLinhas - 1;
        var erro = Assert.Throws<InvalidOperationException>(
            () => ExportadorPlanilha.ExigirLimiteDeDados("Lançamentos", teto + 1));

        var texto = MensagemExportacaoPlanilha.Para(erro);
        var embrulhada = MensagemExportacaoPlanilha.Para(new IOException("falha ao gravar", erro));

        Assert.Equal(erro.Message, texto);
        Assert.Equal(erro.Message, embrulhada);
        Assert.Contains("Lançamentos", texto, StringComparison.Ordinal);
        Assert.Contains((teto + 1).ToString(CultureInfo.InvariantCulture), texto, StringComparison.Ordinal);
        Assert.Contains(teto.ToString(CultureInfo.InvariantCulture), texto, StringComparison.Ordinal);
        Assert.DoesNotContain(Environment.NewLine, texto, StringComparison.Ordinal);
        Assert.DoesNotContain(erro.StackTrace ?? "STACK", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void CaminhoSenhaECancelamento_NaoVazam()
    {
        var caminho = new IOException("/data/user/0/com.pitthan.orcafacil/cache/segredo.xlsx");
        var segredo = new InvalidOperationException("A aba Categorias tem senha content://token e Espelho.Pasta.Uri");
        var rastreio = new InvalidOperationException("A aba Categorias tem 1 linha\n   at OrcaFacil.Exportador");

        var textoCaminho = MensagemExportacaoPlanilha.Para(caminho);
        var textoSegredo = MensagemExportacaoPlanilha.Para(segredo);
        var textoRastreio = MensagemExportacaoPlanilha.Para(rastreio);

        Assert.Equal(MensagemExportacaoPlanilha.Generica, textoCaminho);
        Assert.Equal(MensagemExportacaoPlanilha.Generica, textoSegredo);
        Assert.Equal(MensagemExportacaoPlanilha.Generica, textoRastreio);
        Assert.DoesNotContain("segredo", textoCaminho, StringComparison.Ordinal);
        Assert.DoesNotContain("/data", textoCaminho, StringComparison.Ordinal);
        Assert.DoesNotContain("senha", textoSegredo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("content://", textoSegredo, StringComparison.Ordinal);
        Assert.DoesNotContain("Espelho.Pasta.Uri", textoSegredo, StringComparison.Ordinal);
        Assert.DoesNotContain("at OrcaFacil", textoRastreio, StringComparison.Ordinal);
        Assert.Null(MensagemExportacaoPlanilha.Para(new OperationCanceledException()));
        Assert.Null(MensagemExportacaoPlanilha.Para(null));
        Assert.Equal(
            "Não foi possível compartilhar a planilha. Tente de novo.",
            MensagemExportacaoPlanilha.Compartilhamento);
        Assert.Equal("Arquivo pronto para compartilhar.", MensagemExportacaoPlanilha.ProntaParaCompartilhar);
        Assert.DoesNotContain("exportado", MensagemExportacaoPlanilha.ProntaParaCompartilhar, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("espelho", MensagemExportacaoPlanilha.Compartilhamento, StringComparison.OrdinalIgnoreCase);
    }
}
