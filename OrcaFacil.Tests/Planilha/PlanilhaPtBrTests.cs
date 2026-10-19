using System.Globalization;
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
            PlanilhaPtBr.GravarData(aba.Cell(3, 1), null);
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
