using System.Text;
using System.Text.RegularExpressions;
using OrcaFacil.Core.Numerico;

namespace OrcaFacil.Tests.Components;

/// <summary>
/// Teclado numérico no celular: inputmode via UserAttributes.
/// Fração explícita ou casas acima de zero usam "decimal"; o resto, "numeric".
/// Comentário no fonte não conta. InputType.Number e type=number ficam de fora
/// (WebView Chrome 83).
/// </summary>
public class TecladoNumericoFonteTests
{
    [Fact]
    public void CampoNumerico_DerivaOModoDasCasas_EComentarioNaoConta()
    {
        var fonte = Ler("OrcaFacil", "Components", "Shared", "CampoNumerico.razor");
        var limpo = SemComentarios(fonte);

        Assert.True(CampoDeriva(limpo));
        Assert.True(SincronizaAntesDeSair(limpo));
        Assert.False(TemTipoNumber(limpo));

        var semDerivacao = SemComentarios(fonte.Replace(
            "tecladoDecimal ? \"decimal\" : \"numeric\"",
            "\"decimal\" /* tecladoDecimal ? \"decimal\" : \"numeric\" */",
            StringComparison.Ordinal));
        Assert.False(CampoDeriva(semDerivacao));

        var modoFixo = SemComentarios(fonte.Replace(
            "TecladoDecimal ? InputMode.@decimal : InputMode.numeric",
            "InputMode.@decimal /* TecladoDecimal ? InputMode.@decimal : InputMode.numeric */",
            StringComparison.Ordinal));
        Assert.False(CampoDeriva(modoFixo));

        var semSincronia = SemComentarios(fonte.Replace(
            "SincronizarAtributos();",
            "// SincronizarAtributos();",
            StringComparison.Ordinal));
        Assert.False(SincronizaAntesDeSair(semSincronia));

        const string soComentario = """
            @* tecladoDecimal ? "decimal" : "numeric" *@
            // TecladoDecimal ? InputMode.@decimal : InputMode.numeric
            /* InputMode="@ModoTeclado" UserAttributes="@_atributos" */
            <!-- ["inputmode"] = TextoModo(tecladoDecimal) -->
            ["inputmode"] = "decimal"
            """;
        var comentario = SemComentarios(soComentario);
        Assert.False(CampoDeriva(comentario));
        Assert.DoesNotContain("numeric", comentario, StringComparison.Ordinal);
        Assert.Contains("[\"inputmode\"] = \"decimal\"", comentario, StringComparison.Ordinal);
    }

    [Fact]
    public void InteirosExplicitos_AbremTecladoNumerico_EComentarioNaoConta()
    {
        var campos = new (string[] Caminho, string Ancora, string Dicionario)[]
        {
            (["OrcaFacil", "Components", "Pages", "Lancamentos", "LancamentoForm.razor"], "_itemParcelaAtual", "AtributosInteiro"),
            (["OrcaFacil", "Components", "Pages", "Lancamentos", "LancamentoForm.razor"], "_itemTotalParcelas", "AtributosInteiro"),
            (["OrcaFacil", "Components", "Pages", "Configuracoes", "InteligenciaArtificialPanel.razor"], "_maxTokens", "AtributosTokens"),
            (["OrcaFacil", "Components", "Pages", "Configuracoes", "ExportarPlanilhaPanel.razor"], "_anoBase", "AtributosAno"),
            (["OrcaFacil", "Components", "Pages", "Cartoes", "CartaoDialog.razor"], "Modelo.UltimosDigitos", "AtributosDigitos")
        };

        foreach (var (caminho, ancora, dicionario) in campos)
        {
            var fonte = SemComentarios(Ler(caminho));
            Assert.True(CampoInteiro(fonte, ancora, dicionario), ancora);
            Assert.False(TemTipoNumber(Tag(fonte, ancora)), ancora);
        }

        var parcela = Ler("OrcaFacil", "Components", "Pages", "Lancamentos", "LancamentoForm.razor");
        var semAtributo = SemComentarios(parcela.Replace(
            "UserAttributes=\"@AtributosInteiro\"",
            "/* UserAttributes=\"@AtributosInteiro\" */",
            StringComparison.Ordinal));
        Assert.False(CampoInteiro(semAtributo, "_itemParcelaAtual", "AtributosInteiro"));
        Assert.False(CampoInteiro(semAtributo, "_itemTotalParcelas", "AtributosInteiro"));

        var semModo = SemComentarios(parcela.Replace(
            "[\"inputmode\"] = \"numeric\"",
            "// [\"inputmode\"] = \"numeric\"",
            StringComparison.Ordinal));
        Assert.False(CampoInteiro(semModo, "_itemParcelaAtual", "AtributosInteiro"));

        var cartao = Ler("OrcaFacil", "Components", "Pages", "Cartoes", "CartaoDialog.razor");
        var digitosComentados = SemComentarios(cartao.Replace(
            "InputMode=\"InputMode.numeric\"",
            "@* InputMode=\"InputMode.numeric\" *@",
            StringComparison.Ordinal));
        Assert.False(CampoInteiro(digitosComentados, "Modelo.UltimosDigitos", "AtributosDigitos"));

        var tokens = Ler("OrcaFacil", "Components", "Pages", "Configuracoes", "InteligenciaArtificialPanel.razor");
        var tokensComentados = SemComentarios(tokens.Replace(
            "UserAttributes=\"@AtributosTokens\"",
            "<!-- UserAttributes=\"@AtributosTokens\" -->",
            StringComparison.Ordinal));
        Assert.False(CampoInteiro(tokensComentados, "_maxTokens", "AtributosTokens"));
    }

