using System.Text;
using System.Text.RegularExpressions;
using OrcaFacil.Core.Numerico;

namespace OrcaFacil.Tests.Components;

/// <summary>
/// Teclado no celular: inputmode via UserAttributes e InputMode.
/// Fração explícita ou casas acima de zero usam "decimal"; o resto, "numeric".
/// Quantidade e meta pedem TecladoCompleto e abrem "text" (teclado com vírgula).
/// Comentário no fonte não conta. InputType.Number e type=number ficam de fora
/// (WebView Chrome 83; inputmode text existe desde o Chrome 66).
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
            "tecladoCompleto ? \"text\" : tecladoDecimal ? \"decimal\" : \"numeric\"",
            "\"decimal\" /* tecladoCompleto ? \"text\" : tecladoDecimal ? \"decimal\" : \"numeric\" */",
            StringComparison.Ordinal));
        Assert.False(CampoDeriva(semDerivacao));

        var modoFixo = SemComentarios(fonte.Replace(
            "TecladoCompleto ? InputMode.text : TecladoDecimal ? InputMode.@decimal : InputMode.numeric",
            "InputMode.@decimal /* TecladoCompleto ? InputMode.text : TecladoDecimal ? InputMode.@decimal : InputMode.numeric */",
            StringComparison.Ordinal));
        Assert.False(CampoDeriva(modoFixo));

        var semSincronia = SemComentarios(fonte.Replace(
            "SincronizarAtributos();",
            "// SincronizarAtributos();",
            StringComparison.Ordinal));
        Assert.False(SincronizaAntesDeSair(semSincronia));

        const string soComentario = """
            @* tecladoCompleto ? "text" : tecladoDecimal ? "decimal" : "numeric" *@
            // TecladoCompleto ? InputMode.text : TecladoDecimal ? InputMode.@decimal : InputMode.numeric
            /* InputMode="@ModoTeclado" UserAttributes="@_atributos" */
            <!-- ["inputmode"] = TextoModo(tecladoCompleto, tecladoDecimal) -->
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
        Assert.Contains("TecladoCompleto=\"true\"", meta, StringComparison.Ordinal);
        Assert.Contains("ValorMaximo=\"100\"", meta, StringComparison.Ordinal);
        Assert.Contains("AdornmentText=\"%\"", meta, StringComparison.Ordinal);
        Assert.DoesNotContain("CasasDecimais=\"0\"", meta, StringComparison.Ordinal);

        Assert.Contains("CasasDecimais=\"8\"", quantidade, StringComparison.Ordinal);
        Assert.Contains("FracaoExplicita=\"true\"", quantidade, StringComparison.Ordinal);
        Assert.Contains("TecladoCompleto=\"true\"", quantidade, StringComparison.Ordinal);
        Assert.Contains("Class=\"mb-3\"", quantidade, StringComparison.Ordinal);
        Assert.DoesNotContain("CasasDecimais=\"0\"", quantidade, StringComparison.Ordinal);

        Assert.DoesNotContain("FracaoExplicita", Tag(orcamento, "Modelo.ValorLimite"), StringComparison.Ordinal);
        Assert.DoesNotContain("TecladoCompleto", Tag(orcamento, "Modelo.ValorLimite"), StringComparison.Ordinal);
        Assert.Contains("AdornmentText=\"R$\"", Tag(orcamento, "Modelo.ValorLimite"), StringComparison.Ordinal);
        Assert.DoesNotContain("FracaoExplicita", Tag(operacao, "Modelo.PrecoUnitario"), StringComparison.Ordinal);
        Assert.DoesNotContain("TecladoCompleto", Tag(operacao, "Modelo.PrecoUnitario"), StringComparison.Ordinal);
        Assert.Contains("AdornmentText=\"R$\"", Tag(operacao, "Modelo.PrecoUnitario"), StringComparison.Ordinal);
        Assert.DoesNotContain("FracaoExplicita", Tag(operacao, "Modelo.Custos"), StringComparison.Ordinal);
        Assert.DoesNotContain("TecladoCompleto", Tag(operacao, "Modelo.Custos"), StringComparison.Ordinal);
        Assert.Equal(
            [
                "OrcaFacil/Components/Pages/Ativos/OperacaoForm.razor:Modelo.Quantidade",
                "OrcaFacil/Components/Pages/Orcamento/OrcamentoForm.razor:Modelo.MetaReducaoPercentual"
            ],
            CamposComTecladoCompleto().OrderBy(item => item, StringComparer.Ordinal).ToArray());
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

        var quantidadeSemTeclado = SemComentarios(Ler("OrcaFacil", "Components", "Pages", "Ativos", "OperacaoForm.razor").Replace(
            "TecladoCompleto=\"true\"",
            "<!-- TecladoCompleto=\"true\" -->",
            StringComparison.Ordinal));
        Assert.DoesNotContain("TecladoCompleto=\"true\"", Tag(quantidadeSemTeclado, "Modelo.Quantidade"), StringComparison.Ordinal);

        var metaSemTeclado = SemComentarios(Ler("OrcaFacil", "Components", "Pages", "Orcamento", "OrcamentoForm.razor").Replace(
            "TecladoCompleto=\"true\"",
            "@* TecladoCompleto=\"true\" *@",
            StringComparison.Ordinal));
        Assert.DoesNotContain("TecladoCompleto=\"true\"", Tag(metaSemTeclado, "Modelo.MetaReducaoPercentual"), StringComparison.Ordinal);

        var campoSemFracao = SemComentarios(campo.Replace(
            "AvisoTeto.Aplicar(_digitos, texto, Casas, ValorMaximo, substituir: false, FracaoExplicita)",
            "/* AvisoTeto.Aplicar(_digitos, texto, Casas, ValorMaximo, substituir: false, FracaoExplicita) */",
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
    public void MetaExibeAteUmaCasa_PontoEhMilhar_EComentarioNaoConta()
    {
        var form = SemComentarios(Ler("OrcaFacil", "Components", "Pages", "Orcamento", "OrcamentoForm.razor"));
        var mensal = SemComentarios(Ler("OrcaFacil", "Components", "Pages", "Orcamento", "OrcamentoMensal.razor"));
        var helper = SemComentarios(Ler("OrcaFacil.Core", "Numerico", "TextoMetaReducao.cs"));
        var mascara = SemComentarios(Ler("OrcaFacil.Core", "Numerico", "MascaraDireitaEsquerda.cs"));

        Assert.True(ExibeMetaComUmaCasa(form));
        Assert.True(ExibeMetaComUmaCasa(mensal));
        Assert.Contains("TextoMetaReducao.Percentual(Modelo.MetaReducaoPercentual.Value)", form, StringComparison.Ordinal);
        Assert.Contains("TextoMetaReducao.Percentual(item.MetaReducaoPercentual.Value)", mensal, StringComparison.Ordinal);
        Assert.True(FormatoUmaCasa(helper));
        Assert.True(PontoEhMilhar(mascara));
        Assert.True(ReabrirTrunca(mascara));

        Assert.Equal("12,5", TextoMetaReducao.Percentual(12.5m));
        Assert.Equal("12,5", TextoMetaReducao.Percentual(12.59m));
        Assert.Equal("0,5", TextoMetaReducao.Percentual(0.5m));
        Assert.Equal("10", TextoMetaReducao.Percentual(10m));
        Assert.Equal(MascaraDireitaEsquerda.CanonicoFracao(12.59m, 1), TextoMetaReducao.Percentual(12.59m));

        var formComentado = SemComentarios(form.Replace(
            "TextoMetaReducao.Percentual(Modelo.MetaReducaoPercentual.Value)",
            "/* TextoMetaReducao.Percentual(Modelo.MetaReducaoPercentual.Value) */",
            StringComparison.Ordinal));
        Assert.False(ExibeMetaComUmaCasa(formComentado));

        var mensalComentado = SemComentarios(mensal.Replace(
            "TextoMetaReducao.Percentual(item.MetaReducaoPercentual.Value)",
            "<!-- TextoMetaReducao.Percentual(item.MetaReducaoPercentual.Value) -->",
            StringComparison.Ordinal));
        Assert.False(ExibeMetaComUmaCasa(mensalComentado));

        var helperComentado = SemComentarios(helper.Replace(
            "MascaraDireitaEsquerda.CanonicoFracao(valor, 1)",
            "/* MascaraDireitaEsquerda.CanonicoFracao(valor, 1) */ valor.ToString(\"0.#\", CultureInfo.GetCultureInfo(\"pt-BR\"))",
            StringComparison.Ordinal));
        Assert.False(FormatoUmaCasa(helperComentado));

        var pontoNaVirgula = SemComentarios(mascara.Replace(
            "if (caractere == ',')",
            "if (caractere is ',' or '.') /* if (caractere == ',') */",
            StringComparison.Ordinal));
        Assert.False(PontoEhMilhar(pontoNaVirgula));

        var arredonda = SemComentarios(mascara.Replace(
            "decimal.Truncate(Abs(valor) * Potencia10(casas))",
            "/* decimal.Truncate(Abs(valor) * Potencia10(casas)) */ decimal.Round(Abs(valor) * Potencia10(casas), 0, MidpointRounding.AwayFromZero)",
            StringComparison.Ordinal));
        Assert.False(ReabrirTrunca(arredonda));

        IgualDigitadoEColado("1.000", "1000");
        IgualDigitadoEColado("12.500", "12500");
        IgualDigitadoEColado("1.000.000", "1000000");
        IgualDigitadoEColado("1.000,5", "1000,5");
        IgualDigitadoEColado("0,5", "0,5");
        IgualDigitadoEColado("12,5", "12,5");
        IgualDigitadoEColado("12.5", "125");
        IgualDigitadoEColado("0.5", "5");

        Assert.Equal("12,5", MascaraDireitaEsquerda.CanonicoFracao(12.59m, 1));
        Assert.Equal("12,5", MascaraDireitaEsquerda.AplicarFracao("", "12,59", 1, 100m, substituir: true));
        Assert.Equal("12,5", DigitarFracao("12,59", casas: 1, maximo: 100m));
        Assert.Equal(12.5m, MascaraDireitaEsquerda.ValorFracao(MascaraDireitaEsquerda.CanonicoFracao(12.59m, 1)));

        Assert.Equal("10", DigitarFracao("101", casas: 1, maximo: 100m));
        Assert.Equal(10m, MascaraDireitaEsquerda.ValorFracao(DigitarFracao("101", casas: 1, maximo: 100m)));
        Assert.NotEqual(100m, MascaraDireitaEsquerda.ValorFracao(DigitarFracao("101", casas: 1, maximo: 100m)));
        Assert.Equal("", MascaraDireitaEsquerda.AplicarFracao("", "101", 1, 100m, substituir: true));
        Assert.Equal("12,5", MascaraDireitaEsquerda.AplicarFracao("12,5", "101", 1, 100m, substituir: true));
        Assert.Equal("100", DigitarFracao("100", casas: 1, maximo: 100m));
        Assert.Equal("100", DigitarFracao("1000", casas: 1, maximo: 100m));
        Assert.Equal("100,", DigitarFracao("100,1", casas: 1, maximo: 100m));
        Assert.Equal(100m, MascaraDireitaEsquerda.ValorFracao(DigitarFracao("100,1", casas: 1, maximo: 100m)));
    }

    [Fact]
    public void TetoMostraOMaximoReal_MetaLigaFor_EComentarioNaoConta()
    {
        var campo = SemComentarios(Ler("OrcaFacil", "Components", "Shared", "CampoNumerico.razor"));
        var orcamento = SemComentarios(Ler("OrcaFacil", "Components", "Pages", "Orcamento", "OrcamentoForm.razor"));
        var aviso = SemComentarios(Ler("OrcaFacil.Core", "Numerico", "AvisoTeto.cs"));
        var css = SemComentarios(Ler("OrcaFacil", "wwwroot", "css", "orcafacil-theme.css"));

        Assert.True(MostraErroDeTeto(campo));
        Assert.True(MetaLigaOErro(orcamento));
        Assert.True(AvisoUsaAMascara(aviso));
        Assert.True(ContrasteDoErro(css));
        Assert.Equal("O máximo é 100.", AvisoTeto.Mensagem(100m));
        Assert.Equal("O máximo é 1000000000.", AvisoTeto.Mensagem(1_000_000_000m));
        Assert.Equal("O máximo é 100,5.", AvisoTeto.Mensagem(100.5m));

        var semMensagem = SemComentarios(campo.Replace(
            "_erro = AvisoTeto.Mensagem(teto)",
            "/* _erro = AvisoTeto.Mensagem(teto) */ _erro = null",
            StringComparison.Ordinal));
        Assert.False(MostraErroDeTeto(semMensagem));

        var semFor = SemComentarios(orcamento.Replace(
            "For=\"@(() => Modelo.MetaReducaoPercentual)\"",
            "@* For=\"@(() => Modelo.MetaReducaoPercentual)\" *@",
            StringComparison.Ordinal));
        Assert.False(MetaLigaOErro(semFor));

        var semMascara = SemComentarios(aviso.Replace(
            "MascaraDireitaEsquerda.AplicarFracao",
            "/* MascaraDireitaEsquerda.AplicarFracao */",
            StringComparison.Ordinal));
        Assert.False(AvisoUsaAMascara(semMascara));

        var corClara = SemComentarios(css.Replace(
            "--mud-palette-error: var(--of-resultado-erro);",
            "/* --mud-palette-error: var(--of-resultado-erro); */ --mud-palette-error: #D64242;",
            StringComparison.Ordinal));
        Assert.False(ContrasteDoErro(corClara));

        var tokenClaro = SemComentarios(css.Replace(
            "--of-resultado-erro: #B4233A;",
            "/* --of-resultado-erro: #B4233A; */ --of-resultado-erro: #D64242;",
            StringComparison.Ordinal));
        Assert.False(ContrasteDoErro(tokenClaro));
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
        return fonte.Contains("TecladoDecimal => !TecladoCompleto && (FracaoExplicita || Casas > 0)", StringComparison.Ordinal)
            && fonte.Contains("TecladoCompleto ? InputMode.text : TecladoDecimal ? InputMode.@decimal : InputMode.numeric", StringComparison.Ordinal)
            && fonte.Contains("tecladoCompleto ? \"text\" : tecladoDecimal ? \"decimal\" : \"numeric\"", StringComparison.Ordinal)
            && fonte.Contains("[\"inputmode\"] = TextoModo(tecladoCompleto, tecladoDecimal)", StringComparison.Ordinal)
            && fonte.Contains("InputMode=\"@ModoTeclado\"", StringComparison.Ordinal)
            && fonte.Contains("UserAttributes=\"@_atributos\"", StringComparison.Ordinal)
            && fonte.Contains("SincronizarAtributos();", StringComparison.Ordinal)
            && !fonte.Contains("[\"inputmode\"] = \"decimal\"", StringComparison.Ordinal)
            && !fonte.Contains("[\"inputmode\"] = \"numeric\"", StringComparison.Ordinal)
            && !fonte.Contains("[\"inputmode\"] = \"text\"", StringComparison.Ordinal)
            && !fonte.Contains("InputMode.none", StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> CamposComTecladoCompleto()
    {
        var campos = new List<string>();
        foreach (var arquivo in Razors())
        {
            var fonte = SemComentarios(File.ReadAllText(arquivo));
            foreach (var tag in Tags(fonte, "CampoNumerico"))
            {
                if (!tag.Contains("TecladoCompleto", StringComparison.Ordinal))
                    continue;

                if (!tag.Contains("TecladoCompleto=\"true\"", StringComparison.Ordinal))
                    campos.Add(Relativo(arquivo) + ":invalido");
                else
                    campos.Add(Relativo(arquivo) + ":" + Ancora(tag));
            }
        }

        return campos;
    }

    private static bool CampoUsaFracao(string fonte)
        => fonte.Contains("AvisoTeto.Aplicar(_digitos, texto, Casas, ValorMaximo, substituir: false, FracaoExplicita)", StringComparison.Ordinal)
            && fonte.Contains("AvisoTeto.Aplicar(_digitos, texto, Casas, ValorMaximo, substituir: true, FracaoExplicita)", StringComparison.Ordinal)
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

    private static bool ExibeMetaComUmaCasa(string fonte)
        => fonte.Contains("TextoMetaReducao.Percentual(", StringComparison.Ordinal)
            && !fonte.Contains("ToString(\"0\")", StringComparison.Ordinal);

    private static bool FormatoUmaCasa(string fonte)
    {
        var metodo = Metodo(fonte, "string Percentual(");
        return metodo.Contains("MascaraDireitaEsquerda.CanonicoFracao(valor, 1)", StringComparison.Ordinal)
            && !metodo.Contains("ToString(\"0.#\"", StringComparison.Ordinal)
            && !metodo.Contains("ToString(\"0\"", StringComparison.Ordinal)
            && !metodo.Contains("decimal.Round", StringComparison.Ordinal);
    }

    private static bool MostraErroDeTeto(string fonte)
    {
        var aplicar = Metodo(fonte, "Task<string> AplicarAsync(");
        var incluir = Metodo(fonte, "Task<string> IncluirTexto(");
        var definir = Metodo(fonte, "Task<string> DefinirTexto(");
        var atualizar = Metodo(fonte, "void AtualizarErro()");
        return aplicar.Contains("if (inalterado && !excedeuTeto)", StringComparison.Ordinal)
            && aplicar.Contains("_erro = AvisoTeto.Mensagem(teto)", StringComparison.Ordinal)
            && aplicar.Contains("AtualizarErro();", StringComparison.Ordinal)
            && incluir.Contains("excedeuTeto: entrada.PassouDoTeto", StringComparison.Ordinal)
            && definir.Contains("excedeuTeto: entrada.PassouDoTeto", StringComparison.Ordinal)
            && atualizar.Contains("if (_tetoEstourado)", StringComparison.Ordinal)
            && atualizar.Contains("_erro = null", StringComparison.Ordinal)
            && fonte.Contains("ErrorText=\"@_erro\"", StringComparison.Ordinal)
            && fonte.Contains("For is not null", StringComparison.Ordinal);
    }

    private static bool MetaLigaOErro(string fonte)
    {
        var meta = Tag(fonte, "Modelo.MetaReducaoPercentual");
        return meta.Contains("For=\"@(() => Modelo.MetaReducaoPercentual)\"", StringComparison.Ordinal)
            && meta.Contains("Class=\"mb-3\"", StringComparison.Ordinal)
            && meta.Contains("HelperText=\"Opcional, em relação ao gasto do mês anterior\"", StringComparison.Ordinal)
            && meta.Contains("AdornmentText=\"%\"", StringComparison.Ordinal)
            && meta.Contains("ValorMaximo=\"100\"", StringComparison.Ordinal);
    }

    private static bool AvisoUsaAMascara(string fonte)
    {
        var texto = Metodo(fonte, "string Texto(");
        return texto.Contains("MascaraDireitaEsquerda.AplicarFracao", StringComparison.Ordinal)
            && texto.Contains("MascaraDireitaEsquerda.AplicarEntrada", StringComparison.Ordinal)
            && !fonte.Contains("EhMilhar", StringComparison.Ordinal);
    }

    private static bool ContrasteDoErro(string css)
    {
        var erro = BlocoCss(css, "html.of-tema-claro .of-mascara-numerica,\nhtml.of-tema-escuro .of-mascara-numerica");
        var quebra = css.IndexOf(".of-mascara-numerica .mud-input-helper-text {", StringComparison.Ordinal);
        if (erro.Length == 0 || quebra < 0)
            return false;
        if (!erro.Contains("--mud-palette-error: var(--of-resultado-erro)", StringComparison.Ordinal))
            return false;

        var claro = Token(css, ".of-tema-claro", "--of-resultado-erro");
        var escuroTema = Token(css, ".of-tema-escuro", "--of-resultado-erro");
        var escuro = escuroTema.Length > 0 ? escuroTema : Token(css, ":root", "--of-resultado-erro");
        var bloco = css[quebra..css.IndexOf('}', quebra)];
        return claro.Length > 0
            && escuro.Length > 0
            && bloco.Contains("white-space: normal", StringComparison.Ordinal)
            && bloco.Contains("overflow-wrap: anywhere", StringComparison.Ordinal)
            && bloco.Contains("max-width: 100%", StringComparison.Ordinal)
            && Contraste(claro, "#ffffff") >= 4.5
            && Contraste(claro, "#EFF3F7") >= 4.5
            && Contraste(escuro, "#1B1F27") >= 4.5
            && Contraste(escuro, "#12151B") >= 4.5;
    }

    private static string BlocoCss(string css, string seletor)
    {
        var inicio = css.IndexOf(seletor + " {", StringComparison.Ordinal);
        if (inicio < 0)
            return "";

        var abre = css.IndexOf('{', inicio);
        var fecha = abre < 0 ? -1 : css.IndexOf('}', abre);
        return abre < 0 || fecha < 0 ? "" : css[abre..fecha];
    }

    private static string Token(string css, string seletor, string nome)
    {
        var bloco = BlocoCss(css, seletor);
        var marca = nome + ":";
        var cor = bloco.IndexOf(marca, StringComparison.Ordinal);
        if (cor < 0)
            return "";

        var hex = bloco.IndexOf('#', cor);
        if (hex < 0 || hex > cor + marca.Length + 8)
            return "";

        var fim = hex + 1;
        while (fim < bloco.Length && Uri.IsHexDigit(bloco[fim]))
            fim++;

        return bloco[hex..fim];
    }

    private static double Contraste(string frente, string fundo)
    {
        var clara = Math.Max(Luminancia(frente), Luminancia(fundo));
        var escura = Math.Min(Luminancia(frente), Luminancia(fundo));
        return (clara + 0.05) / (escura + 0.05);
    }

    private static double Luminancia(string hex)
    {
        hex = hex.TrimStart('#');
        var r = Canal(hex[..2]);
        var g = Canal(hex.Substring(2, 2));
        var b = Canal(hex.Substring(4, 2));
        return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
    }

    private static double Canal(string hex)
    {
        var valor = Convert.ToInt32(hex, 16) / 255d;
        return valor <= 0.04045 ? valor / 12.92 : Math.Pow((valor + 0.055) / 1.055, 2.4);
    }

    private static bool PontoEhMilhar(string fonte)
    {
        var tecla = Metodo(fonte, "string? AcrescentarFracao(");
        var cola = Metodo(fonte, "string CanonicoDeTexto(");
        return tecla.Contains("if (caractere == ',')", StringComparison.Ordinal)
            && !tecla.Contains("'.'", StringComparison.Ordinal)
            && !fonte.Contains("EhMilhar", StringComparison.Ordinal)
            && cola.Contains("Replace(\".\", \"\", StringComparison.Ordinal)", StringComparison.Ordinal)
            && !cola.Contains("LastIndexOf('.')", StringComparison.Ordinal);
    }

    private static bool ReabrirTrunca(string fonte)
    {
        var metodo = Metodo(fonte, "string CanonicoFracao(");
        return metodo.Contains("decimal.Truncate(Abs(valor) * Potencia10(casas))", StringComparison.Ordinal)
            && !metodo.Contains("decimal.Round", StringComparison.Ordinal);
    }

    private static void IgualDigitadoEColado(string texto, string canonico)
    {
        var digitado = DigitarFracao(texto, casas: 8);
        var colado = MascaraDireitaEsquerda.AplicarFracao("", texto, 8, null, substituir: true);
        Assert.Equal(canonico, digitado);
        Assert.Equal(canonico, colado);
        Assert.Equal(MascaraDireitaEsquerda.ValorFracao(canonico), MascaraDireitaEsquerda.ValorFracao(digitado));
        Assert.Equal(MascaraDireitaEsquerda.ValorFracao(canonico), MascaraDireitaEsquerda.ValorFracao(colado));
    }

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
