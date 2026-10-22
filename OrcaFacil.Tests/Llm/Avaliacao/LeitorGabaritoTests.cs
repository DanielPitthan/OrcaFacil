using System.Globalization;
using OrcaFacil.Core.Llm.Avaliacao;

namespace OrcaFacil.Tests.Llm.Avaliacao;

public class LeitorGabaritoTests
{
    [Fact]
    public void JsonValido_LeIdDataTotalEItens()
    {
        const string json = """
            {
              "id": " sint-001 ",
              "data": "2026-10-18",
              "total": 12.90,
              "itens": [
                { "descricao": " Arroz ", "preco": 10.00 },
                { "descricao": "Feijão", "preco": 2.9 }
              ]
            }
            """;

        var gabarito = LeitorGabarito.Ler(json);

        Assert.Equal("sint-001", gabarito.Id);
        Assert.Equal(new DateOnly(2026, 10, 18), gabarito.Data);
        Assert.Equal(12.90m, gabarito.TotalCupom);
        Assert.Equal(2, gabarito.Itens.Count);
        Assert.Equal("Arroz", gabarito.Itens[0].Descricao);
        Assert.Equal(10.00m, gabarito.Itens[0].Preco);
        Assert.Equal("Feijão", gabarito.Itens[1].Descricao);
        Assert.Equal(2.9m, gabarito.Itens[1].Preco);
        Assert.Equal("GabaritoComprovante", gabarito.ToString());
    }

    [Fact]
    public void ItensVazios_SaoValidos()
    {
        const string json = """
            { "id": "sint-vazio", "data": "2026-10-18", "total": 0, "itens": [] }
            """;

        var gabarito = LeitorGabarito.Ler(json);

        Assert.Empty(gabarito.Itens);
        Assert.Equal(0m, gabarito.TotalCupom);
    }

    [Theory]
    [InlineData("2026-13-01")]
    [InlineData("18/10/2026")]
    [InlineData("2026-10-18T00:00:00")]
    [InlineData("2026-02-31")]
    [InlineData("")]
    public void DataInvalida_LancaFormatExceptionComMotivoFixo(string data)
    {
        var json = $$"""
            { "id": "sint-001", "data": "{{data}}", "total": 12.90, "itens": [] }
            """;

        var excecao = Assert.Throws<FormatException>(() => LeitorGabarito.Ler(json));

        Assert.Equal(LeitorGabarito.MotivoCampoInvalido("data"), excecao.Message);
        if (data.Length > 0)
            Assert.DoesNotContain(data, excecao.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TotalAusente_LancaFormatExceptionComMotivoFixo()
    {
        const string json = """
            { "id": "sint-001", "data": "2026-10-18", "itens": [] }
            """;

        var excecao = Assert.Throws<FormatException>(() => LeitorGabarito.Ler(json));

        Assert.Equal(LeitorGabarito.MotivoCampoAusente("total"), excecao.Message);
    }

    [Fact]
    public void ItemSemPreco_LancaFormatExceptionComMotivoFixo()
    {
        const string json = """
            {
              "id": "sint-001",
              "data": "2026-10-18",
              "total": 12.90,
              "itens": [ { "descricao": "Arroz" } ]
            }
            """;

        var excecao = Assert.Throws<FormatException>(() => LeitorGabarito.Ler(json));

        Assert.Equal(LeitorGabarito.MotivoCampoAusente("preco"), excecao.Message);
    }

    [Fact]
    public void DecimalComPonto_IndependeDaCultura()
    {
        const string json = """
            {
              "id": "sint-001",
              "data": "2026-10-18",
              "total": 12.90,
              "itens": [ { "descricao": "Arroz", "preco": 0.125 } ]
            }
            """;
        var cultura = CultureInfo.CurrentCulture;
        var ui = CultureInfo.CurrentUICulture;
        try
        {
            var ptBr = CultureInfo.GetCultureInfo("pt-BR");
            CultureInfo.CurrentCulture = ptBr;
            CultureInfo.CurrentUICulture = ptBr;

            var gabarito = LeitorGabarito.Ler(json);

            Assert.Equal(',', CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0]);
            Assert.Equal(12.90m, gabarito.TotalCupom);
            Assert.Equal(0.125m, Assert.Single(gabarito.Itens).Preco);
        }
        finally
        {
            CultureInfo.CurrentCulture = cultura;
            CultureInfo.CurrentUICulture = ui;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nao-json")]
    [InlineData("[1, 2]")]
    public void JsonInvalido_MotivoFixo(string? json)
    {
        var excecao = Assert.Throws<FormatException>(() => LeitorGabarito.Ler(json!));

        Assert.Equal(LeitorGabarito.MotivoJsonInvalido, excecao.Message);
    }
}