    [Fact]
    public void QuantidadeEMetaAceitamFracaoPtBr_ENenhumaCasaFicaEmZero()
    {
        var orcamento = SemComentarios(Ler("OrcaFacil", "Components", "Pages", "Orcamento", "OrcamentoForm.razor"));
        var operacao = SemComentarios(Ler("OrcaFacil", "Components", "Pages", "Ativos", "OperacaoForm.razor"));
        var campo = SemComentarios(Ler("OrcaFacil", "Components", "Shared", "CampoNumerico.razor"));
        var meta = Tag(orcamento, "Modelo.MetaReducaoPercentual");
        var quantidade = Tag(operacao, "Modelo.Quantidade");

        Assert.Contains("CasasDecimais=\"1\"", meta, StringComparison.Ordinal);
        Assert.Contains("FracaoExplicita=\"true\"", meta, StringComparison.Ordinal);
        Assert.Contains("ValorMaximo=\"100\"", meta, StringComparison.Ordinal);
        Assert.Contains("AdornmentText=\"%\"", meta, StringComparison.Ordinal);
        Assert.DoesNotContain("CasasDecimais=\"0\"", meta, StringComparison.Ordinal);

        Assert.Contains("CasasDecimais=\"8\"", quantidade, StringComparison.Ordinal);
        Assert.Contains("FracaoExplicita=\"true\"", quantidade, StringComparison.Ordinal);
        Assert.Contains("Class=\"mb-3\"", quantidade, StringComparison.Ordinal);
        Assert.DoesNotContain("CasasDecimais=\"0\"", quantidade, StringComparison.Ordinal);

        Assert.DoesNotContain("FracaoExplicita", Tag(orcamento, "Modelo.ValorLimite"), StringComparison.Ordinal);
        Assert.Contains("AdornmentText=\"R$\"", Tag(orcamento, "Modelo.ValorLimite"), StringComparison.Ordinal);
        Assert.DoesNotContain("FracaoExplicita", Tag(operacao, "Modelo.PrecoUnitario"), StringComparison.Ordinal);
        Assert.Contains("AdornmentText=\"R$\"", Tag(operacao, "Modelo.PrecoUnitario"), StringComparison.Ordinal);
        Assert.DoesNotContain("FracaoExplicita", Tag(operacao, "Modelo.Custos"), StringComparison.Ordinal);
        Assert.True(CampoUsaFracao(campo));

        var metaSemFracao = SemComentarios(Ler("OrcaFacil", "Components", "Pages", "Orcamento", "OrcamentoForm.razor").Replace(
            "FracaoExplicita=\"true\"",
            "<!-- FracaoExplicita=\"true\" -->",
            StringComparison.Ordinal));
        Assert.DoesNotContain("FracaoExplicita=\"true\"", Tag(metaSemFracao, "Modelo.MetaReducaoPercentual"), StringComparison.Ordinal);

        var quantidadeSemFracao = SemComentarios(Ler("OrcaFacil", "Components", "Pages", "Ativos", "OperacaoForm.razor").Replace(
            "FracaoExplicita=\"true\"",
            "/* FracaoExplicita=\"true\" */",
            StringComparison.Ordinal));
        Assert.False(Tag(quantidadeSemFracao, "Modelo.Quantidade").Contains("FracaoExplicita=\"true\"", StringComparison.Ordinal));

        var campoSemFracao = SemComentarios(campo.Replace(
            "MascaraDireitaEsquerda.AplicarFracao",
            "/* MascaraDireitaEsquerda.AplicarFracao */",
            StringComparison.Ordinal));
        Assert.False(CampoUsaFracao(campoSemFracao));

        foreach (var arquivo in Razors())
        {
            var fonte = SemComentarios(File.ReadAllText(arquivo));
            foreach (var tag in Tags(fonte, "CampoNumerico"))
                Assert.DoesNotContain("CasasDecimais=\"0\"", tag, StringComparison.Ordinal);
        }

        var meia = DigitarFracao("0,5", casas: 8);
        Assert.Equal("0,5", meia);
        Assert.Equal(0.5m, MascaraDireitaEsquerda.ValorFracao(meia));
        Assert.Equal("0,5", MascaraDireitaEsquerda.ExibirFracao(meia));

        var doisEMeio = DigitarFracao("2,5", casas: 8);
        Assert.Equal(2.5m, MascaraDireitaEsquerda.ValorFracao(doisEMeio));
        Assert.Equal("2,5", MascaraDireitaEsquerda.ExibirFracao(doisEMeio));

        var dozeEMeio = DigitarFracao("12,5", casas: 8);
        Assert.Equal(12.5m, MascaraDireitaEsquerda.ValorFracao(dozeEMeio));
        Assert.Equal("12,5", MascaraDireitaEsquerda.ExibirFracao(dozeEMeio));

        var dozeNaMeta = DigitarFracao("12,5", casas: 1, maximo: 100m);
        Assert.Equal(12.5m, MascaraDireitaEsquerda.ValorFracao(dozeNaMeta));
        Assert.Equal("12,5", MascaraDireitaEsquerda.ExibirFracao(dozeNaMeta));

        var cem = DigitarFracao("100", casas: 1, maximo: 100m);
        Assert.Equal("100", cem);
        Assert.Equal(100m, MascaraDireitaEsquerda.ValorFracao(cem));
        Assert.Equal("100", MascaraDireitaEsquerda.ExibirFracao(cem));

        Assert.Equal("0,5", ReabrirFracao(0.5m, 8));
        Assert.Equal(0.5m, MascaraDireitaEsquerda.ValorFracao(MascaraDireitaEsquerda.CanonicoFracao(0.5m, 8)));
        Assert.Equal("0,06", ReabrirFracao(0.06m, 8));
        Assert.Equal(0.06m, MascaraDireitaEsquerda.ValorFracao(MascaraDireitaEsquerda.CanonicoFracao(0.06m, 8)));
        Assert.Equal("2,5", ReabrirFracao(2.5m, 8));
        Assert.Equal("12,5", ReabrirFracao(12.5m, 1));
        Assert.Equal("100", ReabrirFracao(100m, 1));

        var colado = MascaraDireitaEsquerda.AplicarFracao("", "0,5", 8, null, substituir: true);
        Assert.Equal(0.5m, MascaraDireitaEsquerda.ValorFracao(colado));
        Assert.Equal("0,5", MascaraDireitaEsquerda.ExibirFracao(colado));
        var dozeColado = MascaraDireitaEsquerda.AplicarFracao("", "12,5", 1, 100m, substituir: true);
        Assert.Equal(12.5m, MascaraDireitaEsquerda.ValorFracao(dozeColado));
        var cemColado = MascaraDireitaEsquerda.AplicarFracao("", "100", 1, 100m, substituir: true);
        Assert.Equal(100m, MascaraDireitaEsquerda.ValorFracao(cemColado));
    }

