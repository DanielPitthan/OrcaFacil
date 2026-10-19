using System.Globalization;
using System.IO.Compression;
using System.Text;
using ClosedXML.Excel;
using OrcaFacil.Core.Planilha;

namespace OrcaFacil.Tests.Planilha;

public class PlanilhaPtBrTests
{
    [Fact]
    public void Data_ReabreComoDateTimeComFormatoBrasileiro()
    {
        var entrada = new DateTime(2026, 10, 19, 15, 4, 5, DateTimeKind.Utc);

        Reabrir(pasta =>
        {
            var aba = PlanilhaPtBr.CriarAba(pasta, "Lançamentos");
            PlanilhaPtBr.GravarData(aba.Cell(2, 1), entrada);
            PlanilhaPtBr.GravarData(aba.Cell(3, 1), (DateTime?)null);
        }, pasta =>
        {
            var aba = pasta.Worksheet("Lançamentos");
            var data = aba.Cell(2, 1);
            Assert.Equal(XLDataType.DateTime, data.DataType);
            Assert.False(data.HasFormula);
            Assert.Equal(new DateTime(2026, 10, 19), data.GetDateTime());
            Assert.Equal(PlanilhaPtBr.FormatoData, data.Style.DateFormat.Format);

            Assert.Equal(XLDataType.Blank, aba.Cell(3, 1).DataType);
        });
    }

    [Fact]
    public void DataHora_ReabreSemSegundosNoFormatoBrasileiro()
    {
        var entrada = new DateTime(2026, 10, 19, 15, 4, 59, DateTimeKind.Local);

        Reabrir(pasta =>
        {
            var aba = PlanilhaPtBr.CriarAba(pasta, "Perfis");
            PlanilhaPtBr.GravarDataHora(aba.Cell(2, 1), entrada);
        }, pasta =>
        {
            var celula = pasta.Worksheet("Perfis").Cell(2, 1);
            Assert.Equal(XLDataType.DateTime, celula.DataType);
            Assert.False(celula.HasFormula);
            Assert.Equal(new DateTime(2026, 10, 19, 15, 4, 0), celula.GetDateTime());
            Assert.Equal(PlanilhaPtBr.FormatoDataHora, celula.Style.DateFormat.Format);
        });
    }

    [Theory]
    [InlineData("1234.56")]
    [InlineData("0")]
    [InlineData("-40.5")]
    public void Moeda_ReabreComoNumeroComFormatoReal(string texto)
    {
        var valor = decimal.Parse(texto, CultureInfo.InvariantCulture);
        Reabrir(pasta =>
        {
            var aba = PlanilhaPtBr.CriarAba(pasta, "Lançamentos");
            PlanilhaPtBr.GravarMoeda(aba.Cell(2, 1), valor);
            PlanilhaPtBr.GravarNumero(aba.Cell(2, 2), valor);
            PlanilhaPtBr.GravarMoeda(aba.Cell(3, 1), null);
        }, pasta =>
        {
            var aba = pasta.Worksheet("Lançamentos");
            var moeda = aba.Cell(2, 1);
            Assert.Equal(XLDataType.Number, moeda.DataType);
            Assert.False(moeda.HasFormula);
            Assert.Equal((double)valor, moeda.GetDouble(), precision: 5);
            Assert.Equal(PlanilhaPtBr.FormatoMoeda, moeda.Style.NumberFormat.Format);
            Assert.False(moeda.Style.IncludeQuotePrefix);

            var numero = aba.Cell(2, 2);
            Assert.Equal(XLDataType.Number, numero.DataType);
            Assert.Equal((double)valor, numero.GetDouble(), precision: 5);
            Assert.NotEqual(PlanilhaPtBr.FormatoMoeda, numero.Style.NumberFormat.Format);

            Assert.Equal(XLDataType.Blank, aba.Cell(3, 1).DataType);
        });
    }

    [Theory]
    [InlineData(true, PlanilhaPtBr.Sim)]
    [InlineData(false, PlanilhaPtBr.Nao)]
    public void Booleano_ReabreComoTextoSimOuNao(bool valor, string esperado)
    {
        Reabrir(pasta =>
        {
            var aba = PlanilhaPtBr.CriarAba(pasta, "Perfis");
            PlanilhaPtBr.GravarSimNao(aba.Cell(2, 1), valor);
        }, pasta =>
        {
            var celula = pasta.Worksheet("Perfis").Cell(2, 1);
            Assert.Equal(XLDataType.Text, celula.DataType);
            Assert.NotEqual(XLDataType.Boolean, celula.DataType);
            Assert.False(celula.HasFormula);
            Assert.Equal(esperado, celula.GetText());
            Assert.False(celula.Style.IncludeQuotePrefix);
        });
    }

