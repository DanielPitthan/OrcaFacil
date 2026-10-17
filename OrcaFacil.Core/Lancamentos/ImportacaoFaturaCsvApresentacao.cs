using System.Globalization;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Services;

namespace OrcaFacil.Core.Lancamentos;

public enum TomMensagemImportacaoFatura
{
    Neutro,
    Aviso,
    Erro
}

public sealed record MensagemImportacaoFaturaCsv(string Texto, TomMensagemImportacaoFatura Tom);

public sealed record LinhaImportacaoFaturaCsvTela(
    int Numero,
    int CategoriaId,
    string Titulo,
    string Subtitulo,
    string Valor,
    bool EhEstorno,
    bool Duplicata,
    bool Selecionada,
    string? EscolhaDuplicata,
    string Aria);

public sealed record ErroImportacaoFaturaCsvTela(int Numero, string Coluna, string Mensagem, string Texto);

public sealed record TotaisImportacaoFaturaCsvTela(
    decimal Compras,
    decimal Estornos,
    decimal Liquido,
    string ComprasTexto,
    string EstornosTexto,
    string LiquidoTexto,
    bool EstornosEmCredito);

public sealed record VistaImportacaoFaturaCsv(
    IReadOnlyList<LinhaImportacaoFaturaCsvTela> Linhas,
    IReadOnlyList<ErroImportacaoFaturaCsvTela> Erros,
    TotaisImportacaoFaturaCsvTela Totais,
    MensagemImportacaoFaturaCsv? Bloqueio,
    string? AvisoDuplicata,
    string TextoSelecao,
    bool PodeConfirmar);

/// <summary>
/// Ignora o segundo toque enquanto o primeiro ainda não terminou.
/// A tela desabilita o botão; esta trava cobre o toque que chega no mesmo instante.
/// </summary>
public sealed class GuardaToque
{
    public bool EmAndamento { get; private set; }

    public bool Tentar()
    {
        if (EmAndamento)
            return false;

        EmAndamento = true;
        return true;
    }

    public void Liberar() => EmAndamento = false;
}

/// <summary>
/// O que a tela de importação mostra e decide, sem arquivo nem banco.
/// A duplicata da prévia pode ser da fatura ou de uma linha anterior do próprio arquivo.
/// Se a linha anterior sair da seleção, a seguinte deixa de ser duplicata e pode entrar.
/// </summary>
public static class ImportacaoFaturaCsvApresentacao
{
    public const string TextoAcao = "Importar CSV";
    public const string TextoBaixarModelo = "Baixar modelo";
    public const string TextoEscolherArquivo = "Escolher arquivo";
    public const string TextoConfirmar = "Confirmar importação";
    public const string TextoLendo = "Lendo o arquivo…";
    public const string TextoImportando = "Importando…";
    public const string Titulo = "Importar CSV";
    public const string TituloCompartilharModelo = "Modelo de fatura OrcaFacil";

    public const string MensagemSalvarAntes = "Salve a fatura antes de importar o CSV.";
    public const string MensagemMesFechado = "O mês desta fatura está fechado. Reabra o período para importar.";
    public const string MensagemNadaSelecionado = "Selecione ao menos uma linha para importar.";
    public const string MensagemSemLinhas = "Nenhuma linha para importar. As linhas de exemplo do modelo são ignoradas.";
    public const string MensagemLinhasComErro = "O arquivo tem linhas com erro. Nada foi importado.";
    public const string MensagemIncluirDuplicata = "Incluir uma duplicata recusa a importação inteira. Desmarque para ignorar.";
    public const string MensagemEscolhaArquivo = "Baixe o modelo ou escolha um arquivo CSV.";
    public const string MensagemFalha = "Não foi possível importar o CSV. Tente de novo.";
    public const string MensagemSeletor = "Não foi possível abrir o seletor de arquivos.";

    public static string MensagemFalhaModelo(string detalhe)
        => $"Não foi possível compartilhar o modelo. {detalhe}";

    public static byte[] BytesDoModelo()
        => FaturaCsvLayout.Codificar(FaturaCsvLayout.GerarModelo());

    public static HashSet<int> SelecaoInicial(PreviaImportacaoFaturaCsv previa)
    {
        ArgumentNullException.ThrowIfNull(previa);
        return previa.LinhasValidas.Where(l => !l.Duplicada).Select(l => l.Numero).ToHashSet();
    }

    public static HashSet<int> Alternar(IReadOnlySet<int> selecionadas, int numero)
    {
        ArgumentNullException.ThrowIfNull(selecionadas);
        var proxima = selecionadas.ToHashSet();
        if (!proxima.Remove(numero))
            proxima.Add(numero);

        return proxima;
    }