    [Fact]
    public void Varredura_TodoCampoNumericoTemModo_ENaoUsaTypeNumber()
    {
        var numericos = new List<string>();
        var textos = new List<string>();
        foreach (var arquivo in Razors())
        {
            var fonte = SemComentarios(File.ReadAllText(arquivo));
            var relativo = Relativo(arquivo);
            Assert.False(TemTipoNumber(fonte), relativo);
            Assert.DoesNotContain("<input", fonte, StringComparison.OrdinalIgnoreCase);

            foreach (var falta in CamposSemTeclado(fonte))
                Assert.Fail(relativo + " sem inputmode: " + falta);

            foreach (var tag in Tags(fonte, "MudNumericField"))
                numericos.Add(relativo + ":" + Ancora(tag));

            foreach (var tag in Tags(fonte, "MudTextField"))
            {
                if (ModoExigido("MudTextField", tag, fonte) is null)
                    continue;
                textos.Add(relativo + ":" + Ancora(tag));
            }
        }

        Assert.Equal(
            [
                "OrcaFacil/Components/Pages/Configuracoes/ExportarPlanilhaPanel.razor:_anoBase",
                "OrcaFacil/Components/Pages/Configuracoes/InteligenciaArtificialPanel.razor:_maxTokens",
                "OrcaFacil/Components/Pages/Lancamentos/LancamentoForm.razor:_itemParcelaAtual",
                "OrcaFacil/Components/Pages/Lancamentos/LancamentoForm.razor:_itemTotalParcelas"
            ],
            numericos.OrderBy(item => item, StringComparer.Ordinal).ToArray());

        Assert.Equal(
            ["OrcaFacil/Components/Pages/Cartoes/CartaoDialog.razor:Modelo.UltimosDigitos"],
            textos.OrderBy(item => item, StringComparer.Ordinal).ToArray());

        var b3 = SemComentarios(Ler("OrcaFacil", "Components", "Pages", "Ativos", "ImportarExtratoB3.razor"));
        Assert.Empty(Tags(b3, "MudNumericField"));
        Assert.Empty(Tags(b3, "CampoNumerico"));
        Assert.Empty(Tags(b3, "MudTextField"));
    }