    [Theory]
    [InlineData("=1+1")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("@soma")]
    [InlineData("\t1")]
    [InlineData(" =1+1")]
    public void TextoQuePareceFormula_ReabreComoTextoInerte(string texto)
        => ConferirTextoInerte(texto, texto);

    [Fact]
    public void TextoComRetornoDeCarro_ReabreComoTextoInerte()
    {
        // O ClosedXML grava CR, LF e CRLF como a quebra de linha da plataforma.
        ConferirTextoInerte("\r1", Environment.NewLine + "1");
        ConferirTextoInerte("\n=1", Environment.NewLine + "=1");
        ConferirTextoInerte("\r\n=1", Environment.NewLine + "=1");
    }

    private void ConferirTextoInerte(string texto, string esperado)
    {
        Reabrir(pasta =>
        {
            var aba = PlanilhaPtBr.CriarAba(pasta, "Lançamentos");
            PlanilhaPtBr.GravarCabecalho(aba, ["Descrição", "Vizinha"]);
            PlanilhaPtBr.GravarTexto(aba.Cell(2, 1), texto);
            PlanilhaPtBr.GravarTexto(aba.Cell(2, 2), "Mercado");
        }, pasta =>
        {
            var aba = pasta.Worksheet("Lançamentos");
            var celula = aba.Cell(2, 1);
            Assert.Equal(XLDataType.Text, celula.DataType);
            Assert.False(celula.HasFormula);
            Assert.Equal(esperado, celula.GetText());
            Assert.True(celula.Style.IncludeQuotePrefix);
            Assert.NotEqual(2d, ValorSeNumero(celula));

            var vizinha = aba.Cell(2, 2);
            Assert.Equal("Mercado", vizinha.GetText());
            Assert.False(vizinha.Style.IncludeQuotePrefix);
            Assert.False(vizinha.HasFormula);
        });
    }

    [Fact]
    public void TextoComumEUltimosDigitos_ReabremComoTexto()
    {
        Reabrir(pasta =>
        {
            var aba = PlanilhaPtBr.CriarAba(pasta, "Cartões");
            PlanilhaPtBr.GravarTexto(aba.Cell(2, 1), "Açúcar");
            PlanilhaPtBr.GravarTexto(aba.Cell(2, 2), "0012");
            PlanilhaPtBr.GravarTexto(aba.Cell(2, 3), "'cita");
            PlanilhaPtBr.GravarTexto(aba.Cell(2, 4), null);
        }, pasta =>
        {
            var aba = pasta.Worksheet("Cartões");
            Assert.Equal(XLDataType.Text, aba.Cell(2, 1).DataType);
            Assert.Equal("Açúcar", aba.Cell(2, 1).GetText());
            Assert.False(aba.Cell(2, 1).Style.IncludeQuotePrefix);

            Assert.Equal(XLDataType.Text, aba.Cell(2, 2).DataType);
            Assert.Equal("0012", aba.Cell(2, 2).GetText());

            Assert.Equal("'cita", aba.Cell(2, 3).GetText());
            Assert.Equal(XLDataType.Blank, aba.Cell(2, 4).DataType);
        });
    }

    [Fact]
    public void NomeDeAba_Com31CaracteresPassaECom32Falha()
    {
        var nomeCurto = new string('a', PlanilhaPtBr.MaximoCaracteresAba);
        var nomeLongo = new string('b', PlanilhaPtBr.MaximoCaracteresAba + 1);

        Reabrir(pasta =>
        {
            var aba = PlanilhaPtBr.CriarAba(pasta, nomeCurto);
            PlanilhaPtBr.GravarTexto(aba.Cell(1, 1), "ok");
            var erro = Assert.Throws<ArgumentException>(() => PlanilhaPtBr.CriarAba(pasta, nomeLongo));
            Assert.Contains("31", erro.Message, StringComparison.Ordinal);
            Assert.Equal("nome", erro.ParamName);
        }, pasta =>
        {
            var aba = Assert.Single(pasta.Worksheets);
            Assert.Equal(nomeCurto, aba.Name);
            Assert.Equal("ok", aba.Cell(1, 1).GetText());
        });
    }

    [Theory]
    [InlineData("A:B")]
    [InlineData("A\\B")]
    [InlineData("A/B")]
    [InlineData("A?B")]
    [InlineData("A*B")]
    [InlineData("A[B")]
    [InlineData("A]B")]
    [InlineData("'aba")]
    [InlineData("aba'")]
    [InlineData(" aba")]
    [InlineData("aba ")]
    [InlineData(" ")]
    public void NomeDeAba_InvalidoFalha(string nome)
    {
        using var pasta = new XLWorkbook();
        Assert.Throws<ArgumentException>(() => PlanilhaPtBr.CriarAba(pasta, nome));
        Assert.Empty(pasta.Worksheets);
    }

