using System.Text;
using System.Text.RegularExpressions;

namespace OrcaFacil.Tests.Lancamentos;

public class LancamentoCartaoFonteTests
{
    [Fact]
    public void Seletores_AbremPeloContextoDoMenu_ENaoZeramARecorrencia()
    {
        var painel = Fonte("OrcaFacil", "Components", "Pages", "Lancamentos", "FaturaCartaoPainel.razor");

        var cartao = Antes(painel, "Rotulo=\"Cartão\"", 500);
        var recorrencia = Antes(painel, "Rotulo=\"Recorrência\"", 500);
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
        var formulario = Fonte("OrcaFacil", "Components", "Pages", "Lancamentos", "LancamentoForm.razor");
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
        var formulario = Fonte("OrcaFacil", "Components", "Pages", "Lancamentos", "LancamentoForm.razor");
        var bloco = ExtrairBloco(formulario, "void GarantirCategoriaValida()");

        Assert.Contains("FluxoLancamento.CategoriaInicial(", bloco, StringComparison.Ordinal);
        Assert.DoesNotContain("filtradas[0].Id", bloco, StringComparison.Ordinal);
    }

    [Fact]
    public void EdicaoInexistente_ZeraOModeloAnterior()
    {
        var formulario = Fonte("OrcaFacil", "Components", "Pages", "Lancamentos", "LancamentoForm.razor");
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
        Assert.True(RetornaLogoApos(senao, "Voltar()"));
        Assert.DoesNotContain("_carregando = false", senao, StringComparison.Ordinal);
        Assert.DoesNotContain("CriarAsync", senao, StringComparison.Ordinal);
        Assert.DoesNotContain("AtualizarAsync", senao, StringComparison.Ordinal);
    }

    [Fact]
    public void EdicaoInexistente_ReturnLogoDepoisDoVoltar_EComentarioNaoConta()
    {
        var formulario = Fonte("OrcaFacil", "Components", "Pages", "Lancamentos", "LancamentoForm.razor");
        var senao = RamoTransacaoNula(formulario);
        Assert.True(RetornaLogoApos(senao, "Voltar()"));

        var semReturn = senao.Replace("return;", "", StringComparison.Ordinal);
        Assert.False(RetornaLogoApos(semReturn, "Voltar()"));

        const string soComentario = """
            Voltar();
            // return;
            /* return; */
            @* return; *@
            _carregando = false;
            """;
        Assert.False(RetornaLogoApos(SemComentarios(soComentario), "Voltar()"));
        Assert.True(RetornaLogoApos(SemComentarios("Voltar();\nreturn;"), "Voltar()"));
    }

    [Fact]
    public void Menus_UmFocoNoAtivadorDoMudMenu_EMutacaoReabreODuplo()
    {
        var painel = Fonte("OrcaFacil", "Components", "Pages", "Lancamentos", "FaturaCartaoPainel.razor");
        var css = Fonte("OrcaFacil", "Components", "Pages", "Lancamentos", "FaturaCartaoPainel.razor.css");
        var linha = Fonte("OrcaFacil", "Components", "Shared", "FormRow.razor");
        var indice = Fonte("OrcaFacil", "wwwroot", "index.html");
        var tema = Fonte("OrcaFacil", "wwwroot", "css", "orcafacil-theme.css");

        Assert.True(UmSoFoco(painel));
        Assert.True(LinhaEmbutidaSemGrupo(linha));
        Assert.True(ToqueELargura(css));
        Assert.True(CaixaDoAtivador(tema));
        Assert.Contains("--of-touch: 44px;", tema, StringComparison.Ordinal);
        Assert.True(Razao("#1E2A33", "#ffffff") >= 4.5);
        Assert.True(Razao("#1E2A33", "#EFF3F7") >= 4.5);
        Assert.True(Razao("#E8EAF0", "#1B1F27") >= 4.5);
        Assert.True(Razao("#E8EAF0", "#12151B") >= 4.5);

        var comBotao = painel.Replace(
            "<div class=\"of-seletor-toque\"",
            "<button type=\"button\" class=\"of-seletor-toque\" tabindex=\"0\"",
            StringComparison.Ordinal);
        Assert.False(UmSoFoco(comBotao));

        var semToque = painel.Replace("ToggleAsync", "Abrir", StringComparison.Ordinal);
        Assert.False(UmSoFoco(semToque));

        var catchVazio = painel + "\ncatch (JSException) { }";
        Assert.False(UmSoFoco(catchVazio));

        var scriptDeVolta = painel + "\nOrcaFacilMenu.umFoco";
        Assert.False(UmSoFoco(scriptDeVolta));

        const string soComentario = """
            <ActivatorContent>
            <!-- <button type="button" tabindex="0">Cartão</button> -->
            @* <button type="button">Recorrência</button> *@
            // ToggleAsync
            </ActivatorContent>
            <ActivatorContent>
            /* role="group" aria-label="Selecionar cartão" */
            </ActivatorContent>
            """;
        Assert.False(UmSoFoco(SemComentarios(soComentario)));

        var comGrupo = linha.Replace(
            "aria-label=\"@RotuloDaLinha\"",
            "role=\"@(Chevron ? \"group\" : null)\" aria-label=\"@Aria\"",
            StringComparison.Ordinal);
        Assert.False(LinhaEmbutidaSemGrupo(comGrupo));

        var rotuloSempre = linha.Replace("Embutido ? null : Aria", "Aria", StringComparison.Ordinal);
        Assert.False(LinhaEmbutidaSemGrupo(rotuloSempre));

        const string grupoComentado = """
            <div class="of-form-row" aria-label="@RotuloDaLinha">
            @* role="group" aria-label="@Aria" *@
            private string? RotuloDaLinha => Embutido ? null : Aria;
            """;
        Assert.True(LinhaEmbutidaSemGrupo(SemComentarios(grupoComentado)));
        Assert.False(LinhaEmbutidaSemGrupo(SemComentarios("private string? RotuloDaLinha => Aria;")));

        var semAltura = css.Replace("min-height: var(--of-touch);", "min-height: 0;", StringComparison.Ordinal);
        Assert.False(ToqueELargura(semAltura));

        var focoNovo = tema.Replace(":focus {", ":focus-visible {", StringComparison.Ordinal);
        Assert.False(CaixaDoAtivador(focoNovo));

        var anelFraco = tema.Replace(
            "outline: 2px solid var(--of-text);",
            "outline: 2px solid var(--of-primary);",
            StringComparison.Ordinal);
        Assert.False(CaixaDoAtivador(anelFraco));
        Assert.True(Razao("#659ECD", "#ffffff") < 4.5);

        var semCaixa = tema.Replace(".mud-menu > div.mud-menu-activator", ".mud-menu-activator", StringComparison.Ordinal);
        Assert.False(CaixaDoAtivador(semCaixa));
    }

