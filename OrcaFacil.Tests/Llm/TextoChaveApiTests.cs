using OrcaFacil.Core.Llm;

namespace OrcaFacil.Tests.Llm;

public class TextoChaveApiTests
{
    [Theory]
    [InlineData("\0")]
    [InlineData("sk-ant-\0resto")]
    [InlineData("linha\nquebrada")]
    [InlineData("linha\rquebrada")]
    public void RecusarInvalida_RecusaLixo_ENaoRepeteAChave(string chave)
    {
        var excecao = Assert.Throws<ArgumentException>(() => TextoChaveApi.RecusarInvalida(chave));

        Assert.Contains(TextoChaveApi.MensagemInvalida, excecao.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(chave, excecao.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("\0", excecao.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RecusarInvalida_AceitaChaveComum()
    {
        var excecao = Record.Exception(() => TextoChaveApi.RecusarInvalida("sk-ant-comum"));

        Assert.Null(excecao);
    }
}
