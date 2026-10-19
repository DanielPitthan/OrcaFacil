namespace OrcaFacil.Tests.Llm;

public class CestaLeituraFonteTests
{
    [Theory]
    [InlineData("Task IniciarLeituraAsync()")]
    [InlineData("Task TentarDeNovoAsync()")]
    public void GuardaDeToqueDuplo_VemAntesDaTrava_EAntesDoPrimeiroAwait(string assinatura)
    {
        var pagina = File.ReadAllText(Path.Combine(EncontrarRaiz(), "OrcaFacil", "Components", "Pages", "Cesta", "CestaPage.razor"));
        var metodo = ExtrairBloco(pagina, assinatura);
        var guarda = IndiceDaCondicao(metodo, "_lendo || _descartada");
        var retorno = guarda < 0
            ? -1
            : metodo.IndexOf("return", guarda, StringComparison.Ordinal);
        var trava = metodo.IndexOf("_lendo = true;", StringComparison.Ordinal);
        var primeiroAwait = metodo.IndexOf("await", StringComparison.Ordinal);

        Assert.True(guarda >= 0, assinatura);
        Assert.True(retorno > guarda, assinatura);
        Assert.True(trava > retorno, assinatura);
        Assert.True(primeiroAwait > trava, assinatura);
    }

    [Fact]
    public void AgoraNao_RetornaAntesDeMarcarVisto()
    {
        var raiz = EncontrarRaiz();
        var dialog = File.ReadAllText(Path.Combine(
            raiz, "OrcaFacil", "Components", "Pages", "Cesta", "AvisoPrivacidadeComprovanteDialog.razor"));
        var agoraNao = ExtrairMembro(dialog, "void AgoraNao()");

        Assert.Contains("Cancel()", agoraNao, StringComparison.Ordinal);
        Assert.DoesNotContain("MarcarVisto", agoraNao, StringComparison.Ordinal);

        var pagina = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Pages", "Cesta", "CestaPage.razor"));
        var confirmar = ExtrairBloco(pagina, "Task<bool> ConfirmarAvisoAsync()");
        var retorno = confirmar.IndexOf("return false", StringComparison.Ordinal);
        var marcar = confirmar.IndexOf("MarcarVisto", StringComparison.Ordinal);

        Assert.True(retorno >= 0);
        Assert.True(marcar > retorno);
    }

    [Fact]
    public void Cadastrar_TravaAntesDoPrimeiroAwait()
    {
        var dialog = File.ReadAllText(Path.Combine(
            EncontrarRaiz(), "OrcaFacil", "Components", "Pages", "Cesta", "PreviaComprovanteDialog.razor"));
        var metodo = ExtrairBloco(dialog, "Task CadastrarAsync()");
        var guarda = IndiceDaCondicao(
            metodo,
            "_cadastrando || _cadastrandoTudo || _confirmando || _editando is null || !_editando.PodeCadastrar");
        var retorno = guarda < 0
            ? -1
            : metodo.IndexOf("return", guarda, StringComparison.Ordinal);
        var trava = metodo.IndexOf("_cadastrando = true;", StringComparison.Ordinal);
        var primeiroAwait = metodo.IndexOf("await", StringComparison.Ordinal);

        Assert.True(guarda >= 0);
        Assert.True(retorno > guarda);
        Assert.True(trava > retorno);
        Assert.True(primeiroAwait > trava);
    }

    [Fact]
    public void CadastrarTudo_TravaAntesDoPrimeiroAwait()
    {
        var dialog = File.ReadAllText(Path.Combine(
            EncontrarRaiz(), "OrcaFacil", "Components", "Pages", "Cesta", "PreviaComprovanteDialog.razor"));
        var metodo = ExtrairBloco(dialog, "Task CadastrarTudoAsync()");
        var guarda = IndiceDaCondicao(
            metodo,
            "_cadastrandoTudo || _cadastrando || _confirmando || !Previa.PodeCadastrarTudo");
        var retorno = guarda < 0
            ? -1
            : metodo.IndexOf("return", guarda, StringComparison.Ordinal);
        var trava = metodo.IndexOf("_cadastrandoTudo = true;", StringComparison.Ordinal);
        var primeiroAwait = metodo.IndexOf("await", StringComparison.Ordinal);

        Assert.True(guarda >= 0);
        Assert.True(retorno > guarda);
        Assert.True(trava > retorno);
        Assert.True(primeiroAwait < 0 || primeiroAwait > trava);
    }