    [Fact]
    public void Varredura_RecusaCampoNovoSemInputMode_EComentarioNaoConta()
    {
        const string semModo = """
            @* <MudNumericField T="int" @bind-Value="_comentario" InputMode="InputMode.numeric" UserAttributes="@AtributosComentario" /> *@
            // <MudTextField T="long" @bind-Value="_linha" InputMode="InputMode.numeric" UserAttributes="@AtributosLinha" />
            /* <MudTextField T="string" @bind-Value="_comentado" Label="Dígitos" InputMode="InputMode.numeric" UserAttributes="@AtributosDoc" /> */
            <!-- ["inputmode"] = "numeric" -->
            <MudNumericField T="int" @bind-Value="_novo" Label="Novo" />
            <MudTextField T="decimal" @bind-Value="_valorNovo" Label="Valor novo" />
            <MudTextField T="string" @bind-Value="Modelo.CodigoDigitos" Label="Código" />
            <MudTextField T="string" @bind-Value="_codigo" Label="Código" />
            <MudTextField T="string" @bind-Value="_placa" Placeholder="Somente dígitos" />
            <MudTextField T="string" @bind-Value="_documento" aria-label="Número do documento" />
            <MudTextField T="string" @bind-Value="_serie" Label="Série" />
            <MudTextField T="string" @bind-Value="_nome" Label="Nome" />
            @code {
                if (!_codigo.All(char.IsDigit))
                    return;
                if (!_serie.All(char.IsAsciiDigit))
                    return;
            }
            """;

        var limpo = SemComentarios(semModo);
        var falhas = CamposSemTeclado(limpo);

        Assert.DoesNotContain("_comentario", limpo, StringComparison.Ordinal);
        Assert.DoesNotContain("_comentado", limpo, StringComparison.Ordinal);
        Assert.DoesNotContain("InputMode", limpo, StringComparison.Ordinal);
        Assert.Contains("_novo", falhas);
        Assert.Contains("_valorNovo", falhas);
        Assert.Contains("Modelo.CodigoDigitos", falhas);
        Assert.Contains("_codigo", falhas);
        Assert.Contains("_placa", falhas);
        Assert.Contains("_documento", falhas);
        Assert.Contains("_serie", falhas);
        Assert.DoesNotContain("_nome", falhas);

        const string comModo = """
            <MudNumericField T="int" @bind-Value="_novo" InputMode="InputMode.numeric" UserAttributes="@AtributosNovo" />
            <MudTextField T="decimal" @bind-Value="_valorNovo" InputMode="InputMode.@decimal" UserAttributes="@AtributosValor" />
            <MudTextField T="string" @bind-Value="Modelo.CodigoDigitos" Label="Código"
                          InputMode="InputMode.numeric" UserAttributes="@AtributosCodigo" />
            <MudTextField T="string" @bind-Value="_codigo" Label="Código"
                          InputMode="InputMode.numeric" UserAttributes="@AtributosCodigoLivre" />
            <MudTextField T="string" @bind-Value="_placa" Placeholder="Somente dígitos"
                          InputMode="InputMode.numeric" UserAttributes="@AtributosPlaca" />
            <MudTextField T="string" @bind-Value="_documento" aria-label="Número do documento"
                          InputMode="InputMode.numeric" UserAttributes="@AtributosDocumento" />
            <MudTextField T="string" @bind-Value="_serie" Label="Série"
                          InputMode="InputMode.numeric" UserAttributes="@AtributosSerie" />
            <MudTextField T="string" @bind-Value="_nome" Label="Nome" />
            @code {
                if (!_codigo.All(char.IsDigit))
                    return;
                if (!_serie.All(char.IsAsciiDigit))
                    return;
                private static readonly Dictionary<string, object?> AtributosNovo = new()
                {
                    ["inputmode"] = "numeric"
                };
                private static readonly Dictionary<string, object?> AtributosValor = new()
                {
                    ["inputmode"] = "decimal"
                };
                private static readonly Dictionary<string, object?> AtributosCodigo = new()
                {
                    ["inputmode"] = "numeric"
                };
                private static readonly Dictionary<string, object?> AtributosCodigoLivre = new()
                {
                    ["inputmode"] = "numeric"
                };
                private static readonly Dictionary<string, object?> AtributosPlaca = new()
                {
                    ["inputmode"] = "numeric"
                };
                private static readonly Dictionary<string, object?> AtributosDocumento = new()
                {
                    ["inputmode"] = "numeric"
                };
                private static readonly Dictionary<string, object?> AtributosSerie = new()
                {
                    ["inputmode"] = "numeric"
                };
            }
            """;

        Assert.Empty(CamposSemTeclado(SemComentarios(comModo)));

        const string foraDaPalavra = """
            <MudTextField T="string" @bind-Value="Modelo.CodigoDigitos" Label="Nome" />
            <MudTextField T="string" @bind-Value="_interno" Label="CodigoDigitos" />
            <MudTextField T="string" @bind-Value="_estudo" Label="Numerologia" />
            <MudTextField T="string" @bind-Value="_codificacao" Label="Codificação" />
            <MudTextField T="string" @bind-Value="_limpar" Label="Digite LIMPAR para confirmar" />
            <MudTextField T="string" @bind-Value="_aviso" Label="Nome" RequiredError="Informe os números" />
            <MudTextField T="string" @bind-Value="_nome" Label="Nome" />
            @code {
                var rotulo = _nome;
                if (rotulo.All(char.IsDigit))
                    return;
                if (_limpar.All(char.IsDigitando))
                    return;
                if (_aviso.All(char.MyIsDigit))
                    return;
            }
            """;
        Assert.Empty(CamposSemTeclado(SemComentarios(foraDaPalavra)));

        const string digitoSoNoComentario = """
            <MudTextField T="string" @bind-Value="_codigo" Label="Código" />
            @* if (!_codigo.All(char.IsDigit)) return; *@
            """;
        Assert.Contains("_codigo", CamposSemTeclado(SemComentarios(digitoSoNoComentario)));

        const string rotuloSoNoComentario = """
            @* <MudTextField T="string" @bind-Value="_codigo" Label="Código" /> *@
            // if (_serie.All(char.IsDigit)) return;
            <MudTextField T="string" @bind-Value="_serie" Label="Série" />
            <MudTextField T="string" @bind-Value="_nome" Label="Nome" />
            """;
        var rotuloLimpo = SemComentarios(rotuloSoNoComentario);
        Assert.DoesNotContain("Código", rotuloLimpo, StringComparison.Ordinal);
        Assert.DoesNotContain("IsDigit", rotuloLimpo, StringComparison.Ordinal);
        Assert.Empty(CamposSemTeclado(rotuloLimpo));

        var painel = Ler("OrcaFacil", "Components", "Pages", "Configuracoes", "ExportarPlanilhaPanel.razor");
        var anoComentado = SemComentarios(painel.Replace(
            "InputMode=\"InputMode.numeric\" UserAttributes=\"@AtributosAno\"",
            "/* InputMode=\"InputMode.numeric\" UserAttributes=\"@AtributosAno\" */",
            StringComparison.Ordinal));
        Assert.Contains("_anoBase", CamposSemTeclado(anoComentado));

        var cartao = Ler("OrcaFacil", "Components", "Pages", "Cartoes", "CartaoDialog.razor");
        var digitosComentados = SemComentarios(cartao.Replace(
            "InputMode=\"InputMode.numeric\" UserAttributes=\"@AtributosDigitos\"",
            "<!-- InputMode=\"InputMode.numeric\" UserAttributes=\"@AtributosDigitos\" -->",
            StringComparison.Ordinal));
        Assert.Contains("Modelo.UltimosDigitos", CamposSemTeclado(digitosComentados));
    }

