using System.Globalization;
using OrcaFacil.Core.Tema;

namespace OrcaFacil.Tests.Tema;

public class LeituraTemaGravadoTests
{
    [Theory]
    [InlineData(TemaPreferido.Claro, "Claro")]
    [InlineData(TemaPreferido.Escuro, "Escuro")]
    [InlineData(TemaPreferido.Automatico, "Automatico")]
    public void TextoJaGravado_ContinuaLido_SemRegravar(TemaPreferido tema, string texto)
    {
        var repositorio = new RepositorioPreferenciasMemoria();
        repositorio.GravarTexto(PreferenciasTema.Chave, texto);
        var adaptador = new AdaptadorPreferencias(repositorio);

        Assert.Equal(tema, new PreferenciasTema(adaptador).Tema);
        Assert.Equal(texto, repositorio.LerTexto(PreferenciasTema.Chave));
        Assert.Null(repositorio.LerInteiro(PreferenciasTema.Chave));
    }

    [Theory]
    [InlineData(TemaPreferido.Automatico)]
    [InlineData(TemaPreferido.Claro)]
    [InlineData(TemaPreferido.Escuro)]
    public void InteiroJaGravado_ContinuaLido_SemRegravar(TemaPreferido tema)
    {
        var repositorio = new RepositorioPreferenciasMemoria();
        repositorio.GravarInteiro(PreferenciasTema.Chave, (int)tema);
        var adaptador = new AdaptadorPreferencias(repositorio);

        Assert.Equal(tema, new PreferenciasTema(adaptador).Tema);
        Assert.Equal((int)tema, repositorio.LerInteiro(PreferenciasTema.Chave));
        Assert.Null(repositorio.LerTexto(PreferenciasTema.Chave));
        Assert.Equal(
            ((int)tema).ToString(CultureInfo.InvariantCulture),
            adaptador.Obter(PreferenciasTema.Chave, MapeamentoTema.TextoAutomatico));
    }
}