    public static VistaImportacaoFaturaCsv Montar(
        PreviaImportacaoFaturaCsv previa,
        IReadOnlySet<int> selecionadas,
        CultureInfo cultura,
        bool mesFechadoNaTela = false)
    {
        ArgumentNullException.ThrowIfNull(previa);
        ArgumentNullException.ThrowIfNull(selecionadas);
        ArgumentNullException.ThrowIfNull(cultura);

        var classificadas = Classificar(previa.LinhasValidas, selecionadas);
        var importaveis = classificadas
            .Where(c => c.Selecionada && !c.DuplicataEfetiva)
            .Select(c => c.Linha)
            .ToList();
        var bloqueio = Bloqueio(previa, classificadas, mesFechadoNaTela);
        var aviso = classificadas.Any(c => c.Selecionada && c.DuplicataEfetiva)
            ? MensagemIncluirDuplicata
            : null;
        var marcadas = classificadas.Count(c => c.Selecionada);

        return new VistaImportacaoFaturaCsv(
            classificadas.Select(c => MontarLinha(c, cultura)).ToList(),
            previa.Erros.Select(MontarErro).ToList(),
            MontarTotais(importaveis, cultura),
            bloqueio,
            aviso,
            classificadas.Count == 0 ? "" : $"{marcadas} de {classificadas.Count} linhas selecionadas",
            PodeConfirmar: bloqueio is null);
    }

    public static PedidoConfirmacaoImportacaoFaturaCsv MontarPedido(
        PreviaImportacaoFaturaCsv previa,
        IReadOnlySet<int> selecionadas)
    {
        ArgumentNullException.ThrowIfNull(previa);
        ArgumentNullException.ThrowIfNull(selecionadas);

        var classificadas = Classificar(previa.LinhasValidas, selecionadas);
        if (classificadas.Any(c => c.Selecionada && c.DuplicataEfetiva))
        {
            var escolhidas = classificadas.Where(c => c.Selecionada).Select(c => c.Linha.Numero).ToList();
            return new PedidoConfirmacaoImportacaoFaturaCsv(escolhidas, IgnorarDuplicatas: false);
        }

        var novas = classificadas
            .Where(c => c.Selecionada && !c.DuplicataEfetiva)
            .Select(c => c.Linha.Numero)
            .ToList();
        return new PedidoConfirmacaoImportacaoFaturaCsv(novas, IgnorarDuplicatas: true);
    }

    public static int ContarDuplicatasIgnoradas(PreviaImportacaoFaturaCsv previa, IReadOnlySet<int> selecionadas)
    {
        ArgumentNullException.ThrowIfNull(previa);
        ArgumentNullException.ThrowIfNull(selecionadas);
        return Classificar(previa.LinhasValidas, selecionadas).Count(c => c.DuplicataEfetiva && !c.Selecionada);
    }

    public static string Resumo(int gravados, int duplicatasIgnoradas)
    {
        var itens = gravados == 1 ? "1 item importado" : $"{gravados} itens importados";
        var duplicatas = duplicatasIgnoradas == 1
            ? "1 duplicata ignorada"
            : $"{duplicatasIgnoradas} duplicatas ignoradas";
        return $"{itens}, {duplicatas}";
    }

