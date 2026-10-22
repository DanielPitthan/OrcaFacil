using System.Text.RegularExpressions;

namespace OrcaFacil.Tests.Lancamentos;

public class LancamentoCartaoFonteTests
{
    [Fact]
    public void Seletores_AbremPeloContextoDoMenu_ENaoZeramARecorrencia()
    {
        var painel = File.ReadAllText(Path.Combine(
            EncontrarRaiz(), "OrcaFacil", "Components", "Pages", "Lancamentos", "FaturaCartaoPainel.razor"));

        var cartao = Antes(painel, "AriaLabel=\"Selecionar cartão\"", 500);
        var recorrencia = Antes(painel, "AriaLabel=\"Selecionar recorrência\"", 500);
        var escolherCartao = ExtrairBloco(painel, "Task SelecionarCartaoAsync(");
        var escolherRecorrencia = ExtrairBloco(painel, "void EscolherRecorrencia(");

        Assert.Contains("ToggleAsync", cartao, StringComparison.Ordinal);
        Assert.Contains("ToggleAsync", recorrencia, StringComparison.Ordinal);
        Assert.Contains("FluxoLancamento.EscolherCartao(", escolherCartao, StringComparison.Ordinal);
        Assert.DoesNotContain("Recorrencia", escolherCartao, StringComparison.Ordinal);
        Assert.Contains("FluxoLancamento.EscolherRecorrencia(", escolherRecorrencia, StringComparison.Ordinal);
        Assert.DoesNotContain("CartaoId", escolherRecorrencia, StringComparison.Ordinal);
    }

    [Fact]
    public void NovoLancamento_ZeraNoOnParametersSet()
    {
        var formulario = File.ReadAllText(Path.Combine(
            EncontrarRaiz(), "OrcaFacil", "Components", "Pages", "Lancamentos", "LancamentoForm.razor"));
        var parameters = ExtrairBloco(formulario, "Task OnParametersSetAsync()");
        var sessao = ExtrairBloco(formulario, "Task AbrirSessaoAsync()");

        Assert.Contains("AbrirSessaoAsync", parameters, StringComparison.Ordinal);
        var trava = parameters.IndexOf("if (_sessaoPronta && _idAplicado == Id)", StringComparison.Ordinal);
        var arma = parameters.IndexOf("_sessaoPronta = true;", StringComparison.Ordinal);
        var id = parameters.IndexOf("_idAplicado = Id;", StringComparison.Ordinal);
        var abreSessao = parameters.IndexOf("await AbrirSessaoAsync()", StringComparison.Ordinal);
        Assert.True(trava >= 0);
        Assert.True(arma >= 0);
        Assert.True(id >= 0);
        Assert.True(abreSessao >= 0);
        Assert.True(trava < arma && arma < id && id < abreSessao);
        Assert.True(IndiceDaCondicao(parameters, "_sessaoPronta && _idAplicado == Id") >= 0);
        var abre = parameters.IndexOf('(', trava);
        var corpo = ConteudoParenteses(parameters, abre);
        var resto = parameters[(abre + corpo.Length + 2)..].TrimStart();
        Assert.Matches(@"^(?:return\s*;|\{\s*return\s*;)", resto);

        var consumir = sessao.IndexOf("Consumir", StringComparison.Ordinal);
        Assert.Contains("FluxoLancamento.IniciarNovo(", sessao[consumir..], StringComparison.Ordinal);
    }

    [Fact]
    public void GarantirCategoriaValida_PulaOCartao()
    {
        var formulario = File.ReadAllText(Path.Combine(
            EncontrarRaiz(), "OrcaFacil", "Components", "Pages", "Lancamentos", "LancamentoForm.razor"));
        var bloco = ExtrairBloco(formulario, "void GarantirCategoriaValida()");

        Assert.Contains("FluxoLancamento.CategoriaInicial(", bloco, StringComparison.Ordinal);
        Assert.DoesNotContain("filtradas[0].Id", bloco, StringComparison.Ordinal);
    }

    [Fact]
    public void EdicaoInexistente_ZeraOModeloAnterior()
    {
        var formulario = File.ReadAllText(Path.Combine(
            EncontrarRaiz(), "OrcaFacil", "Components", "Pages", "Lancamentos", "LancamentoForm.razor"));
        var sessao = ExtrairBloco(formulario, "Task AbrirSessaoAsync()");
        var obter = sessao.IndexOf("ObterAsync", StringComparison.Ordinal);
        var consumir = sessao.IndexOf("Consumir", StringComparison.Ordinal);
        var entre = sessao[obter..consumir];
        var marca = entre.IndexOf("transacao is not null", StringComparison.Ordinal);
        var abreSucesso = entre.IndexOf('{', marca);
        var sucesso = ExtrairChaves(entre, abreSucesso);
        var depois = entre[(abreSucesso + sucesso.Length)..];
        var senao = ExtrairChaves(depois, depois.IndexOf('{', depois.IndexOf("else", StringComparison.Ordinal)));

        Assert.DoesNotContain("IniciarNovo", sucesso, StringComparison.Ordinal);
        Assert.Contains("FluxoLancamento.IniciarNovo(", senao, StringComparison.Ordinal);
        Assert.Contains("Lançamento não encontrado.", senao, StringComparison.Ordinal);
        Assert.Contains("Voltar()", senao, StringComparison.Ordinal);
        Assert.DoesNotContain("CriarAsync", senao, StringComparison.Ordinal);
        Assert.DoesNotContain("AtualizarAsync", senao, StringComparison.Ordinal);
    }

