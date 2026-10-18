using OrcaFacil.Core.Armazenamento;
using OrcaFacil.Core.Armazenamento.Espelho.Pasta;
using OrcaFacil.Core.Llm;
using OrcaFacil.Core.Tema;

namespace OrcaFacil.Tests.Tema;

public class LimpezaChaveFalhouTests
{
    private const string Segredo = "sk-ant-segredo-que-nao-pode-vazar";
    private const string Uri = "content://com.android.externalstorage.documents/tree/primary%3AOrca";

    [Fact]
    public async Task CofreQueDevolveFalha_NaoReportaZerado_ELimpaOResto()
    {
        var memoria = new PreferenciasEmMemoria();
        var cofre = new CofreQueFalha(Segredo);
        var preferencias = new PreferenciasTema(memoria, cofre);
        Preparar(memoria, preferencias);

        var resultado = ResultadoLimpeza.Total(await preferencias.Redefinir());

        Assert.False(resultado.ChaveApagada);
        Assert.True(resultado.ChavePermaneceu);
        Assert.Equal(ResultadoLimpeza.TextoChaveNaoApagada, resultado.Mensagem);
        Assert.Equal(ResultadoLimpeza.TextoChaveNaoApagada, resultado.TextoRegistro);
        Assert.NotEqual(ResultadoLimpeza.TextoAplicacaoZerada, resultado.Mensagem);
        Assert.DoesNotContain(ResultadoLimpeza.TextoRegistroZerada, resultado.TextoRegistro, StringComparison.Ordinal);
        Assert.DoesNotContain(Segredo, resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain(Segredo, resultado.TextoRegistro, StringComparison.Ordinal);
        Assert.DoesNotContain(Segredo, resultado.ToString(), StringComparison.Ordinal);
        Assert.Equal(Segredo, cofre.Chave);
        AssertRestouSoTemaEPasta(memoria, preferencias);
    }

    [Fact]
    public async Task CofreQueLancaComAChaveNaExcecao_NaoVaza_ENaoReportaZerado()
    {
        var memoria = new PreferenciasEmMemoria();
        var cofre = new CofreQueLanca(Segredo);
        var preferencias = new PreferenciasTema(memoria, cofre);
        Preparar(memoria, preferencias);

        var resultado = ResultadoLimpeza.Total(await preferencias.Redefinir());

        Assert.True(resultado.ChavePermaneceu);
        Assert.Equal(ResultadoLimpeza.TextoChaveNaoApagada, resultado.Mensagem);
        Assert.DoesNotContain(Segredo, resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperation", resultado.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("keystore", resultado.Mensagem, StringComparison.Ordinal);
        Assert.Equal(Segredo, cofre.Chave);
        AssertRestouSoTemaEPasta(memoria, preferencias);
    }

    [Fact]
    public async Task CofreQueDizQueApagouMasAChaveFica_NaoReportaZerado_ENaSegundaVezApaga()
    {
        var memoria = new PreferenciasEmMemoria();
        var cofre = new CofreQueConfirmaLeitura(Segredo);
        var preferencias = new PreferenciasTema(memoria, cofre);
        Preparar(memoria, preferencias);

        var primeira = ResultadoLimpeza.Total(await preferencias.Redefinir());

        Assert.True(primeira.ChavePermaneceu);
        Assert.Equal(ResultadoLimpeza.TextoChaveNaoApagada, primeira.Mensagem);
        Assert.NotEqual(ResultadoLimpeza.TextoAplicacaoZerada, primeira.Mensagem);
        Assert.DoesNotContain(Segredo, primeira.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain(Segredo, primeira.ToString(), StringComparison.Ordinal);
        Assert.Equal(Segredo, cofre.Chave);
        Assert.Equal(1, cofre.LeiturasSemApagar);
        AssertRestouSoTemaEPasta(memoria, preferencias);

        cofre.PassarAApagar();
        var segunda = ResultadoLimpeza.Total(await preferencias.Redefinir());

        Assert.False(segunda.ChavePermaneceu);
        Assert.Equal(ResultadoLimpeza.TextoAplicacaoZerada, segunda.Mensagem);
        Assert.Null(cofre.Chave);
        Assert.Equal(2, cofre.LeiturasSemApagar);
        AssertRestouSoTemaEPasta(memoria, preferencias);
    }

    [Fact]
    public void CofreQueApaga_ContinuaDizendoAplicacaoZerada()
    {
        var resultado = ResultadoLimpeza.Total(chaveApagada: true);

        Assert.False(resultado.ChavePermaneceu);
        Assert.Equal(ResultadoLimpeza.TextoAplicacaoZerada, resultado.Mensagem);
        Assert.Equal(ResultadoLimpeza.TextoRegistroZerada, resultado.TextoRegistro);
        Assert.DoesNotContain(Segredo, resultado.Mensagem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("html.of-tema-claro", "#FFFFFF", "#9B2C3A")]
    [InlineData("html.of-tema-escuro", "#0B1220", "#E07171")]
    public void AvisoDaChave_FicaLegivelNoSnackbarDosDoisTemas(string tema, string texto, string fundo)
    {
        var css = File.ReadAllText(CaminhoCss());
        Assert.Contains($"{tema} .mud-snackbar.of-snack-chave", css, StringComparison.Ordinal);
        Assert.Contains($"color: {texto};", css, StringComparison.Ordinal);
        Assert.Contains($"background-color: {fundo};", css, StringComparison.Ordinal);

        var razao = Contraste(texto, fundo);
        Assert.True(razao >= 4.5, $"{texto} sobre {fundo} = {razao:0.00}");
    }

    private static void Preparar(PreferenciasEmMemoria memoria, PreferenciasTema preferencias)
    {
        preferencias.Tema = TemaPreferido.Escuro;
        memoria.Definir(UriEscolhaPasta.ChavePreferencia, Uri);
        memoria.Definir(ChipsInteligencia.ChaveUrl, "https://api.anthropic.com");
        memoria.Definir(ChipsInteligencia.ChaveModelo, PedidoMensagem.ModeloAnthropic);
        memoria.Definir("Config.NotificacoesAtivas", "false");
    }

    private static void AssertRestouSoTemaEPasta(PreferenciasEmMemoria memoria, PreferenciasTema preferencias)
    {
        Assert.Equal("Escuro", memoria.ValorOuNulo(PreferenciasTema.Chave));
        Assert.Equal(TemaPreferido.Escuro, preferencias.Tema);
        Assert.Equal(Uri, memoria.ValorOuNulo(UriEscolhaPasta.ChavePreferencia));
        Assert.Equal(Uri, new MemoriaPastaEspelho(memoria).LerUri());
        Assert.False(memoria.Contem(ChipsInteligencia.ChaveUrl));
        Assert.False(memoria.Contem(ChipsInteligencia.ChaveModelo));
        Assert.False(memoria.Contem("Config.NotificacoesAtivas"));
    }

    private static string CaminhoCss()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var caminho = Path.Combine(dir.FullName, "OrcaFacil", "wwwroot", "css", "orcafacil-theme.css");
            if (File.Exists(caminho))
                return caminho;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar orcafacil-theme.css.");
    }

    private static double Contraste(string frente, string fundo)
    {
        var clara = Math.Max(Luminancia(frente), Luminancia(fundo));
        var escura = Math.Min(Luminancia(frente), Luminancia(fundo));
        return (clara + 0.05) / (escura + 0.05);
    }

    private static double Luminancia(string hex)
    {
        hex = hex.TrimStart('#');
        var r = Canal(hex[..2]);
        var g = Canal(hex.Substring(2, 2));
        var b = Canal(hex.Substring(4, 2));
        return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
    }

    private static double Canal(string hex)
    {
        var valor = Convert.ToInt32(hex, 16) / 255d;
        return valor <= 0.04045 ? valor / 12.92 : Math.Pow((valor + 0.055) / 1.055, 2.4);
    }

    private sealed class CofreQueFalha : ICofreChaveApi
    {
        public CofreQueFalha(string chave) => Chave = chave;

        public string? Chave { get; private set; }

        public Task<string?> LerAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Chave);
        }

        public Task GuardarAsync(string chave, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Chave = chave;
            return Task.CompletedTask;
        }

        public Task<bool> RemoverTudo() => Task.FromResult(false);
    }

    private sealed class CofreQueLanca : ICofreChaveApi
    {
        public CofreQueLanca(string chave) => Chave = chave;

        public string Chave { get; }

        public Task<string?> LerAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<string?>(Chave);
        }

        public Task GuardarAsync(string chave, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("guardar " + Chave);

        public Task<bool> RemoverTudo() => throw new InvalidOperationException("keystore " + Chave);
    }

    /// <summary>
    /// <c>Remove</c> devolve sucesso e a chave continua. A leitura seguinte, sem apagar, denuncia a falha.
    /// Na repetição a chave sai de verdade.
    /// </summary>
    private sealed class CofreQueConfirmaLeitura : ICofreChaveApi
    {
        private bool _apagar;

        public CofreQueConfirmaLeitura(string chave) => Chave = chave;

        public string? Chave { get; private set; }

        public int LeiturasSemApagar { get; private set; }

        public void PassarAApagar() => _apagar = true;

        public Task<string?> LerAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Chave);
        }

        public Task GuardarAsync(string chave, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Chave = chave;
            return Task.CompletedTask;
        }

        public Task<bool> RemoverTudo()
            => RemocaoChaveApi.ConcluirAsync(
                () =>
                {
                    if (_apagar)
                        Chave = null;

                    return Task.FromResult(true);
                },
                () =>
                {
                    LeiturasSemApagar++;
                    return Task.FromResult(Chave);
                });
    }
}