    public static string ResumoDaConfirmacao(
        ResultadoImportacaoFaturaCsv resultado,
        PreviaImportacaoFaturaCsv previa,
        IReadOnlySet<int> selecionadas)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        return Resumo(
            resultado.Gravados,
            resultado.DuplicatasIgnoradas + ContarDuplicatasIgnoradas(previa, selecionadas));
    }

    public static void AplicarNaFatura(TransacaoFormModel modelo, decimal valor, IEnumerable<ItemLancamento>? itens)
    {
        ArgumentNullException.ThrowIfNull(modelo);
        modelo.Valor = valor;
        modelo.Itens = CopiarItens(itens);
    }

    public static List<ItemLancamentoFormModel> CopiarItens(IEnumerable<ItemLancamento>? itens)
        => (itens ?? [])
            .OrderBy(i => i.Ordem)
            .ThenBy(i => i.Id)
            .Select(i => new ItemLancamentoFormModel
            {
                Id = i.Id,
                Descricao = i.Descricao ?? string.Empty,
                Descritivo = i.Descritivo,
                Valor = i.Valor,
                EhEstorno = i.EhEstorno,
                Natureza = i.Natureza,
                ParcelaAtual = i.ParcelaAtual,
                TotalParcelas = i.TotalParcelas,
                CategoriaId = i.CategoriaId,
                Ordem = i.Ordem
            })
            .ToList();

    private sealed record Classificacao(LinhaPreviaImportacaoFaturaCsv Linha, bool DuplicataEfetiva, bool Selecionada);

    private static List<Classificacao> Classificar(
        IReadOnlyList<LinhaPreviaImportacaoFaturaCsv> linhas,
        IReadOnlySet<int> selecionadas)
    {
        var daFatura = HashesDaFatura(linhas);
        var jaSelecionados = new HashSet<string>(StringComparer.Ordinal);
        var resultado = new List<Classificacao>(linhas.Count);
        foreach (var linha in linhas)
        {
            var efetiva = daFatura.Contains(linha.Hash) || jaSelecionados.Contains(linha.Hash);
            var marcada = selecionadas.Contains(linha.Numero);
            if (marcada && !efetiva)
                jaSelecionados.Add(linha.Hash);

            resultado.Add(new Classificacao(linha, efetiva, marcada));
        }

        return resultado;
    }

    /// <summary>
    /// Hash que já está na fatura: a primeira ocorrência no arquivo veio marcada como duplicata
    /// sem uma linha anterior com o mesmo hash.
    /// </summary>
    private static HashSet<string> HashesDaFatura(IReadOnlyList<LinhaPreviaImportacaoFaturaCsv> linhas)
    {
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var daFatura = new HashSet<string>(StringComparer.Ordinal);
        foreach (var linha in linhas)
        {
            if (linha.Duplicada && !vistos.Contains(linha.Hash))
                daFatura.Add(linha.Hash);

            vistos.Add(linha.Hash);
        }

        return daFatura;
    }

    private static MensagemImportacaoFaturaCsv? Bloqueio(
        PreviaImportacaoFaturaCsv previa,
        IReadOnlyList<Classificacao> classificadas,
        bool mesFechadoNaTela)
    {
        if (!previa.ArquivoAceito)
        {
            return new MensagemImportacaoFaturaCsv(
                string.IsNullOrWhiteSpace(previa.ErroArquivo)
                    ? "O arquivo não pôde ser lido. Nada foi importado."
                    : previa.ErroArquivo,
                TomMensagemImportacaoFatura.Erro);
        }

        if (previa.Erros.Count > 0)
            return new MensagemImportacaoFaturaCsv(MensagemLinhasComErro, TomMensagemImportacaoFatura.Erro);

        if (previa.MesFechado || mesFechadoNaTela)
            return new MensagemImportacaoFaturaCsv(MensagemMesFechado, TomMensagemImportacaoFatura.Aviso);

        if (classificadas.Count == 0)
            return new MensagemImportacaoFaturaCsv(MensagemSemLinhas, TomMensagemImportacaoFatura.Aviso);

        var incluiDuplicata = classificadas.Any(c => c.Selecionada && c.DuplicataEfetiva);
        var temNova = classificadas.Any(c => c.Selecionada && !c.DuplicataEfetiva);
        if (incluiDuplicata || temNova || classificadas.All(c => c.DuplicataEfetiva))
            return null;

        return new MensagemImportacaoFaturaCsv(MensagemNadaSelecionado, TomMensagemImportacaoFatura.Aviso);
    }

    private static LinhaImportacaoFaturaCsvTela MontarLinha(Classificacao classificacao, CultureInfo cultura)
    {
        var linha = classificacao.Linha;
        var titulo = FaturaCartaoEditor.TituloLinha(linha.Descritivo, linha.Descricao, linha.NomeCategoria, linha.EhEstorno);
        var detalhe = FaturaCartaoEditor.SubtituloLinha(
            linha.NomeCategoria, linha.EhEstorno, linha.Natureza, linha.ParcelaAtual, linha.TotalParcelas);
        var data = linha.Data.ToString("dd/MM/yyyy", cultura);
        string? escolha = null;
        var aria = classificacao.Selecionada
            ? $"Remover a linha {linha.Numero} da importação"
            : $"Incluir a linha {linha.Numero} na importação";
        if (classificacao.DuplicataEfetiva)
        {
            escolha = classificacao.Selecionada ? "Incluir" : "Ignorar";
            aria = classificacao.Selecionada
                ? $"Incluir a duplicata da linha {linha.Numero}"
                : $"Ignorar a duplicata da linha {linha.Numero}";
        }

        return new LinhaImportacaoFaturaCsvTela(
            linha.Numero,
            linha.CategoriaId,
            titulo,
            $"Linha {linha.Numero} · {data} · {detalhe}",
            FormatadorMoeda.FormatarItemFatura(linha.Valor, linha.EhEstorno, cultura),
            linha.EhEstorno,
            classificacao.DuplicataEfetiva,
            classificacao.Selecionada,
            escolha,
            aria);
    }

    private static ErroImportacaoFaturaCsvTela MontarErro(FaturaCsv.ErroLinha erro)
        => new(
            erro.Numero,
            erro.Coluna,
            erro.Mensagem,
            $"Linha {erro.Numero} · {erro.Coluna}: {erro.Mensagem}");

    private static TotaisImportacaoFaturaCsvTela MontarTotais(
        IReadOnlyList<LinhaPreviaImportacaoFaturaCsv> importaveis,
        CultureInfo cultura)
    {
        var compras = EstornoFaturaService.Arredondar(importaveis.Where(l => !l.EhEstorno).Sum(l => l.Valor));
        var estornos = EstornoFaturaService.Arredondar(importaveis.Where(l => l.EhEstorno).Sum(l => l.Valor));
        var liquido = EstornoFaturaService.Arredondar(compras - estornos);
        var estornosEmCredito = estornos > 0;
        return new TotaisImportacaoFaturaCsvTela(
            compras,
            estornos,
            liquido,
            FormatadorMoeda.Formatar(compras, SinalMoeda.Neutro, cultura),
            estornosEmCredito
                ? FormatadorMoeda.Formatar(estornos, SinalMoeda.Credito, cultura)
                : FormatadorMoeda.Formatar(estornos, SinalMoeda.Neutro, cultura),
            FormatadorMoeda.FormatarSaldo(liquido, cultura),
            estornosEmCredito);
    }
}
