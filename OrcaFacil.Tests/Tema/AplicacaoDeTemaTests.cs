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

        var resultado = AplicacaoDeTema.DefinirEAplicar(preferencias, tema, aplicado =>
        {
            Assert.Equal(texto, memoria.ValorOuNulo(PreferenciasTema.Chave));
            Assert.Equal(tema, preferencias.Tema);
            aplicados.Add(aplicado);
        });

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

        AplicacaoDeTema.DefinirEAplicar(preferencias, TemaPreferido.Claro, aplicado =>
        {
            Assert.Equal("Claro", memoria.ValorOuNulo(PreferenciasTema.Chave));
            aplicados.Add(aplicado);
        });
        AplicacaoDeTema.DefinirEAplicar(preferencias, TemaPreferido.Escuro, aplicado =>
        {
            Assert.Equal("Escuro", memoria.ValorOuNulo(PreferenciasTema.Chave));
            aplicados.Add(aplicado);
        });

        Assert.Equal([TemaUsuarioApp.Light, TemaUsuarioApp.Dark], aplicados);
        Assert.Equal("Escuro", memoria.ValorOuNulo(PreferenciasTema.Chave));
        Assert.Equal(TemaPreferido.Escuro, preferencias.Tema);
    }
}