    [Fact]
    public void ConfirmarPrevia_TravaEFechaSemAwait()
    {
        var dialog = File.ReadAllText(Path.Combine(
            EncontrarRaiz(), "OrcaFacil", "Components", "Pages", "Cesta", "PreviaComprovanteDialog.razor"));
        var metodo = ExtrairBloco(dialog, "void Confirmar()");
        var guarda = IndiceDaCondicao(metodo, "!Previa.PodeConfirmar || _confirmando");
        var retorno = guarda < 0
            ? -1
            : metodo.IndexOf("return", guarda, StringComparison.Ordinal);
        var trava = metodo.IndexOf("_confirmando = true;", StringComparison.Ordinal);
        var fechar = metodo.IndexOf("MudDialog.Close", StringComparison.Ordinal);

        Assert.True(guarda >= 0);
        Assert.True(retorno > guarda);
        Assert.True(trava > retorno);
        Assert.True(fechar > trava);
        Assert.DoesNotContain("await", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviaAberta_TravaAntesDoPrimeiroAwait()
    {
        var pagina = File.ReadAllText(Path.Combine(
            EncontrarRaiz(), "OrcaFacil", "Components", "Pages", "Cesta", "CestaPage.razor"));
        var metodo = ExtrairBloco(pagina, "Task AbrirPreviaAsync()");
        var guarda = IndiceDaCondicao(metodo, "_comprovante is null || _descartada || _previaAberta");
        var retorno = guarda < 0
            ? -1
            : metodo.IndexOf("return", guarda, StringComparison.Ordinal);
        var trava = metodo.IndexOf("_previaAberta = true;", StringComparison.Ordinal);
        var primeiroAwait = metodo.IndexOf("await", StringComparison.Ordinal);

        Assert.True(guarda >= 0);
        Assert.True(retorno > guarda);
        Assert.True(trava > retorno);
        Assert.True(primeiroAwait > trava);
    }

    [Fact]
    public void SalvarCompra_DbUpdateException_ViraSnackbarSemStack()
    {
        var pagina = File.ReadAllText(Path.Combine(
            EncontrarRaiz(), "OrcaFacil", "Components", "Pages", "Cesta", "CestaPage.razor"));
        var metodo = ExtrairBloco(pagina, "Task<bool> SalvarCompraAsync(");
        var nome = metodo.IndexOf("when (CestaCadastroService.NomeDeProdutoRepetido(", StringComparison.Ordinal);
        Assert.True(nome >= 0);
        var blocoNome = ExtrairChaves(metodo, metodo.IndexOf('{', nome));
        Assert.Contains("MensagemNomeProdutoRepetido", blocoNome, StringComparison.Ordinal);
        Assert.DoesNotContain("MensagemFalhaAoGravar", blocoNome, StringComparison.Ordinal);
        Assert.DoesNotContain("ex.Message", blocoNome, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", blocoNome, StringComparison.Ordinal);
        Assert.DoesNotContain("ToString()", blocoNome, StringComparison.Ordinal);

        var generico = metodo.IndexOf("catch (DbUpdateException)", nome + blocoNome.Length, StringComparison.Ordinal);
        Assert.True(generico > nome);
        var blocoGenerico = ExtrairChaves(metodo, metodo.IndexOf('{', generico));
        Assert.Contains("MensagemFalhaAoGravar", blocoGenerico, StringComparison.Ordinal);
        Assert.DoesNotContain("MensagemNomeProdutoRepetido", blocoGenerico, StringComparison.Ordinal);
        Assert.DoesNotContain("ex.Message", blocoGenerico, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", blocoGenerico, StringComparison.Ordinal);
        Assert.DoesNotContain("ToString()", blocoGenerico, StringComparison.Ordinal);

        var servico = File.ReadAllText(Path.Combine(EncontrarRaiz(), "OrcaFacil.Core", "Services", "CestaCadastroService.cs"));
        Assert.Contains(
            "public const string MensagemNomeProdutoRepetido = \"Já existe um produto com esse nome.\";",
            servico,
            StringComparison.Ordinal);
        Assert.Contains(
            "public const string MensagemFalhaAoGravar = \"Não foi possível gravar a compra.\";",
            servico,
            StringComparison.Ordinal);
        Assert.Contains("when (NomeDeProdutoRepetido(ex))", servico, StringComparison.Ordinal);
    }

    [Fact]
    public void DialogoDaCompra_CopiaPelaFuncaoPuraNosDoisPontos()
    {
        var dialog = File.ReadAllText(Path.Combine(
            EncontrarRaiz(), "OrcaFacil", "Components", "Pages", "Cesta", "CompraCestaDialog.razor"));
        var abertura = ExtrairBloco(dialog, "Task OnInitializedAsync()");
        var salvar = ExtrairBloco(dialog, "void Salvar()");

        Assert.Contains("CompraCestaItens.CopiarItens(", abertura, StringComparison.Ordinal);
        Assert.Contains("CompraCestaItens.CopiarItens(", salvar, StringComparison.Ordinal);
        Assert.DoesNotContain("ProdutoCestaId = item.ProdutoCestaId", abertura, StringComparison.Ordinal);
        Assert.DoesNotContain("ProdutoCestaId = linha.ProdutoCestaId", salvar, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("if (false && (_lendo || _descartada)) return;", "_lendo || _descartada")]
    [InlineData("if (!(_lendo || _descartada)) return;", "_lendo || _descartada")]
    [InlineData("if (!_lendo || _descartada) return;", "_lendo || _descartada")]
    [InlineData("if (_cadastrandoTudo) return;", "_cadastrando")]
    [InlineData("if (false && (_cadastrandoTudo || _cadastrando || _confirmando || !Previa.PodeCadastrarTudo)) return;", "_cadastrandoTudo || _cadastrando || _confirmando || !Previa.PodeCadastrarTudo")]
    public void Guarda_RejeitaFalseNegacaoEPrefixo(string metodo, string condicao)
    {
        Assert.Equal(-1, IndiceDaCondicao("void M() { " + metodo + " }", condicao));
    }

    [Theory]
    [InlineData("if (_lendo || _descartada) return;", "_lendo || _descartada")]
    [InlineData("if (  _lendo   ||   _descartada  ) return;", "_lendo || _descartada")]
    [InlineData("if (!Previa.PodeConfirmar || _confirmando) return;", "!Previa.PodeConfirmar || _confirmando")]
    public void Guarda_AceitaOTextoExatoComEspaco(string metodo, string condicao)
    {
        Assert.True(IndiceDaCondicao("void M() { " + metodo + " }", condicao) >= 0);
    }

    [Fact]
    public void Contraste_LocalCategoriaEErroDoSheet()
    {
        var raiz = EncontrarRaiz();
        var tema = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "wwwroot", "css", "orcafacil-theme.css"));
        var lista = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Shared", "ItemList.razor.css"));
        var sheet = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Components", "Shared", "ItemSheet.razor.css"));
        var sub = ExtrairRegra(tema, ".of-appbar-sub");
        var categoria = ExtrairRegra(lista, ".of-lista-sub");

        var titulo = ExtrairRegra(lista, ".of-lista-titulo");
        Assert.Contains("color: var(--of-text-secondary);", sub, StringComparison.Ordinal);
        Assert.Contains("color: var(--of-text-secondary);", categoria, StringComparison.Ordinal);
        Assert.Contains("color: var(--of-text);", titulo, StringComparison.Ordinal);
        Assert.True(Razao("#1E2A33", "#ffffff") >= 4.5);
        Assert.True(Razao("#1E2A33", "#EFF3F7") >= 4.5);
        Assert.True(Razao("#E8EAF0", "#1B1F27") >= 4.5);
        Assert.True(Razao("#E8EAF0", "#12151B") >= 4.5);
        Assert.Contains("color: #9B2C3A;", sheet, StringComparison.Ordinal);
        Assert.Contains(".of-lista-item-erro", lista, StringComparison.Ordinal);
        Assert.Contains("#9B2C3A", lista, StringComparison.Ordinal);
        Assert.Contains("#F59A9A", lista, StringComparison.Ordinal);
        Assert.True(Razao("#5A6B78", "#ffffff") >= 4.5);
        Assert.True(Razao("#5A6B78", "#EFF3F7") >= 4.5);
        Assert.True(Razao("#C9D1DD", "#1B1F27") >= 4.5);
        Assert.True(Razao("#C9D1DD", "#12151B") >= 4.5);
        Assert.True(Razao("#9B2C3A", "#ffffff") >= 4.5);
        Assert.True(Razao("#F59A9A", "#1B1F27") >= 4.5);
    }

    private static string ExtrairRegra(string css, string seletor)
    {
        var indice = css.IndexOf(seletor, StringComparison.Ordinal);
        Assert.True(indice >= 0, seletor);
        var abre = css.IndexOf('{', indice);
        var fecha = css.IndexOf('}', abre);
        return css[abre..(fecha + 1)];
    }

    private static double Razao(string frente, string fundo)
    {
        var clara = Math.Max(Luz(frente), Luz(fundo));
        var escura = Math.Min(Luz(frente), Luz(fundo));
        return (clara + 0.05) / (escura + 0.05);
    }

    private static double Luz(string hex)
    {
        hex = hex.TrimStart('#');
        double Canal(string trecho)
        {
            var valor = Convert.ToInt32(trecho, 16) / 255d;
            return valor <= 0.04045 ? valor / 12.92 : Math.Pow((valor + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Canal(hex[..2])) + (0.7152 * Canal(hex.Substring(2, 2))) + (0.0722 * Canal(hex.Substring(4, 2)));
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
        return System.Text.RegularExpressions.Regex.IsMatch(encontrada.Trim(), @"\A" + padrao + @"\z");
    }

    private static string TokenDeCondicao(string token)
    {
        if (token.StartsWith('!') && token.Length > 1 && (token[1] == '_' || char.IsLetter(token[1])))
            return @"(?<![\w])!" + System.Text.RegularExpressions.Regex.Escape(token[1..]) + @"(?![\w])";

        if (token[0] == '_' || char.IsLetter(token[0]))
            return @"(?<![\w])" + System.Text.RegularExpressions.Regex.Escape(token) + @"(?![\w])";

        return System.Text.RegularExpressions.Regex.Escape(token);
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

    private static string ExtrairMembro(string fonte, string assinatura)
    {
        var indice = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(indice >= 0, assinatura);
        var i = indice + assinatura.Length;
        while (i < fonte.Length && char.IsWhiteSpace(fonte[i]))
            i++;

        if (i < fonte.Length && fonte[i] == '{')
            return ExtrairChaves(fonte, i);

        var fim = fonte.IndexOf(';', i);
        Assert.True(fim > i, assinatura);
        return fonte[indice..(fim + 1)];
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
}