    [Fact]
    public void Ativador_UmSoBotaoComAriaLabel()
    {
        var raiz = EncontrarRaiz();
        var painel = File.ReadAllText(Path.Combine(
            raiz, "OrcaFacil", "Components", "Pages", "Lancamentos", "FaturaCartaoPainel.razor"));
        var script = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "wwwroot", "js", "menuFoco.js"));
        var ativadores = Trechos(painel, "<ActivatorContent", "</ActivatorContent>");

        Assert.Equal(2, ativadores.Count);
        Assert.Contains("aria-label=\"Selecionar cartão\"", ativadores[0], StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Selecionar recorrência\"", ativadores[1], StringComparison.Ordinal);
        foreach (var ativador in ativadores)
        {
            Assert.Equal(1, Contar(ativador, "<button"));
            Assert.Contains("Embutido=\"true\"", ativador, StringComparison.Ordinal);
            Assert.DoesNotContain("tabindex", ativador, StringComparison.OrdinalIgnoreCase);
            var botao = ativador[ativador.IndexOf("<button", StringComparison.Ordinal)..];
            Assert.Contains("aria-label=\"", botao, StringComparison.Ordinal);
        }

        Assert.Contains("OrcaFacilMenu.umFoco", painel, StringComparison.Ordinal);
        Assert.Contains("tabIndex = -1", script, StringComparison.Ordinal);
        Assert.Contains("removeAttribute(\"role\")", script, StringComparison.Ordinal);
    }

    private static string Antes(string fonte, string marca, int tamanho)
    {
        var indice = fonte.IndexOf(marca, StringComparison.Ordinal);
        Assert.True(indice >= 0, marca);
        var inicio = Math.Max(0, indice - tamanho);
        return fonte[inicio..indice];
    }

    private static string ExtrairBloco(string fonte, string assinatura)
    {
        var indice = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(indice >= 0, assinatura);
        return ExtrairChaves(fonte, fonte.IndexOf('{', indice));
    }

    private static string ExtrairChaves(string fonte, int abre)
    {
        var profundidade = 0;
        for (var i = abre; i < fonte.Length; i++)
        {
            if (fonte[i] == '{')
                profundidade++;
            else if (fonte[i] == '}')
            {
                profundidade--;
                if (profundidade == 0)
                    return fonte[abre..(i + 1)];
            }
        }

        throw new InvalidOperationException("Bloco sem fechamento.");
    }

    private static string EncontrarRaiz()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null)
        {
            if (File.Exists(Path.Combine(diretorio.FullName, "OrcaFacil.slnx")))
                return diretorio.FullName;

            diretorio = diretorio.Parent;
        }

        throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }

    private static List<string> Trechos(string fonte, string inicio, string fim)
    {
        var lista = new List<string>();
        var cursor = 0;
        while (true)
        {
            var abre = fonte.IndexOf(inicio, cursor, StringComparison.Ordinal);
            if (abre < 0)
                return lista;

            var fecha = fonte.IndexOf(fim, abre, StringComparison.Ordinal);
            Assert.True(fecha > abre);
            lista.Add(fonte[abre..(fecha + fim.Length)]);
            cursor = fecha + fim.Length;
        }
    }

    private static int Contar(string fonte, string trecho)
    {
        var total = 0;
        var cursor = 0;
        while ((cursor = fonte.IndexOf(trecho, cursor, StringComparison.Ordinal)) >= 0)
        {
            total++;
            cursor += trecho.Length;
        }

        return total;
    }

    private static int IndiceDaCondicao(string metodo, string condicao)
    {
        foreach (var (indice, texto) in CondicoesIf(metodo))
        {
            if (CondicaoIgual(texto, condicao))
                return indice;
        }

        return -1;
    }

    private static bool CondicaoIgual(string encontrada, string esperada)
    {
        var tokens = esperada.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var padrao = string.Join(@"\s+", tokens.Select(TokenDeCondicao));
        return Regex.IsMatch(encontrada.Trim(), @"\A" + padrao + @"\z");
    }

    private static string TokenDeCondicao(string token)
    {
        if (token.StartsWith('!') && token.Length > 1 && (token[1] == '_' || char.IsLetter(token[1])))
            return @"(?<![\w])!" + Regex.Escape(token[1..]) + @"(?![\w])";

        if (token[0] == '_' || char.IsLetter(token[0]))
            return @"(?<![\w])" + Regex.Escape(token) + @"(?![\w])";

        return Regex.Escape(token);
    }

    private static IEnumerable<(int Indice, string Texto)> CondicoesIf(string metodo)
    {
        var indice = 0;
        while ((indice = metodo.IndexOf("if", indice, StringComparison.Ordinal)) >= 0)
        {
            if (indice > 0 && (char.IsLetterOrDigit(metodo[indice - 1]) || metodo[indice - 1] == '_'))
            {
                indice += 2;
                continue;
            }

            var cursor = indice + 2;
            if (cursor < metodo.Length && (char.IsLetterOrDigit(metodo[cursor]) || metodo[cursor] == '_'))
            {
                indice += 2;
                continue;
            }

            while (cursor < metodo.Length && char.IsWhiteSpace(metodo[cursor]))
                cursor++;

            if (cursor >= metodo.Length || metodo[cursor] != '(')
            {
                indice += 2;
                continue;
            }

            yield return (indice, ConteudoParenteses(metodo, cursor));
            indice = cursor + 1;
        }
    }

    private static string ConteudoParenteses(string fonte, int abre)
    {
        var profundidade = 0;
        for (var i = abre; i < fonte.Length; i++)
        {
            if (fonte[i] == '(')
                profundidade++;
            else if (fonte[i] == ')')
            {
                profundidade--;
                if (profundidade == 0)
                    return fonte[(abre + 1)..i];
            }
        }

        throw new InvalidOperationException("Condição sem fechamento.");
    }
}