    private static bool CampoDeriva(string fonte)
    {
        return fonte.Contains("TecladoDecimal => FracaoExplicita || Casas > 0", StringComparison.Ordinal)
            && fonte.Contains("TecladoDecimal ? InputMode.@decimal : InputMode.numeric", StringComparison.Ordinal)
            && fonte.Contains("tecladoDecimal ? \"decimal\" : \"numeric\"", StringComparison.Ordinal)
            && fonte.Contains("[\"inputmode\"] = TextoModo(tecladoDecimal)", StringComparison.Ordinal)
            && fonte.Contains("InputMode=\"@ModoTeclado\"", StringComparison.Ordinal)
            && fonte.Contains("UserAttributes=\"@_atributos\"", StringComparison.Ordinal)
            && fonte.Contains("SincronizarAtributos();", StringComparison.Ordinal)
            && !fonte.Contains("[\"inputmode\"] = \"decimal\"", StringComparison.Ordinal)
            && !fonte.Contains("[\"inputmode\"] = \"numeric\"", StringComparison.Ordinal);
    }

    private static bool CampoUsaFracao(string fonte)
        => fonte.Contains("MascaraDireitaEsquerda.AplicarFracao", StringComparison.Ordinal)
            && fonte.Contains("MascaraDireitaEsquerda.CanonicoFracao", StringComparison.Ordinal)
            && fonte.Contains("MascaraDireitaEsquerda.ValorFracao", StringComparison.Ordinal)
            && fonte.Contains("MascaraDireitaEsquerda.ExibirFracao", StringComparison.Ordinal);