    [Fact]
    public void Cesta_AtivadorTemCaixaToqueEAnel_ForaDoCssIsolado()
    {
        var tema = Fonte("OrcaFacil", "wwwroot", "css", "orcafacil-theme.css");
        var dialogo = Fonte("OrcaFacil", "Components", "Pages", "Cesta", "CompraCestaDialog.razor");
        var painelCss = Fonte("OrcaFacil", "Components", "Pages", "Lancamentos", "FaturaCartaoPainel.razor.css");

        Assert.False(CaixaDoAtivador(painelCss));
        Assert.True(CestaCoberta(dialogo, tema));

        var semClasse = dialogo.Replace("class=\"of-seletor\"", "class=\"of-linha\"", StringComparison.Ordinal);
        Assert.False(CestaCoberta(semClasse, tema));

        var semAltura = tema.Replace(
            ".of-seletor .mud-menu > div.mud-menu-activator {\n    display: block;\n    width: 100%;\n    max-width: 100%;\n    min-width: 0;\n    min-height: var(--of-touch);",
            ".of-seletor .mud-menu > div.mud-menu-activator {\n    display: block;\n    width: 100%;\n    max-width: 100%;\n    min-width: 0;\n    min-height: 0;",
            StringComparison.Ordinal);
        Assert.False(CestaCoberta(dialogo, semAltura));

        const string soNoPainel = """
            .of-seletor ::deep .mud-menu > div.mud-menu-activator {
                display: block;
                min-height: var(--of-touch);
            }
            .of-seletor ::deep .mud-menu > div.mud-menu-activator:focus {
                outline: 2px solid var(--of-text);
                outline-offset: -2px;
            }
            """;
        Assert.False(CestaCoberta(dialogo, SemComentarios(soNoPainel)));
    }