    [Fact]
    public void CelulaLonga_ECortadaEm32767()
    {
        var longo = new string('x', PlanilhaPtBr.MaximoCaracteresCelula + 5_000);
        var formula = "=" + new string('y', PlanilhaPtBr.MaximoCaracteresCelula + 20);
        var emojis = string.Concat(Enumerable.Repeat("😀", 20_000));
        var citacao = "'" + new string('z', PlanilhaPtBr.MaximoCaracteresCelula + 10);

        Reabrir(pasta =>
        {
            var aba = PlanilhaPtBr.CriarAba(pasta, "Observações");
            PlanilhaPtBr.GravarTexto(aba.Cell(1, 1), longo);
            PlanilhaPtBr.GravarTexto(aba.Cell(2, 1), formula);
            PlanilhaPtBr.GravarTexto(aba.Cell(3, 1), emojis);
            PlanilhaPtBr.GravarTexto(aba.Cell(4, 1), citacao);
        }, pasta =>
        {
            var aba = pasta.Worksheet("Observações");
            var texto = aba.Cell(1, 1).GetText();
            Assert.Equal(PlanilhaPtBr.MaximoCaracteresCelula, texto.Length);
            Assert.Equal(new string('x', PlanilhaPtBr.MaximoCaracteresCelula), texto);
            Assert.False(aba.Cell(1, 1).Style.IncludeQuotePrefix);

            var perigoso = aba.Cell(2, 1);
            Assert.Equal(XLDataType.Text, perigoso.DataType);
            Assert.False(perigoso.HasFormula);
            Assert.True(perigoso.Style.IncludeQuotePrefix);
            Assert.StartsWith("=", perigoso.GetText(), StringComparison.Ordinal);
            Assert.Equal(PlanilhaPtBr.MaximoCaracteresCelula, perigoso.GetText().Length);

            var emoji = aba.Cell(3, 1).GetText();
            Assert.True(emoji.Length <= PlanilhaPtBr.MaximoCaracteresCelula);
            Assert.False(char.IsHighSurrogate(emoji[^1]));
            Assert.StartsWith("😀", emoji, StringComparison.Ordinal);

            var aspas = aba.Cell(4, 1).GetText();
            Assert.StartsWith("'z", aspas, StringComparison.Ordinal);
            Assert.Equal(PlanilhaPtBr.MaximoCaracteresCelula - 1, aspas.Length);
            Assert.Equal(XLDataType.Text, aba.Cell(4, 1).DataType);
        });
    }

