using OrcaFacil.Core.Armazenamento.Espelho.Pasta;
using OrcaFacil.Core.Tema;

namespace OrcaFacil.Tests.Tema;

public class PreferenciasTemaTests
{
    [Fact]
    public void Padrao_EAutomatico_SemGravarAChave()
    {
        var memoria = new PreferenciasEmMemoria();
        var preferencias = new PreferenciasTema(memoria);

        Assert.Equal(TemaPreferido.Automatico, preferencias.Tema);
        Assert.Equal(TemaPreferido.Automatico, MapeamentoTema.Padrao);
        Assert.False(memoria.Contem(PreferenciasTema.Chave));
    }

    [Theory]
    [InlineData(TemaPreferido.Claro, "Claro")]
    [InlineData(TemaPreferido.Escuro, "Escuro")]
    [InlineData(TemaPreferido.Automatico, "Automatico")]
    public void Persistencia_GravaELeOTextoDaChave(TemaPreferido tema, string texto)
    {
        var memoria = new PreferenciasEmMemoria();
        var preferencias = new PreferenciasTema(memoria);

        preferencias.Tema = tema;

        Assert.Equal(PreferenciasTema.Chave, "Config.Tema");
        Assert.Equal(texto, memoria.ValorOuNulo(PreferenciasTema.Chave));
        Assert.Equal(tema, new PreferenciasTema(memoria).Tema);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("dark")]
    [InlineData("claro")]
    public void TextoDesconhecido_VoltaParaAutomatico(string? texto)
    {
        var memoria = new PreferenciasEmMemoria();
        if (texto is not null)
            memoria.Definir(PreferenciasTema.Chave, texto);

        Assert.Equal(TemaPreferido.Automatico, new PreferenciasTema(memoria).Tema);
    }

    [Fact]
    public void Redefinir_PreservaAChaveEApagaOResto()
    {
        var memoria = new PreferenciasEmMemoria();
        var preferencias = new PreferenciasTema(memoria);
        preferencias.Tema = TemaPreferido.Escuro;
        memoria.Definir("Config.NotificacoesAtivas", "false");
        memoria.Definir("Config.GraficoLancamentosExpandido", "false");

        preferencias.Redefinir();

        Assert.Equal("Escuro", memoria.ValorOuNulo(PreferenciasTema.Chave));
        Assert.Equal(TemaPreferido.Escuro, preferencias.Tema);
        Assert.False(memoria.Contem("Config.NotificacoesAtivas"));
        Assert.False(memoria.Contem("Config.GraficoLancamentosExpandido"));
    }

    [Fact]
    public void Redefinir_PreservaAUriDaPastaDoEspelho()
    {
        const string uri = "content://com.android.externalstorage.documents/tree/primary%3AOrca";
        var memoria = new PreferenciasEmMemoria();
        var preferencias = new PreferenciasTema(memoria);
        preferencias.Tema = TemaPreferido.Escuro;
        memoria.Definir(UriEscolhaPasta.ChavePreferencia, uri);
        memoria.Definir("Config.SomAtivo", "false");

        preferencias.Redefinir();

        Assert.Equal("Escuro", memoria.ValorOuNulo(PreferenciasTema.Chave));
        Assert.Equal(uri, memoria.ValorOuNulo(UriEscolhaPasta.ChavePreferencia));
        Assert.Equal(uri, new MemoriaPastaEspelho(memoria).LerUri());
        Assert.False(memoria.Contem("Config.SomAtivo"));
    }

    [Fact]
    public void Redefinir_SemUriDePasta_NaoCriaAChave()
    {
        var memoria = new PreferenciasEmMemoria();
        var preferencias = new PreferenciasTema(memoria);
        preferencias.Tema = TemaPreferido.Claro;

        preferencias.Redefinir();

        Assert.False(memoria.Contem(UriEscolhaPasta.ChavePreferencia));
        Assert.Equal("Claro", memoria.ValorOuNulo(PreferenciasTema.Chave));
    }

    [Fact]
    public void Redefinir_PreservaAutomaticoExplicito()
    {
        var memoria = new PreferenciasEmMemoria();
        var preferencias = new PreferenciasTema(memoria);
        preferencias.Tema = TemaPreferido.Automatico;
        memoria.Definir("Config.SomAtivo", "false");

        preferencias.Redefinir();

        Assert.Equal("Automatico", memoria.ValorOuNulo(PreferenciasTema.Chave));
        Assert.Equal(TemaPreferido.Automatico, preferencias.Tema);
        Assert.False(memoria.Contem("Config.SomAtivo"));
    }

    [Fact]
    public void Redefinir_SemChave_ContinuaAutomaticoENaoCriaAChave()
    {
        var memoria = new PreferenciasEmMemoria();
        var preferencias = new PreferenciasTema(memoria);
        memoria.Definir("Config.PrazosAviso", "4");

        preferencias.Redefinir();

        Assert.False(memoria.Contem(PreferenciasTema.Chave));
        Assert.Equal(TemaPreferido.Automatico, preferencias.Tema);
        Assert.False(memoria.Contem("Config.PrazosAviso"));
    }

    [Fact]
    public void Redefinir_PreservaOTextoOriginalDaChave()
    {
        var memoria = new PreferenciasEmMemoria();
        memoria.Definir(PreferenciasTema.Chave, "legado");
        var preferencias = new PreferenciasTema(memoria);
        memoria.Definir("Config.SomAtivo", "false");

        preferencias.Redefinir();

        Assert.Equal("legado", memoria.ValorOuNulo(PreferenciasTema.Chave));
        Assert.Equal(TemaPreferido.Automatico, preferencias.Tema);
    }
}