    [Fact]
    public void Fechamento_DevolveFocoAoAtivador_EComentarioNaoConta()
    {
        var script = Fonte("OrcaFacil", "wwwroot", "js", "menuFoco.js");
        var indice = Fonte("OrcaFacil", "wwwroot", "index.html");
        var painel = Fonte("OrcaFacil", "Components", "Pages", "Lancamentos", "FaturaCartaoPainel.razor");

        Assert.True(DevolveFoco(script, indice));
        Assert.DoesNotContain("menuFoco", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("InvokeVoidAsync", painel, StringComparison.Ordinal);
        Assert.True(UmSoFoco(painel));

        var semFocus = script.Replace("alvo.focus();", "", StringComparison.Ordinal);
        Assert.False(DevolveFoco(semFocus, indice));

        var focoComentado = SemComentarios(script.Replace(
            "alvo.focus();",
            "// alvo.focus();",
            StringComparison.Ordinal));
        Assert.False(DevolveFoco(focoComentado, indice));

        var semFechar = script.Replace("antes && !agora", "!agora", StringComparison.Ordinal);
        Assert.False(DevolveFoco(semFechar, indice));

        var semAria = script.Replace("aria-expanded", "aria-hidden", StringComparison.Ordinal);
        Assert.False(DevolveFoco(semAria, indice));

        var tagComentada = SemComentarios(indice.Replace(
            "<script src=\"js/menuFoco.js\"></script>",
            "<!-- <script src=\"js/menuFoco.js\"></script> -->",
            StringComparison.Ordinal));
        Assert.False(DevolveFoco(script, tagComentada));

        const string soComentario = """
            // if (antes && !agora) alvo.focus();
            /* mud-menu-activator aria-expanded MutationObserver */
            @* <script src="js/menuFoco.js"></script> *@
            """;
        Assert.False(DevolveFoco(SemComentarios(soComentario), SemComentarios(soComentario)));
    }

    [Fact]
    public void CliqueForaEmCampo_NaoRoubaOFoco()
    {
        var script = Fonte("OrcaFacil", "wwwroot", "js", "menuFoco.js");
        Assert.True(NaoRoubaCampo(script));

        var sempre = script.Replace(
            """
                    if (dentroDoMenu(ativo, alvo))
                        return true;
                    return false;
            """,
            """
                    if (dentroDoMenu(ativo, alvo))
                        return true;
                    return true;
            """,
            StringComparison.Ordinal);
        Assert.False(NaoRoubaCampo(sempre));

        var guardaComentada = SemComentarios(script.Replace(
            "return false;",
            "// return false;",
            StringComparison.Ordinal));
        Assert.False(NaoRoubaCampo(guardaComentada));
    }

    [Fact]
    public void TabEShiftTabComMenuAberto_PreservamOAvanco()
    {
        var script = Fonte("OrcaFacil", "wwwroot", "js", "menuFoco.js");
        Assert.True(TabPreservaAvanco(script));

        var imediato = script.Replace(
            """
                    setTimeout(function () {
                        if (alvo.getAttribute("aria-expanded") === "true")
                            return;
                        guardarPopover(alvo);
                        if (deveDevolver(document.activeElement, alvo)) {
                            if ((Date.now() - tabQuando) < 500)
                                moverParaFora(alvo, tabDirecao);
                            else
                                alvo.focus();
                        }
                        tabQuando = 0;
                    }, 0);
            """,
            """
                    if (deveDevolver(document.activeElement, alvo))
                        alvo.focus();
            """,
            StringComparison.Ordinal);
        Assert.False(TabPreservaAvanco(imediato));

        var incondicional = script.Replace(
            """
                        if (deveDevolver(document.activeElement, alvo)) {
                            if ((Date.now() - tabQuando) < 500)
                                moverParaFora(alvo, tabDirecao);
                            else
                                alvo.focus();
                        }
            """,
            "            alvo.focus();",
            StringComparison.Ordinal);
        Assert.False(TabPreservaAvanco(incondicional));
        Assert.False(NaoRoubaCampo(incondicional));

        var tabRecenteNoAtivador = script.Replace(
            "moverParaFora(alvo, tabDirecao);",
            "alvo.focus();",
            StringComparison.Ordinal);
        Assert.False(TabPreservaAvanco(tabRecenteNoAtivador));

        var teclaComentada = SemComentarios(script.Replace(
            "document.addEventListener(\"keydown\", aoTeclar, true);",
            "// document.addEventListener(\"keydown\", aoTeclar, true);",
            StringComparison.Ordinal));
        Assert.False(TabPreservaAvanco(teclaComentada));

        var esperaComentada = SemComentarios(script.Replace(
            "setTimeout(function () {",
            "// setTimeout(function () {",
            StringComparison.Ordinal));
        Assert.False(TabPreservaAvanco(esperaComentada));
    }

    [Fact]
    public void CalendarioDoDatePicker_NaoPuxaOFocoParaOAtivador()
    {
        var script = Fonte("OrcaFacil", "wwwroot", "js", "menuFoco.js");
        var painel = Fonte("OrcaFacil", "Components", "Pages", "Lancamentos", "FaturaCartaoPainel.razor");
        var dialogo = Fonte("OrcaFacil", "Components", "Pages", "Cesta", "CompraCestaDialog.razor");

        Assert.True(NaoPuxaOutroPopover(script, painel, dialogo));
        Assert.True(DevolveFoco(script, Fonte("OrcaFacil", "wwwroot", "index.html")));
        Assert.True(NaoRoubaCampo(script));
        Assert.True(TabPreservaAvanco(script));

        var qualquerPopover = script.Replace(
            """
                    if (popover && (ativo === popover || popover.contains(ativo)))
                        return true;
            """,
            """
                    if (ativo.closest(".mud-popover") || ativo.closest(".mud-menu-list") || ativo.closest(".mud-overlay"))
                        return true;
            """,
            StringComparison.Ordinal);
        Assert.False(NaoPuxaOutroPopover(qualquerPopover, painel, dialogo));

        var guardaComentada = SemComentarios(script.Replace(
            "popover.contains(ativo)",
            "/* popover.contains(ativo) */",
            StringComparison.Ordinal));
        Assert.False(NaoPuxaOutroPopover(guardaComentada, painel, dialogo));

        var semAbertura = SemComentarios(script.Replace(
            """
                    if (!antes && agora)
                        guardarPopover(alvo);
            """,
            """
                    // if (!antes && agora)
                    //     guardarPopover(alvo);
            """,
            StringComparison.Ordinal));
        Assert.False(NaoPuxaOutroPopover(semAbertura, painel, dialogo));

        var semReleitura = SemComentarios(script.Replace(
            """
                        if (alvo.getAttribute("aria-expanded") === "true")
                            return;
            """,
            """
                        // if (alvo.getAttribute("aria-expanded") === "true")
                        //     return;
            """,
            StringComparison.Ordinal));
        Assert.False(NaoPuxaOutroPopover(semReleitura, painel, dialogo));
        Assert.False(DevolveFoco(semReleitura, Fonte("OrcaFacil", "wwwroot", "index.html")));
    }

    [Fact]
    public void ParaChoqueESentinela_NaoEntramNaOrdemDeTab()
    {
        var script = Fonte("OrcaFacil", "wwwroot", "js", "menuFoco.js");
        Assert.True(RejeitaParaChoque(script));

        var aceitaClasse = SemComentarios(script.Replace(
            """
                    if (no.classList && no.classList.contains("fixed") && no.classList.contains("pointer-events-none"))
                        return false;
            """,
            """
                    // if (no.classList && no.classList.contains("fixed") && no.classList.contains("pointer-events-none"))
                    //     return false;
            """,
            StringComparison.Ordinal));
        Assert.False(RejeitaParaChoque(aceitaClasse));

        var aceitaEstilo = SemComentarios(script.Replace(
            """
                    if (estilo.position === "fixed" && estilo.pointerEvents === "none")
                        return false;
            """,
            """
                    // if (estilo.position === "fixed" && estilo.pointerEvents === "none")
                    //     return false;
            """,
            StringComparison.Ordinal));
        Assert.False(RejeitaParaChoque(aceitaEstilo));

        var aceitaZero = SemComentarios(script.Replace(
            """
                    if (no.offsetWidth === 0 || no.offsetHeight === 0)
                        return false;
            """,
            """
                    // if (no.offsetWidth === 0 || no.offsetHeight === 0)
                    //     return false;
            """,
            StringComparison.Ordinal));
        Assert.False(RejeitaParaChoque(aceitaZero));
    }

    [Fact]
    public void ShiftTabNoDialogo_VoltaParaOUltimoControle()
    {
        var script = Fonte("OrcaFacil", "wwwroot", "js", "menuFoco.js");
        var dialogo = Fonte("OrcaFacil", "Components", "Pages", "Cesta", "CompraCestaDialog.razor");
        Assert.True(VoltaDentroDoDialogo(script, dialogo));

        var semDialogo = script.Replace(
            "dialogo = alvo.closest(\"[role='dialog']\");",
            "dialogo = null;",
            StringComparison.Ordinal);
        Assert.False(VoltaDentroDoDialogo(semDialogo, dialogo));

        var semVolta = SemComentarios(script.Replace(
            """
                    if (!escolhido && dialogo)
                        escolhido = voltaNoDialogo(ordem, passo, popover);
            """,
            """
                    // if (!escolhido && dialogo)
                    //     escolhido = voltaNoDialogo(ordem, passo, popover);
            """,
            StringComparison.Ordinal));
        Assert.False(VoltaDentroDoDialogo(semVolta, dialogo));

        var puxaPopover = script.Replace(
            "if (!estaNoPopover(ordem[j], popover))",
            "if (true)",
            StringComparison.Ordinal);
        Assert.False(VoltaDentroDoDialogo(puxaPopover, dialogo));
    }

    [Fact]
    public void RelogioDoTab_NaoArmaNoCalendario()
    {
        var script = Fonte("OrcaFacil", "wwwroot", "js", "menuFoco.js");
        var painel = Fonte("OrcaFacil", "Components", "Pages", "Lancamentos", "FaturaCartaoPainel.razor");
        var dialogo = Fonte("OrcaFacil", "Components", "Pages", "Cesta", "CompraCestaDialog.razor");
        Assert.True(RelogioSoNoPopoverDoMenu(script, painel, dialogo));

        var qualquerPopover = script.Replace(
            "if (!origem || !origemDoMenu(origem))",
            "if (!origem || !origem.closest(\"[id^='popovercontent-']\"))",
            StringComparison.Ordinal);
        Assert.False(RelogioSoNoPopoverDoMenu(qualquerPopover, painel, dialogo));

        var guardaComentada = SemComentarios(script.Replace(
            """
                    if (!origem || !origemDoMenu(origem))
                        return;
            """,
            """
                    // if (!origem || !origemDoMenu(origem))
                    //     return;
            """,
            StringComparison.Ordinal));
        Assert.False(RelogioSoNoPopoverDoMenu(guardaComentada, painel, dialogo));

        var semLista = SemComentarios(script.Replace(
            "popoversDoMenu.add(popover);",
            "// popoversDoMenu.add(popover);",
            StringComparison.Ordinal));
        Assert.False(RelogioSoNoPopoverDoMenu(semLista, painel, dialogo));

        var listaForte = script.Replace("new WeakSet()", "[]", StringComparison.Ordinal)
            .Replace("popoversDoMenu.add(popover);", "popoversDoMenu.push(popover);", StringComparison.Ordinal);
        Assert.False(RelogioSoNoPopoverDoMenu(listaForte, painel, dialogo));
    }

    [Fact]
    public void TabNoMenu_ImpedeOFocoNativo()
    {
        var script = Fonte("OrcaFacil", "wwwroot", "js", "menuFoco.js");
        Assert.True(ImpedeTabNativo(script));

        var semPrevent = script.Replace("evento.preventDefault();", "", StringComparison.Ordinal);
        Assert.False(ImpedeTabNativo(semPrevent));

        var preventComentado = SemComentarios(script.Replace(
            "evento.preventDefault();",
            "// evento.preventDefault();",
            StringComparison.Ordinal));
        Assert.False(ImpedeTabNativo(preventComentado));
    }

    private static string Fonte(params string[] partes)
        => SemComentarios(File.ReadAllText(Path.Combine(new[] { EncontrarRaiz() }.Concat(partes).ToArray())));

    private static string RamoTransacaoNula(string formulario)
    {
        var sessao = ExtrairBloco(formulario, "Task AbrirSessaoAsync()");
        var obter = sessao.IndexOf("ObterAsync", StringComparison.Ordinal);
        var consumir = sessao.IndexOf("Consumir", StringComparison.Ordinal);
        var entre = sessao[obter..consumir];
        var marca = entre.IndexOf("transacao is not null", StringComparison.Ordinal);
        var abreSucesso = entre.IndexOf('{', marca);
        var sucesso = ExtrairChaves(entre, abreSucesso);
        var depois = entre[(abreSucesso + sucesso.Length)..];
        return ExtrairChaves(depois, depois.IndexOf('{', depois.IndexOf("else", StringComparison.Ordinal)));
    }

    private static bool RetornaLogoApos(string bloco, string chamada)
    {
        var indice = bloco.IndexOf(chamada, StringComparison.Ordinal);
        if (indice < 0)
            return false;

        var resto = bloco[(indice + chamada.Length)..].TrimStart();
        if (resto.StartsWith(';'))
            resto = resto[1..].TrimStart();
        if (!resto.StartsWith("return", StringComparison.Ordinal))
            return false;

        var depois = resto["return".Length..];
        if (depois.Length > 0 && (char.IsLetterOrDigit(depois[0]) || depois[0] == '_'))
            return false;

        return depois.TrimStart().StartsWith(';');
    }

    private static bool UmSoFoco(string painel)
    {
        var ativadores = Trechos(painel, "<ActivatorContent", "</ActivatorContent>");
        if (ativadores.Count != 2)
            return false;

        foreach (var ativador in ativadores)
        {
            if (Contar(ativador, "<button") != 0)
                return false;
            if (Contar(ativador, "ToggleAsync") != 1)
                return false;
            if (ativador.Contains("tabindex", StringComparison.OrdinalIgnoreCase))
                return false;
            if (ativador.Contains("role=\"group\"", StringComparison.Ordinal)
                || ativador.Contains("role=\"button\"", StringComparison.Ordinal)
                || ativador.Contains("<MudButton", StringComparison.Ordinal)
                || ativador.Contains("<MudIconButton", StringComparison.Ordinal))
                return false;
            if (!ativador.Contains("Embutido=\"true\"", StringComparison.Ordinal))
                return false;
            if (ativador.Contains("AriaLabel=", StringComparison.Ordinal))
                return false;
        }

        return !painel.Contains("OrcaFacilMenu", StringComparison.Ordinal)
            && !painel.Contains("menuFoco", StringComparison.Ordinal)
            && !painel.Contains("catch (JSException)", StringComparison.Ordinal)
            && !painel.Contains("InvokeVoidAsync", StringComparison.Ordinal);
    }

    private static bool DevolveFoco(string script, string indice)
    {
        if (!indice.Contains("<script src=\"js/menuFoco.js\"></script>", StringComparison.Ordinal))
            return false;
        if (script.Contains("?.", StringComparison.Ordinal)
            || script.Contains("??", StringComparison.Ordinal)
            || script.Contains("replaceAll", StringComparison.Ordinal)
            || script.Contains("tabIndex", StringComparison.Ordinal)
            || script.Contains("removeAttribute(\"role\")", StringComparison.Ordinal)
            || script.Contains("focus-visible", StringComparison.Ordinal))
            return false;
        if (!script.Contains("mud-menu-activator", StringComparison.Ordinal))
            return false;
        if (!script.Contains("new WeakMap()", StringComparison.Ordinal))
            return false;
        if (!script.Contains("new MutationObserver", StringComparison.Ordinal))
            return false;
        if (!script.Contains("attributeFilter: [\"aria-expanded\"]", StringComparison.Ordinal))
            return false;
        if (!script.Contains("subtree: true", StringComparison.Ordinal))
            return false;
        if (!script.Contains("if (antes && !agora)", StringComparison.Ordinal))
            return false;
        if (Contar(script, "alvo.focus()") != 1)
            return false;

        return EsperaDevolucao(script);
    }

    private static bool NaoPuxaOutroPopover(string script, string painel, string dialogo)
    {
        if (!painel.Contains("<MudDatePicker", StringComparison.Ordinal))
            return false;
        if (!dialogo.Contains("<MudDatePicker", StringComparison.Ordinal))
            return false;
        if (script.Contains("?.", StringComparison.Ordinal)
            || script.Contains("??", StringComparison.Ordinal)
            || script.Contains("replaceAll", StringComparison.Ordinal)
            || script.Contains(".mud-popover", StringComparison.Ordinal)
            || script.Contains(".mud-overlay", StringComparison.Ordinal)
            || script.Contains(".mud-menu-list", StringComparison.Ordinal))
            return false;
        if (!script.Contains("aria-controls", StringComparison.Ordinal))
            return false;
        if (!script.Contains("popovercontent-", StringComparison.Ordinal))
            return false;
        if (!script.Contains("popoverDoMenu", StringComparison.Ordinal))
            return false;
        if (!script.Contains("if (!antes && agora)", StringComparison.Ordinal))
            return false;
        if (!script.Contains("guardarPopover(alvo);", StringComparison.Ordinal))
            return false;
        if (!script.Contains("ativo === alvo", StringComparison.Ordinal))
            return false;
        if (!script.Contains("popover.contains(ativo)", StringComparison.Ordinal))
            return false;
        if (Contar(script, "alvo.focus()") != 1)
            return false;

        return EsperaDevolucao(script);
    }

    private static bool EsperaDevolucao(string script)
        => Regex.IsMatch(
            script,
            @"setTimeout\(function \(\) \{\s*if \(alvo\.getAttribute\(""aria-expanded""\) === ""true""\)\s*return;\s*guardarPopover\(alvo\);\s*if \(deveDevolver\(document\.activeElement, alvo\)\) \{\s*if \(\(Date\.now\(\) - tabQuando\) < 500\)\s*moverParaFora\(alvo, tabDirecao\);\s*else\s*alvo\.focus\(\);\s*\}\s*tabQuando = 0;\s*\}, 0\);",
            RegexOptions.Singleline);

    private static bool NaoRoubaCampo(string script)
    {
        if (Contar(script, "alvo.focus()") != 1)
            return false;
        if (!script.Contains("ativo === document.body", StringComparison.Ordinal))
            return false;
        if (!script.Contains("!ativo", StringComparison.Ordinal))
            return false;
        if (!Regex.IsMatch(
                script,
                @"if \(deveDevolver\(document\.activeElement, alvo\)\) \{\s*if \(\(Date\.now\(\) - tabQuando\) < 500\)\s*moverParaFora\(alvo, tabDirecao\);\s*else\s*alvo\.focus\(\);",
                RegexOptions.Singleline))
            return false;

        return Regex.IsMatch(
            script,
            @"if\s*\(\s*dentroDoMenu\(ativo,\s*alvo\)\s*\)\s*return\s+true\s*;\s*return\s+false\s*;");
    }

    private static bool TabPreservaAvanco(string script)
    {
        if (Contar(script, "alvo.focus()") != 1)
            return false;
        if (script.Contains("?.", StringComparison.Ordinal) || script.Contains("??", StringComparison.Ordinal))
            return false;
        if (Regex.IsMatch(script, @"if\s*\(\s*antes\s*&&\s*!agora\s*\)\s*alvo\.focus"))
            return false;
        if (!script.Contains("document.addEventListener(\"keydown\", aoTeclar, true);", StringComparison.Ordinal))
            return false;
        if (!script.Contains("evento.shiftKey", StringComparison.Ordinal))
            return false;
        if (!script.Contains("popovercontent-", StringComparison.Ordinal))
            return false;
        if (!script.Contains("moverParaFora(alvo, tabDirecao);", StringComparison.Ordinal))
            return false;
        if (Regex.IsMatch(script, @"< 500\)\s*alvo\.focus\(\)"))
            return false;
        if (!Regex.IsMatch(script, @"< 500\)\s*moverParaFora\(alvo,\s*tabDirecao\);"))
            return false;

        return EsperaDevolucao(script);
    }

    private static bool RejeitaParaChoque(string script)
    {
        if (script.Contains("?.", StringComparison.Ordinal) || script.Contains("??", StringComparison.Ordinal))
            return false;

        return Regex.IsMatch(
            script,
            @"function podeTabular\(no\) \{.*?classList\.contains\(""fixed""\) && no\.classList\.contains\(""pointer-events-none""\)\)\s*return false;.*?position === ""fixed"" && estilo\.pointerEvents === ""none""\)\s*return false;.*?offsetWidth === 0 \|\| no\.offsetHeight === 0\)\s*return false;\s*return true;\s*\}",
            RegexOptions.Singleline);
    }

    private static bool VoltaDentroDoDialogo(string script, string dialogo)
    {
        if (!dialogo.Contains("<MudDialog", StringComparison.Ordinal))
            return false;
        if (script.Contains("?.", StringComparison.Ordinal) || script.Contains("??", StringComparison.Ordinal))
            return false;
        if (!script.Contains("dialogo = alvo.closest(\"[role='dialog']\");", StringComparison.Ordinal))
            return false;
        if (!script.Contains("var ordem = tabulaveis(dialogo);", StringComparison.Ordinal))
            return false;
        if (!Regex.IsMatch(script, @"if \(raiz && raiz\.querySelectorAll\)\s*base = raiz;"))
            return false;
        if (!Regex.IsMatch(
                script,
                @"if \(!escolhido && dialogo\)\s*escolhido = voltaNoDialogo\(ordem, passo, popover\);"))
            return false;
        if (!Regex.IsMatch(
                script,
                @"function voltaNoDialogo\(ordem, passo, popover\) \{\s*var inicio = passo < 0 \? ordem\.length - 1 : 0;\s*return seguinteNaOrdem\(ordem, inicio, passo, popover\);\s*\}"))
            return false;
        if (!Regex.IsMatch(
                script,
                @"if \(!estaNoPopover\(ordem\[j\], popover\)\)\s*return ordem\[j\];"))
            return false;

        return script.Contains("popover.contains(candidato)", StringComparison.Ordinal);
    }

    private static bool RelogioSoNoPopoverDoMenu(string script, string painel, string dialogo)
    {
        if (!painel.Contains("<MudDatePicker", StringComparison.Ordinal))
            return false;
        if (!dialogo.Contains("<MudDatePicker", StringComparison.Ordinal))
            return false;
        if (script.Contains("?.", StringComparison.Ordinal) || script.Contains("??", StringComparison.Ordinal))
            return false;
        if (script.Contains("closest(\"[id^='popovercontent-']\")", StringComparison.Ordinal))
            return false;
        if (!script.Contains("new WeakSet()", StringComparison.Ordinal))
            return false;
        if (script.Contains("popoversDoMenu = []", StringComparison.Ordinal)
            || script.Contains("popoversDoMenu.push(", StringComparison.Ordinal))
            return false;
        if (!script.Contains("popoversDoMenu.add(popover);", StringComparison.Ordinal))
            return false;
        if (!script.Contains("popoversDoMenu.has(no)", StringComparison.Ordinal))
            return false;
        if (!script.Contains("if (!antes && agora)", StringComparison.Ordinal))
            return false;

        return Regex.IsMatch(
            script,
            @"if \(!origem \|\| !origemDoMenu\(origem\)\)\s*return;\s*tabDirecao = evento\.shiftKey \? -1 : 1;\s*tabQuando = Date\.now\(\);",
            RegexOptions.Singleline);
    }

    private static bool ImpedeTabNativo(string script)
    {
        if (script.Contains("?.", StringComparison.Ordinal) || script.Contains("??", StringComparison.Ordinal))
            return false;
        if (Contar(script, "evento.preventDefault()") != 1)
            return false;

        return Regex.IsMatch(
            script,
            @"if \(!origem \|\| !origemDoMenu\(origem\)\)\s*return;\s*tabDirecao = evento\.shiftKey \? -1 : 1;\s*tabQuando = Date\.now\(\);\s*evento\.preventDefault\(\);",
            RegexOptions.Singleline);
    }

    private static bool LinhaEmbutidaSemGrupo(string linha)
    {
        var div = linha.IndexOf("<div class=\"of-form-row\"", StringComparison.Ordinal);
        if (div < 0)
            return false;

        var fecha = linha.IndexOf('>', div);
        if (fecha < 0)
            return false;

        var abertura = linha[div..(fecha + 1)];
        if (abertura.Contains("role", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!abertura.Contains("aria-label=\"@RotuloDaLinha\"", StringComparison.Ordinal))
            return false;

        return linha.Contains("Embutido ? null : Aria", StringComparison.Ordinal)
            && !linha.Contains("role=\"group\"", StringComparison.Ordinal)
            && !linha.Contains("\"group\"", StringComparison.Ordinal);
    }

    private static bool ToqueELargura(string css)
    {
        var toque = Regra(css, ".of-seletor-toque");
        if (toque.Length == 0)
            return false;
        if (toque.Contains("min-height: 0", StringComparison.Ordinal))
            return false;
        if (!toque.Contains("min-height: var(--of-touch);", StringComparison.Ordinal))
            return false;
        if (!toque.Contains("width: 100%;", StringComparison.Ordinal)
            || !toque.Contains("max-width: 100%;", StringComparison.Ordinal)
            || !toque.Contains("min-width: 0;", StringComparison.Ordinal))
            return false;

        return !Regex.IsMatch(toque, @"min-width:\s*[1-9]");
    }

    private static bool CaixaDoAtivador(string css)
    {
        const string seletor = ".of-seletor .mud-menu > div.mud-menu-activator";
        if (css.IndexOf(seletor, StringComparison.Ordinal) < 0)
            return false;

        var caixa = Regra(css, seletor);
        var focoIndice = css.IndexOf(seletor + ":focus", StringComparison.Ordinal);
        if (focoIndice < 0)
            return false;
        if (css.AsSpan(focoIndice + (seletor + ":focus").Length).StartsWith("-visible"))
            return false;

        var foco = Regra(css, seletor + ":focus");
        if (caixa.Length == 0 || foco.Length == 0)
            return false;
        if (!caixa.Contains("display: block;", StringComparison.Ordinal))
            return false;
        if (caixa.Contains("display: contents", StringComparison.Ordinal))
            return false;
        if (!caixa.Contains("min-height: var(--of-touch);", StringComparison.Ordinal))
            return false;
        if (caixa.Contains("min-height: 0", StringComparison.Ordinal))
            return false;
        if (!caixa.Contains("width: 100%;", StringComparison.Ordinal)
            || !caixa.Contains("max-width: 100%;", StringComparison.Ordinal)
            || !caixa.Contains("min-width: 0;", StringComparison.Ordinal))
            return false;
        if (Regex.IsMatch(caixa, @"min-width:\s*[1-9]"))
            return false;

        return foco.Contains("outline: 2px solid var(--of-text);", StringComparison.Ordinal)
            && foco.Contains("outline-offset: -2px;", StringComparison.Ordinal);
    }

    private static bool CestaCoberta(string dialogo, string tema)
        => dialogo.Contains("class=\"of-seletor\"", StringComparison.Ordinal) && CaixaDoAtivador(tema);

    private static string Regra(string css, string seletor)
    {
        var indice = css.IndexOf(seletor, StringComparison.Ordinal);
        if (indice < 0)
            return "";

        var abre = css.IndexOf('{', indice);
        if (abre < 0)
            return "";

        var fecha = css.IndexOf('}', abre);
        return fecha < 0 ? "" : css[abre..(fecha + 1)];
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

    private static string SemComentarios(string fonte)
    {
        var texto = new StringBuilder(fonte.Length);
        for (var i = 0; i < fonte.Length; i++)
        {
            var c = fonte[i];
            if (c == '"')
            {
                texto.Append(c);
                for (i++; i < fonte.Length; i++)
                {
                    texto.Append(fonte[i]);
                    if (fonte[i] == '\\' && i + 1 < fonte.Length)
                    {
                        texto.Append(fonte[++i]);
                        continue;
                    }

                    if (fonte[i] == '"')
                        break;
                }

                continue;
            }

            if (c == '@' && i + 1 < fonte.Length && fonte[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < fonte.Length && !(fonte[i] == '*' && fonte[i + 1] == '@'))
                    i++;
                if (i + 1 < fonte.Length)
                    i++;
                continue;
            }

            if (c == '<' && ComecaCom(fonte, i, "<!--"))
            {
                i += 4;
                while (i + 2 < fonte.Length && !ComecaCom(fonte, i, "-->"))
                    i++;
                if (i + 2 < fonte.Length)
                    i += 2;
                continue;
            }

            if (c == '/' && i + 1 < fonte.Length && fonte[i + 1] == '/')
            {
                while (i < fonte.Length && fonte[i] != '\n')
                    i++;
                if (i < fonte.Length)
                    texto.Append('\n');
                continue;
            }

            if (c == '/' && i + 1 < fonte.Length && fonte[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < fonte.Length && !(fonte[i] == '*' && fonte[i + 1] == '/'))
                    i++;
                if (i + 1 < fonte.Length)
                    i++;
                continue;
            }

            texto.Append(c);
        }

        return texto.ToString();
    }

    private static bool ComecaCom(string fonte, int indice, string trecho)
    {
        if (indice + trecho.Length > fonte.Length)
            return false;

        for (var i = 0; i < trecho.Length; i++)
        {
            if (fonte[indice + i] != trecho[i])
                return false;
        }

        return true;
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