    private static string DigitarFracao(string texto, int casas, decimal? maximo = null)
    {
        var digitos = "";
        foreach (var caractere in texto)
            digitos = MascaraDireitaEsquerda.AplicarFracao(digitos, caractere.ToString(), casas, maximo, substituir: false);
        return digitos;
    }

    private static string ReabrirFracao(decimal valor, int casas)
        => MascaraDireitaEsquerda.ExibirFracao(MascaraDireitaEsquerda.CanonicoFracao(valor, casas));

    private static bool SincronizaAntesDeSair(string fonte)
    {
        var metodo = Metodo(fonte, "void OnParametersSet()");
        var sinc = metodo.IndexOf("SincronizarAtributos();", StringComparison.Ordinal);
        var saida = metodo.IndexOf("return;", StringComparison.Ordinal);
        return sinc >= 0 && saida > sinc;
    }

    private static bool CampoInteiro(string fonte, string ancora, string dicionario)
    {
        var tag = Tag(fonte, ancora);
        return tag.Contains("InputMode=\"InputMode.numeric\"", StringComparison.Ordinal)
            && tag.Contains($"UserAttributes=\"@{dicionario}\"", StringComparison.Ordinal)
            && DicionarioNumerico(fonte, dicionario)
            && !TemTipoNumber(tag);
    }

    private static bool DicionarioNumerico(string fonte, string nome)
        => DicionarioModo(fonte, nome, "numeric");

