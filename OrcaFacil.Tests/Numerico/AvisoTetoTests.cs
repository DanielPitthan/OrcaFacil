using OrcaFacil.Core.Numerico;

namespace OrcaFacil.Tests.Numerico;

public class AvisoTetoTests
{
    [Fact]
    public void Mensagem_UsaOTetoRealEmPtBr()
    {
        Assert.Equal("O máximo é 100.", AvisoTeto.Mensagem(100m));
        Assert.Equal("O máximo é 1000000000.", AvisoTeto.Mensagem(1_000_000_000m));
        Assert.Equal("O máximo é 100,5.", AvisoTeto.Mensagem(100.5m));
    }

    [Fact]
    public void Digitar101_MostraOMaximo_EUmValorValidoApagaOErro()
    {
        var digitos = "";
        string? erro = null;
        foreach (var tecla in "101")
            (digitos, erro) = Passo(digitos, tecla.ToString(), erro, substituir: false);

        Assert.Equal("10", digitos);
        Assert.Equal("O máximo é 100.", erro);

        (digitos, erro) = Passo(digitos, "0", erro, substituir: false);
        Assert.Equal("100", digitos);
        Assert.Null(erro);
    }

    [Fact]
    public void Colar101_MostraOMaximo_ENaoReescreveOTexto()
    {
        var (vazio, erroVazio) = Passo("", "101", erro: null, substituir: true);
        Assert.Equal("", vazio);
        Assert.Equal("O máximo é 100.", erroVazio);

        var (mantem, erroMantem) = Passo("12,5", "101", erro: null, substituir: true);
        Assert.Equal("12,5", mantem);
        Assert.Equal("O máximo é 100.", erroMantem);

        var (valido, semErro) = Passo(mantem, "50", erroMantem, substituir: true);
        Assert.Equal("50", valido);
        Assert.Null(semErro);
    }

    [Fact]
    public void PontoVirgulaExtraEValorDentroDoTeto_NaoMostramErro()
    {
        var ponto = AvisoTeto.Aplicar("12", ".", 1, 100m, substituir: false, fracaoExplicita: true);
        Assert.Equal("12", ponto.Digitos);
        Assert.False(ponto.PassouDoTeto);

        var extra = AvisoTeto.Aplicar("12,5", "9", 1, 100m, substituir: false, fracaoExplicita: true);
        Assert.Equal("12,5", extra.Digitos);
        Assert.False(extra.PassouDoTeto);

        var digitos = "";
        string? erro = null;
        foreach (var tecla in "12,5")
            (digitos, erro) = Passo(digitos, tecla.ToString(), erro, substituir: false);

        Assert.Equal("12,5", digitos);
        Assert.Null(erro);
        Assert.Equal("100,", AvisoTeto.Aplicar("100", ",", 1, 100m, substituir: false, fracaoExplicita: true).Digitos);
        Assert.True(AvisoTeto.Aplicar("100,", "1", 1, 100m, substituir: false, fracaoExplicita: true).PassouDoTeto);
    }

    [Fact]
    public void EscalaFixa_TambemAvistaOTetoSemVirarOutroNumero()
    {
        var digitado = AvisoTeto.Aplicar("", "101", 0, 100m, substituir: false, fracaoExplicita: false);
        Assert.Equal("10", digitado.Digitos);
        Assert.True(digitado.PassouDoTeto);
        Assert.Equal("O máximo é 100.", AvisoTeto.Mensagem(100m));

        var colado = AvisoTeto.Aplicar("12", "101", 0, 100m, substituir: true, fracaoExplicita: false);
        Assert.Equal("12", colado.Digitos);
        Assert.True(colado.PassouDoTeto);
    }

    private static (string Digitos, string? Erro) Passo(string digitos, string entrada, string? erro, bool substituir)
    {
        var resultado = AvisoTeto.Aplicar(digitos, entrada, 1, 100m, substituir, fracaoExplicita: true);
        var inalterado = resultado.Digitos == digitos;
        if (inalterado && !resultado.PassouDoTeto)
            return (digitos, erro);

        var novoErro = resultado.PassouDoTeto ? AvisoTeto.Mensagem(100m) : null;
        return (resultado.Digitos, novoErro);
    }
}