    [Fact]
    public void Cabecalho_FixaLarguraNegritoENaoMedeConteudo()
    {
        Reabrir(pasta =>
        {
            var aba = PlanilhaPtBr.CriarAba(pasta, "Categorias");
            PlanilhaPtBr.GravarCabecalho(aba, ["=Total", "Nome"]);
        }, pasta =>
        {
            var aba = pasta.Worksheet("Categorias");
            Assert.Equal(PlanilhaPtBr.LarguraColuna, aba.Column(1).Width);
            Assert.Equal(PlanilhaPtBr.LarguraColuna, aba.Column(2).Width);
            Assert.True(aba.Cell(1, 1).Style.Font.Bold);
            Assert.True(aba.Cell(1, 2).Style.Font.Bold);
            Assert.Equal("=Total", aba.Cell(1, 1).GetText());
            Assert.True(aba.Cell(1, 1).Style.IncludeQuotePrefix);
            Assert.False(aba.Cell(1, 1).HasFormula);
            Assert.Equal(1, aba.SheetView.SplitRow);
        });

        var fonte = File.ReadAllText(Path.Combine(EncontrarRaiz(), "OrcaFacil.Core", "Planilha", "PlanilhaPtBr.cs"));
        Assert.DoesNotContain("AdjustToContents", fonte, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("10.50")]
    [InlineData("1234.56")]
    [InlineData("999999.99")]
    [InlineData("-40.25")]
    public void MoedaRealista_ReabreComOCentavoExato(string texto)
    {
        var valor = decimal.Parse(texto, CultureInfo.InvariantCulture);

        Reabrir(pasta =>
        {
            var aba = PlanilhaPtBr.CriarAba(pasta, "Lançamentos");
            PlanilhaPtBr.GravarMoeda(aba.Cell(2, 1), valor);
        }, pasta =>
        {
            var celula = pasta.Worksheet("Lançamentos").Cell(2, 1);
            Assert.Equal(XLDataType.Number, celula.DataType);
            Assert.Equal(valor, (decimal)celula.GetDouble());
            Assert.Equal(PlanilhaPtBr.FormatoMoeda, celula.Style.NumberFormat.Format);
        });
    }

    [Fact]
    public void MoedaAcimaDoDouble_EntraComoTextoExato()
    {
        var valor = 1234567890123456m;

        Reabrir(pasta =>
        {
            var aba = PlanilhaPtBr.CriarAba(pasta, "Lançamentos");
            PlanilhaPtBr.GravarMoeda(aba.Cell(2, 1), valor);
        }, pasta =>
        {
            var celula = pasta.Worksheet("Lançamentos").Cell(2, 1);
            Assert.Equal(XLDataType.Text, celula.DataType);
            Assert.Contains("1234567890123456", celula.GetText(), StringComparison.Ordinal);
            Assert.DoesNotContain("1234567890123460", celula.GetText(), StringComparison.Ordinal);
            Assert.StartsWith("R$ ", celula.GetText(), StringComparison.Ordinal);
        });
    }

    [Fact]
    public void DecimalNoTeto_ViraTextoExatoSemExcecao()
    {
        const string digitos = "79228162514264337593543950335";

        Reabrir(pasta =>
        {
            var aba = PlanilhaPtBr.CriarAba(pasta, "Operações");
            PlanilhaPtBr.GravarNumero(aba.Cell(2, 1), decimal.MaxValue);
            PlanilhaPtBr.GravarNumero(aba.Cell(3, 1), decimal.MinValue);
            PlanilhaPtBr.GravarMoeda(aba.Cell(4, 1), decimal.MaxValue);
            PlanilhaPtBr.GravarMoeda(aba.Cell(5, 1), decimal.MinValue);
        }, pasta =>
        {
            var aba = pasta.Worksheet("Operações");
            Assert.Equal(digitos, aba.Cell(2, 1).GetText());
            Assert.Equal(XLDataType.Text, aba.Cell(2, 1).DataType);
            Assert.Equal("-" + digitos, aba.Cell(3, 1).GetText());
            Assert.True(aba.Cell(3, 1).Style.IncludeQuotePrefix);
            Assert.Equal("R$ " + digitos, aba.Cell(4, 1).GetText());
            Assert.Equal("R$ -" + digitos, aba.Cell(5, 1).GetText());
            Assert.Equal(XLDataType.Text, aba.Cell(5, 1).DataType);
        });
    }

    [Fact]
    public void SurrogateSolto_ViraSubstituicaoENaoLanca()
    {
        ConferirSurrogate(new string(['\uD800', 'f', 'o', 't', 'o']), "\uFFFDfoto");
        ConferirSurrogate(new string(['\uDE00', 'f', 'o', 't', 'o']), "\uFFFDfoto");
        ConferirSurrogate(new string(['f', 'o', '\uD800', 't', 'o']), "fo\uFFFDto");
        ConferirSurrogate(new string(['f', 'o', '\uDE00', 't', 'o']), "fo\uFFFDto");
        ConferirSurrogate(new string(['f', 'o', 't', 'o', '\uD800']), "foto\uFFFD");
        ConferirSurrogate(new string(['f', 'o', 't', 'o', '\uDE00']), "foto\uFFFD");
        ConferirSurrogate(new string(['\uD800']) + "😀", "\uFFFD😀");
        ConferirSurrogate("😀" + new string(['\uDE00']), "😀\uFFFD");
    }

    [Fact]
    public void SubstituicaoNoInicio_NeutralizaFormulaEEmojiFica()
    {
        ConferirTextoInerte(new string(['\uD800', '=', '1', '+', '1']), "\uFFFD=1+1");
        ConferirTextoInerte("\uFFFD=1+1", "\uFFFD=1+1");

        Reabrir(pasta =>
        {
            var aba = PlanilhaPtBr.CriarAba(pasta, "Perfis");
            PlanilhaPtBr.GravarTexto(aba.Cell(2, 1), "foto😀");
            PlanilhaPtBr.GravarTexto(aba.Cell(3, 1), "😀=1+1");
        }, pasta =>
        {
            var aba = pasta.Worksheet("Perfis");
            Assert.Equal("foto😀", aba.Cell(2, 1).GetText());
            Assert.False(aba.Cell(2, 1).Style.IncludeQuotePrefix);
            Assert.Equal("😀=1+1", aba.Cell(3, 1).GetText());
            Assert.False(aba.Cell(3, 1).Style.IncludeQuotePrefix);
            Assert.False(aba.Cell(3, 1).HasFormula);
        });
    }

    private void ConferirSurrogate(string texto, string esperado)
    {
        Reabrir(pasta =>
        {
            var aba = PlanilhaPtBr.CriarAba(pasta, "Perfis");
            PlanilhaPtBr.GravarTexto(aba.Cell(2, 1), texto);
        }, pasta =>
        {
            var celula = pasta.Worksheet("Perfis").Cell(2, 1);
            Assert.Equal(XLDataType.Text, celula.DataType);
            Assert.Equal(esperado, celula.GetText());
        });
    }

    [Fact]
    public void DataUtcNoHorarioDeVerao_ViraUtcMenosDuas()
    {
        var utc = new DateTime(2018, 11, 15, 3, 30, 45, DateTimeKind.Utc);

        Reabrir(pasta =>
        {
            var aba = PlanilhaPtBr.CriarAba(pasta, "Perfis");
            PlanilhaPtBr.GravarDataHora(aba.Cell(2, 1), utc);
            PlanilhaPtBr.GravarData(aba.Cell(3, 1), utc);
        }, pasta =>
        {
            var aba = pasta.Worksheet("Perfis");
            Assert.Equal(new DateTime(2018, 11, 15, 1, 30, 0), aba.Cell(2, 1).GetDateTime());
            Assert.Equal(new DateTime(2018, 11, 15), aba.Cell(3, 1).GetDateTime());
        });
    }

    [Theory]
    [InlineData("\u00A0=1+1")]
    [InlineData("\u3000=1")]
    [InlineData("\uFEFF=1")]
    [InlineData("\u200B=1")]
    [InlineData("\u200C=1")]
    [InlineData("\u200D+1")]
    [InlineData("\u2060-1")]
    [InlineData("\uFF1D1+1")]
    [InlineData("\uFF0B1")]
    [InlineData("\uFF0D1")]
    [InlineData("\uFF20soma")]
    [InlineData("\u22121")]
    public void TextoComBrancoOuFullwidth_ReabreComoTextoInerte(string texto)
        => ConferirTextoInerte(texto, texto);

    [Theory]
    [InlineData("\u0009=1+1")]
    [InlineData("\u000A=1+1")]
    [InlineData("\u000B=1+1")]
    [InlineData("\u000C=1+1")]
    [InlineData("\u000D=1+1")]
    [InlineData("\u0020=1+1")]
    [InlineData("\u0085=1+1")]
    [InlineData("\u00A0=1+1")]
    [InlineData("\u00AD=1+1")]
    [InlineData("\u0600=1+1")]
    [InlineData("\u0601=1+1")]
    [InlineData("\u0602=1+1")]
    [InlineData("\u0603=1+1")]
    [InlineData("\u0604=1+1")]
    [InlineData("\u0605=1+1")]
    [InlineData("\u061C=1+1")]
    [InlineData("\u06DD=1+1")]
    [InlineData("\u070F=1+1")]
    [InlineData("\u0890=1+1")]
    [InlineData("\u0891=1+1")]
    [InlineData("\u08E2=1+1")]
    [InlineData("\u1680=1+1")]
    [InlineData("\u180E=1+1")]
    [InlineData("\u2000=1+1")]
    [InlineData("\u2001=1+1")]
    [InlineData("\u2002=1+1")]
    [InlineData("\u2003=1+1")]
    [InlineData("\u2004=1+1")]
    [InlineData("\u2005=1+1")]
    [InlineData("\u2006=1+1")]
    [InlineData("\u2007=1+1")]
    [InlineData("\u2008=1+1")]
    [InlineData("\u2009=1+1")]
    [InlineData("\u200A=1+1")]
    [InlineData("\u200B=1+1")]
    [InlineData("\u200C=1+1")]
    [InlineData("\u200D=1+1")]
    [InlineData("\u200E=1+1")]
    [InlineData("\u200F=1+1")]
    [InlineData("\u2028=1+1")]
    [InlineData("\u2029=1+1")]
    [InlineData("\u202A=1+1")]
    [InlineData("\u202B=1+1")]
    [InlineData("\u202C=1+1")]
    [InlineData("\u202D=1+1")]
    [InlineData("\u202E=1+1")]
    [InlineData("\u202F=1+1")]
    [InlineData("\u205F=1+1")]
    [InlineData("\u2060=1+1")]
    [InlineData("\u2061=1+1")]
    [InlineData("\u2062=1+1")]
    [InlineData("\u2063=1+1")]
    [InlineData("\u2064=1+1")]
    [InlineData("\u2066=1+1")]
    [InlineData("\u2067=1+1")]
    [InlineData("\u2068=1+1")]
    [InlineData("\u2069=1+1")]
    [InlineData("\u206A=1+1")]
    [InlineData("\u206B=1+1")]
    [InlineData("\u206C=1+1")]
    [InlineData("\u206D=1+1")]
    [InlineData("\u206E=1+1")]
    [InlineData("\u206F=1+1")]
    [InlineData("\u3000=1+1")]
    [InlineData("\uFEFF=1+1")]
    [InlineData("\uFFF9=1+1")]
    [InlineData("\uFFFA=1+1")]
    [InlineData("\uFFFB=1+1")]
    [InlineData("\U000110BD=1+1")]
    [InlineData("\U000110CD=1+1")]
    [InlineData("\U00013430=1+1")]
    [InlineData("\U00013431=1+1")]
    [InlineData("\U00013432=1+1")]
    [InlineData("\U00013433=1+1")]
    [InlineData("\U00013434=1+1")]
    [InlineData("\U00013435=1+1")]
    [InlineData("\U00013436=1+1")]
    [InlineData("\U00013437=1+1")]
    [InlineData("\U00013438=1+1")]
    [InlineData("\U00013439=1+1")]
    [InlineData("\U0001343A=1+1")]
    [InlineData("\U0001343B=1+1")]
    [InlineData("\U0001343C=1+1")]
    [InlineData("\U0001343D=1+1")]
    [InlineData("\U0001343E=1+1")]
    [InlineData("\U0001343F=1+1")]
    [InlineData("\U0001BCA0=1+1")]
    [InlineData("\U0001BCA1=1+1")]
    [InlineData("\U0001BCA2=1+1")]
    [InlineData("\U0001BCA3=1+1")]
    [InlineData("\U0001D173=1+1")]
    [InlineData("\U0001D174=1+1")]
    [InlineData("\U0001D175=1+1")]
    [InlineData("\U0001D176=1+1")]
    [InlineData("\U0001D177=1+1")]
    [InlineData("\U0001D178=1+1")]
    [InlineData("\U0001D179=1+1")]
    [InlineData("\U0001D17A=1+1")]
    [InlineData("\U000E0001=1+1")]
    [InlineData("\U000E0020=1+1")]
    [InlineData("\U000E0021=1+1")]
    [InlineData("\U000E0022=1+1")]
    [InlineData("\U000E0023=1+1")]
    [InlineData("\U000E0024=1+1")]
    [InlineData("\U000E0025=1+1")]
    [InlineData("\U000E0026=1+1")]
    [InlineData("\U000E0027=1+1")]
    [InlineData("\U000E0028=1+1")]
    [InlineData("\U000E0029=1+1")]
    [InlineData("\U000E002A=1+1")]
    [InlineData("\U000E002B=1+1")]
    [InlineData("\U000E002C=1+1")]
    [InlineData("\U000E002D=1+1")]
    [InlineData("\U000E002E=1+1")]
    [InlineData("\U000E002F=1+1")]
    [InlineData("\U000E0030=1+1")]
    [InlineData("\U000E0031=1+1")]
    [InlineData("\U000E0032=1+1")]
    [InlineData("\U000E0033=1+1")]
    [InlineData("\U000E0034=1+1")]
    [InlineData("\U000E0035=1+1")]
    [InlineData("\U000E0036=1+1")]
    [InlineData("\U000E0037=1+1")]
    [InlineData("\U000E0038=1+1")]
    [InlineData("\U000E0039=1+1")]
    [InlineData("\U000E003A=1+1")]
    [InlineData("\U000E003B=1+1")]
    [InlineData("\U000E003C=1+1")]
    [InlineData("\U000E003D=1+1")]
    [InlineData("\U000E003E=1+1")]
    [InlineData("\U000E003F=1+1")]
    [InlineData("\U000E0040=1+1")]
    [InlineData("\U000E0041=1+1")]
    [InlineData("\U000E0042=1+1")]
    [InlineData("\U000E0043=1+1")]
    [InlineData("\U000E0044=1+1")]
    [InlineData("\U000E0045=1+1")]
    [InlineData("\U000E0046=1+1")]
    [InlineData("\U000E0047=1+1")]
    [InlineData("\U000E0048=1+1")]
    [InlineData("\U000E0049=1+1")]
    [InlineData("\U000E004A=1+1")]
    [InlineData("\U000E004B=1+1")]
    [InlineData("\U000E004C=1+1")]
    [InlineData("\U000E004D=1+1")]
    [InlineData("\U000E004E=1+1")]
    [InlineData("\U000E004F=1+1")]
    [InlineData("\U000E0050=1+1")]
    [InlineData("\U000E0051=1+1")]
    [InlineData("\U000E0052=1+1")]
    [InlineData("\U000E0053=1+1")]
    [InlineData("\U000E0054=1+1")]
    [InlineData("\U000E0055=1+1")]
    [InlineData("\U000E0056=1+1")]
    [InlineData("\U000E0057=1+1")]
    [InlineData("\U000E0058=1+1")]
    [InlineData("\U000E0059=1+1")]
    [InlineData("\U000E005A=1+1")]
    [InlineData("\U000E005B=1+1")]
    [InlineData("\U000E005C=1+1")]
    [InlineData("\U000E005D=1+1")]
    [InlineData("\U000E005E=1+1")]
    [InlineData("\U000E005F=1+1")]
    [InlineData("\U000E0060=1+1")]
    [InlineData("\U000E0061=1+1")]
    [InlineData("\U000E0062=1+1")]
    [InlineData("\U000E0063=1+1")]
    [InlineData("\U000E0064=1+1")]
    [InlineData("\U000E0065=1+1")]
    [InlineData("\U000E0066=1+1")]
    [InlineData("\U000E0067=1+1")]
    [InlineData("\U000E0068=1+1")]
    [InlineData("\U000E0069=1+1")]
    [InlineData("\U000E006A=1+1")]
    [InlineData("\U000E006B=1+1")]
    [InlineData("\U000E006C=1+1")]
    [InlineData("\U000E006D=1+1")]
    [InlineData("\U000E006E=1+1")]
    [InlineData("\U000E006F=1+1")]
    [InlineData("\U000E0070=1+1")]
    [InlineData("\U000E0071=1+1")]
    [InlineData("\U000E0072=1+1")]
    [InlineData("\U000E0073=1+1")]
    [InlineData("\U000E0074=1+1")]
    [InlineData("\U000E0075=1+1")]
    [InlineData("\U000E0076=1+1")]
    [InlineData("\U000E0077=1+1")]
    [InlineData("\U000E0078=1+1")]
    [InlineData("\U000E0079=1+1")]
    [InlineData("\U000E007A=1+1")]
    [InlineData("\U000E007B=1+1")]
    [InlineData("\U000E007C=1+1")]
    [InlineData("\U000E007D=1+1")]
    [InlineData("\U000E007E=1+1")]
    [InlineData("\U000E007F=1+1")]
    public void PrefixoInvisivel_NeutralizaFormula(string texto)
        => ConferirTextoInerte(texto, EsperadoAposGravar(texto));

    private static string EsperadoAposGravar(string texto)
    {
        var resultado = new StringBuilder(texto.Length);
        for (var i = 0; i < texto.Length; i++)
        {
            var caractere = texto[i];
            if (caractere < ' ' && caractere is not '\t' and not '\n' and not '\r'
                || caractere is '\uFFFE' or '\uFFFF')
            {
                resultado.Append(' ');
            }
            else if (caractere == '\r' && i + 1 < texto.Length && texto[i + 1] == '\n')
            {
                resultado.Append(Environment.NewLine);
                i++;
            }
            else if (caractere is '\n' or '\r')
            {
                resultado.Append(Environment.NewLine);
            }
            else
            {
                resultado.Append(caractere);
            }
        }

        return resultado.ToString();
    }

    [Fact]
    public void Controle_ViraEspacoENaoViraCodigoDoExcel()
    {
        using var stream = new MemoryStream();
        using (var pasta = new XLWorkbook())
        {
            var aba = PlanilhaPtBr.CriarAba(pasta, "Texto");
            PlanilhaPtBr.GravarTexto(aba.Cell(1, 1), "a\u0001b");
            PlanilhaPtBr.GravarTexto(aba.Cell(2, 1), "\u0001=1");
            pasta.SaveAs(stream);
        }

        stream.Position = 0;
        using (var reaberta = new XLWorkbook(stream))
        {
            var aba = reaberta.Worksheet("Texto");
            Assert.Equal("a b", aba.Cell(1, 1).GetText());
            Assert.Equal(" =1", aba.Cell(2, 1).GetText());
            Assert.Equal(XLDataType.Text, aba.Cell(2, 1).DataType);
            Assert.True(aba.Cell(2, 1).Style.IncludeQuotePrefix);
            Assert.False(aba.Cell(2, 1).HasFormula);
        }

        stream.Position = 0;
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        foreach (var entrada in zip.Entries)
        {
            using var leitor = new StreamReader(entrada.Open(), Encoding.UTF8);
            var xml = leitor.ReadToEnd();
            Assert.DoesNotContain("_x0001_", xml, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DataOnly_ReabreComoDateTime()
    {
        Reabrir(pasta =>
        {
            var aba = PlanilhaPtBr.CriarAba(pasta, "Metas");
            PlanilhaPtBr.GravarData(aba.Cell(2, 1), new DateOnly(2026, 10, 19));
            PlanilhaPtBr.GravarData(aba.Cell(3, 1), DateOnly.MinValue);
            PlanilhaPtBr.GravarData(aba.Cell(4, 1), (DateOnly?)null);
        }, pasta =>
        {
            var aba = pasta.Worksheet("Metas");
            Assert.Equal(XLDataType.DateTime, aba.Cell(2, 1).DataType);
            Assert.Equal(new DateTime(2026, 10, 19), aba.Cell(2, 1).GetDateTime());
            Assert.Equal(PlanilhaPtBr.FormatoData, aba.Cell(2, 1).Style.DateFormat.Format);
            Assert.Equal(XLDataType.Blank, aba.Cell(3, 1).DataType);
            Assert.Equal(XLDataType.Blank, aba.Cell(4, 1).DataType);
        });
    }

    [Fact]
    public void DataUtc_ViraHorarioDeSaoPaulo()
    {
        var utc = new DateTime(2026, 10, 19, 2, 30, 45, DateTimeKind.Utc);

        Reabrir(pasta =>
        {
            var aba = PlanilhaPtBr.CriarAba(pasta, "Perfis");
            PlanilhaPtBr.GravarDataHora(aba.Cell(2, 1), utc);
            PlanilhaPtBr.GravarData(aba.Cell(3, 1), utc);
        }, pasta =>
        {
            var aba = pasta.Worksheet("Perfis");
            Assert.Equal(new DateTime(2026, 10, 18, 23, 30, 0), aba.Cell(2, 1).GetDateTime());
            Assert.Equal(PlanilhaPtBr.FormatoDataHora, aba.Cell(2, 1).Style.DateFormat.Format);
            Assert.Equal(new DateTime(2026, 10, 18), aba.Cell(3, 1).GetDateTime());
        });
    }

    [Fact]
    public void DataMinima_SaiVazia()
    {
        Reabrir(pasta =>
        {
            var aba = PlanilhaPtBr.CriarAba(pasta, "Metas");
            PlanilhaPtBr.GravarData(aba.Cell(2, 1), DateTime.MinValue);
            PlanilhaPtBr.GravarDataHora(aba.Cell(3, 1), DateTime.MinValue);
            PlanilhaPtBr.GravarDataHora(aba.Cell(4, 1), new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        }, pasta =>
        {
            var aba = pasta.Worksheet("Metas");
            Assert.Equal(XLDataType.Blank, aba.Cell(2, 1).DataType);
            Assert.Equal(XLDataType.Blank, aba.Cell(3, 1).DataType);
            Assert.Equal(XLDataType.Blank, aba.Cell(4, 1).DataType);
        });
    }

    [Theory]
    [InlineData("History")]
    [InlineData("history")]
    [InlineData("HISTORY")]
    public void NomeHistory_EReservado(string nome)
    {
        using var pasta = new XLWorkbook();
        var erro = Assert.Throws<ArgumentException>(() => PlanilhaPtBr.CriarAba(pasta, nome));
        Assert.Contains("History", erro.Message, StringComparison.Ordinal);
        Assert.Empty(pasta.Worksheets);
    }

    [Fact]
    public void LinhaAcimaDoTeto_FalhaENaUltimaPassa()
    {
        using var pasta = new XLWorkbook();
        var aba = PlanilhaPtBr.CriarAba(pasta, "Limite");
        Assert.Throws<ArgumentOutOfRangeException>(() => PlanilhaPtBr.Celula(aba, PlanilhaPtBr.MaximoLinhas + 1, 1));

        var ultima = PlanilhaPtBr.Celula(aba, PlanilhaPtBr.MaximoLinhas, 1);
        PlanilhaPtBr.GravarTexto(ultima, "cabe");
        Assert.Equal("cabe", ultima.GetText());
    }

    private static double? ValorSeNumero(IXLCell celula)
    {
        return celula.DataType == XLDataType.Number ? celula.GetDouble() : null;
    }

    private static void Reabrir(Action<XLWorkbook> gravar, Action<XLWorkbook> conferir)
    {
        using var stream = new MemoryStream();
        using (var pasta = new XLWorkbook())
        {
            gravar(pasta);
            pasta.SaveAs(stream);
        }

        stream.Position = 0;
        using var reaberta = new XLWorkbook(stream);
        conferir(reaberta);
    }

    private static string EncontrarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OrcaFacil.Core", "Planilha", "PlanilhaPtBr.cs")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar PlanilhaPtBr.cs a partir de " + AppContext.BaseDirectory);
    }
}
