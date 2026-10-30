using OrcaFacil.Core.Categorias;

namespace OrcaFacil.Tests.Categorias;

public class SessaoVoltarPaletaTests
{
    [Fact]
    public void SemPaleta_OVoltarSegueParaAPagina()
    {
        var sessao = new SessaoVoltarPaleta();

        Assert.False(sessao.Aberta);
        Assert.False(sessao.TentarConsumir());
    }

    [Fact]
    public void ComPaleta_OVoltarFechaSoEla_ENaoLiberaAPaginaAntesDoFechar()
    {
        var sessao = new SessaoVoltarPaleta();
        var fechou = 0;
        void Fechar() => fechou++;

        sessao.Abrir(Fechar);

        Assert.True(sessao.Aberta);
        Assert.True(sessao.TentarConsumir());
        Assert.Equal(1, fechou);
        Assert.True(sessao.Aberta);
        Assert.True(sessao.TentarConsumir());
        Assert.Equal(2, fechou);
    }

    [Fact]
    public void FecharAMesmaAcao_DevolveOVoltarParaAPagina()
    {
        var sessao = new SessaoVoltarPaleta();
        var fechou = 0;
        Action fechar = () => fechou++;

        sessao.Abrir(fechar);
        sessao.Fechar(fechar);

        Assert.False(sessao.Aberta);
        Assert.False(sessao.TentarConsumir());
        Assert.Equal(0, fechou);
    }

    [Fact]
    public void FecharDeOutraAcao_NaoSoltaAPaletaAtual()
    {
        var sessao = new SessaoVoltarPaleta();
        var atual = 0;
        var antiga = 0;
        Action fecharAtual = () => atual++;
        Action fecharAntiga = () => antiga++;

        sessao.Abrir(fecharAntiga);
        sessao.Abrir(fecharAtual);
        sessao.Fechar(fecharAntiga);

        Assert.True(sessao.TentarConsumir());
        Assert.Equal(1, atual);
        Assert.Equal(0, antiga);
    }

    [Fact]
    public void AcaoNula_Recusa()
    {
        var sessao = new SessaoVoltarPaleta();

        Assert.Throws<ArgumentNullException>(() => sessao.Abrir(null!));
        Assert.Throws<ArgumentNullException>(() => sessao.Fechar(null!));
        Assert.False(sessao.Aberta);
    }
}
