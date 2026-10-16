using OrcaFacil.Core.Tema;

namespace OrcaFacil.Tests.Tema;

public class AplicacaoDeTemaTests
{
    [Theory]
    [InlineData(TemaPreferido.Claro, "Claro", TemaUsuarioApp.Light)]
    [InlineData(TemaPreferido.Escuro, "Escuro", TemaUsuarioApp.Dark)]
    [InlineData(TemaPreferido.Automatico, "Automatico", TemaUsuarioApp.Unspecified)]
    public void DefinirEAplicar_GravaAChaveEAplicaOTema(
        TemaPreferido tema,
        string texto,
        TemaUsuarioApp userAppTheme)
    {
        var memoria = new PreferenciasEmMemoria();
        var preferencias = new PreferenciasTema(memoria);
        var aplicados = new List<TemaUsuarioApp>();

        var resultado = AplicacaoDeTema.DefinirEAplicar(preferencias, tema, aplicados.Add);

        Assert.Equal(userAppTheme, resultado);
        Assert.Equal(new[] { userAppTheme }, aplicados);
        Assert.Equal(texto, memoria.ValorOuNulo(PreferenciasTema.Chave));
        Assert.Equal(tema, preferencias.Tema);
    }

    [Fact]
    public void DefinirEAplicar_SubstituiOTemaAnterior()
    {
        var memoria = new PreferenciasEmMemoria();
        var preferencias = new PreferenciasTema(memoria);
        var aplicados = new List<TemaUsuarioApp>();

        AplicacaoDeTema.DefinirEAplicar(preferencias, TemaPreferido.Claro, aplicados.Add);
        AplicacaoDeTema.DefinirEAplicar(preferencias, TemaPreferido.Escuro, aplicados.Add);

        Assert.Equal([TemaUsuarioApp.Light, TemaUsuarioApp.Dark], aplicados);
        Assert.Equal("Escuro", memoria.ValorOuNulo(PreferenciasTema.Chave));
        Assert.Equal(TemaPreferido.Escuro, preferencias.Tema);
    }
}