    private static bool DicionarioModo(string fonte, string nome, string modo)
    {
        var inicio = fonte.IndexOf(nome + " = new()", StringComparison.Ordinal);
        if (inicio < 0)
            return false;

        var abre = fonte.IndexOf('{', inicio);
        var fecha = fonte.IndexOf("};", abre, StringComparison.Ordinal);
        if (abre < 0 || fecha < 0)
            return false;

        var corpo = fonte[abre..fecha];
        var outro = modo == "numeric" ? "decimal" : "numeric";
        return corpo.Contains($"[\"inputmode\"] = \"{modo}\"", StringComparison.Ordinal)
            && !corpo.Contains($"[\"inputmode\"] = \"{outro}\"", StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> CamposSemTeclado(string fonte)
    {
        var falhas = new List<string>();
        foreach (var componente in new[] { "MudNumericField", "MudTextField" })
        {
            foreach (var tag in Tags(fonte, componente))
            {
                var modo = ModoExigido(componente, tag, fonte);
                if (modo is null)
                    continue;
                if (!TagTemModo(fonte, tag, modo))
                    falhas.Add(Ancora(tag));
            }
        }

        return falhas;
    }

    private static string? ModoExigido(string componente, string tag, string fonte)
    {
        if (componente == "MudNumericField")
            return TipoFracionario(tag) ? "decimal" : "numeric";

        if (TipoFracionario(tag))
            return "decimal";
        if (TipoInteiro(tag))
            return "numeric";
        if (EhCampoDeDigitos(tag) || ValidadoComoDigito(fonte, tag))
            return "numeric";
        return null;
    }

    private static bool TagTemModo(string fonte, string tag, string modo)
    {
        var nome = NomeUserAttributes(tag);
        if (string.IsNullOrEmpty(nome) || TemTipoNumber(tag))
            return false;

        var inputMode = modo == "decimal"
            ? tag.Contains("InputMode=\"InputMode.@decimal\"", StringComparison.Ordinal)
                || tag.Contains("InputMode=\"InputMode.decimal\"", StringComparison.Ordinal)
            : tag.Contains("InputMode=\"InputMode.numeric\"", StringComparison.Ordinal);
        return inputMode && DicionarioModo(fonte, nome, modo);
    }

    private static readonly Regex PalavraDeDigito = new(
        @"(?<![\p{L}\p{N}_])(d[ií]gitos?|n[uú]meros?|c[oó]digos?)(?![\p{L}\p{N}_])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ChamadaIsDigit = new(
        @"(?<![\p{L}\p{N}_])Is(?:Ascii)?Digit(?![\p{L}\p{N}_])",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static bool EhCampoDeDigitos(string tag)
    {
        foreach (var atributo in new[] { "Label", "Placeholder", "HelperText", "AriaLabel", "aria-label" })
        {
            var valor = ValorAtributo(tag, atributo);
            if (valor is not null && PalavraDeDigito.IsMatch(valor))
                return true;
        }

        return false;
    }

    private static string? ValorAtributo(string tag, string nome)
    {
        var marca = new Regex(
            $@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(nome)}\s*=\s*""",
            RegexOptions.CultureInvariant);
        var encontrado = marca.Match(tag);
        if (!encontrado.Success)
            return null;

        var inicio = encontrado.Index + encontrado.Length;
        var fim = tag.IndexOf('"', inicio);
        return fim < 0 ? null : tag[inicio..fim];
    }

    private static bool ValidadoComoDigito(string fonte, string tag)
    {
        var nome = NomeDoValor(Ancora(tag));
        if (nome.Length < 3)
            return false;

        var identificador = new Regex(
            $@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(nome)}(?![\p{{L}}\p{{N}}_])",
            RegexOptions.CultureInvariant);
        foreach (var enunciado in ForaDasTags(fonte).Split(';'))
        {
            if (identificador.IsMatch(enunciado) && ChamadaIsDigit.IsMatch(enunciado))
                return true;
        }

        return false;
    }

    private static string ForaDasTags(string fonte)
    {
        var texto = new StringBuilder(fonte.Length);
        for (var i = 0; i < fonte.Length; i++)
        {
            if (fonte[i] == '<' && i + 1 < fonte.Length && (char.IsLetter(fonte[i + 1]) || fonte[i + 1] == '/'))
            {
                i = FimDaTag(fonte, i) - 1;
                texto.Append('\n');
                continue;
            }

            texto.Append(fonte[i]);
        }

        return texto.ToString();
    }

    private static string NomeDoValor(string ancora)
    {
        var ponto = ancora.LastIndexOf('.');
        return ponto < 0 ? ancora : ancora[(ponto + 1)..];
    }

    private static bool TemTipoNumber(string fonte)
        => fonte.Contains("InputType.Number", StringComparison.Ordinal)
            || fonte.Contains("type=\"number\"", StringComparison.Ordinal)
            || fonte.Contains("type='number'", StringComparison.Ordinal);

    private static bool TipoInteiro(string tag)
        => Regex.IsMatch(tag, "T=\"(sbyte|byte|short|ushort|int|uint|long|ulong)\\??\"");

    private static bool TipoFracionario(string tag)
        => Regex.IsMatch(tag, "T=\"(float|double|decimal)\\??\"");

    private static string Ancora(string tag)
    {
        const string bind = "@bind-Value=\"";
        var inicio = tag.IndexOf(bind, StringComparison.Ordinal);
        if (inicio < 0)
            return tag;
        inicio += bind.Length;
        var fim = tag.IndexOf('"', inicio);
        return fim < 0 ? tag[inicio..] : tag[inicio..fim];
    }

    private static string? NomeUserAttributes(string tag)
    {
        const string marca = "UserAttributes=\"@";
        var inicio = tag.IndexOf(marca, StringComparison.Ordinal);
        if (inicio < 0)
            return null;
        inicio += marca.Length;
        var fim = tag.IndexOf('"', inicio);
        return fim < 0 ? null : tag[inicio..fim];
    }

    private static string Tag(string fonte, string ancora)
    {
        var indice = fonte.IndexOf(ancora, StringComparison.Ordinal);
        if (indice < 0)
            return "";

        var inicio = fonte.LastIndexOf('<', indice);
        if (inicio < 0)
            return "";

        var fim = FimDaTag(fonte, inicio);
        return fonte[inicio..fim];
    }

    private static IEnumerable<string> Tags(string fonte, string nome)
    {
        var marcador = "<" + nome;
        var i = 0;
        while ((i = fonte.IndexOf(marcador, i, StringComparison.Ordinal)) >= 0)
        {
            var depois = i + marcador.Length;
            if (depois < fonte.Length && (char.IsLetterOrDigit(fonte[depois]) || fonte[depois] == '_'))
            {
                i = depois;
                continue;
            }

            var fim = FimDaTag(fonte, i);
            yield return fonte[i..fim];
            i = fim;
        }
    }

    private static int FimDaTag(string fonte, int inicio)
    {
        for (var i = inicio; i < fonte.Length; i++)
        {
            var c = fonte[i];
            if (c == '"')
            {
                for (i++; i < fonte.Length && fonte[i] != '"'; i++)
                {
                    if (fonte[i] == '\\')
                        i++;
                }

                continue;
            }

            if (c == '>')
                return i + 1;
        }

        return fonte.Length;
    }

    private static string Metodo(string fonte, string assinatura)
    {
        var inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        if (inicio < 0)
            return "";

        var abre = fonte.IndexOf('{', inicio);
        if (abre < 0)
            return "";

        var profundidade = 0;
        for (var i = abre; i < fonte.Length; i++)
        {
            if (fonte[i] == '{')
                profundidade++;
            else if (fonte[i] == '}')
            {
                profundidade--;
                if (profundidade == 0)
                    return fonte[inicio..(i + 1)];
            }
        }

        return "";
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

            if (c == '<' && FonteComecaCom(fonte, i, "<!--"))
            {
                i += 4;
                while (i + 2 < fonte.Length && !FonteComecaCom(fonte, i, "-->"))
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

    private static bool FonteComecaCom(string fonte, int indice, string trecho)
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

    private static IEnumerable<string> Razors()
    {
        var raiz = Raiz();
        return Directory.EnumerateFiles(Path.Combine(raiz, "OrcaFacil"), "*.razor", SearchOption.AllDirectories)
            .Where(caminho => !caminho.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !caminho.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .OrderBy(caminho => caminho, StringComparer.Ordinal);
    }

    private static string Relativo(string caminho)
        => Path.GetRelativePath(Raiz(), caminho).Replace('\\', '/');

    private static string Ler(params string[] partes)
        => File.ReadAllText(Path.Combine(new[] { Raiz() }.Concat(partes).ToArray()));

    private static string Raiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OrcaFacil.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Raiz do repositório não encontrada a partir de " + AppContext.BaseDirectory);
    }
}
