using System.Net;
using System.Security.Cryptography;
using OrcaFacil.Core.Llm;

namespace OrcaFacil.Tests.Llm;

public class LeituraChaveApiTests
{
    private const string Url = "https://api.anthropic.com";
    private const string Salva = "sk-ant-salva";

    [Fact]
    public async Task LeituraQueLanca_ViraSemChave_EChamaRemoveAll()
    {
        var cofre = CofreComFalha(new InvalidOperationException("keystore"));

        var valor = await cofre.LerAsync();

        Assert.Null(valor);
        Assert.Equal(1, cofre.Remocoes);
        Assert.Null(cofre.Chave);
    }

    [Fact]
    public async Task ValorCorrompido_ViraSemChave_EChamaRemoveAll()
    {
        var cofre = new CofreDeTeste("chave\r\ncorrompida");

        var valor = await cofre.LerAsync();

        Assert.Null(valor);
        Assert.Equal(1, cofre.Remocoes);
        Assert.Null(cofre.Chave);
    }

    [Fact]
    public async Task RemoveAllQueLanca_AindaAssimNaoHaChave()
    {
        var valor = await LeituraChaveApi.LerAsync(
            _ => throw new CryptographicException("ilegível"),
            () => throw new IOException("não apagou " + Salva));

        Assert.Null(valor);
    }

    [Fact]
    public async Task Cancelamento_NaoApagaAChaveSalva()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var cofre = new CofreDeTeste(Salva);

        var excecao = await Record.ExceptionAsync(() => cofre.LerAsync(cts.Token));

        Assert.IsAssignableFrom<OperationCanceledException>(excecao);
        Assert.Equal(0, cofre.Remocoes);
        Assert.Equal(Salva, cofre.Chave);
        Assert.Equal(0, cofre.Leituras);
    }

    [Fact]
    public async Task CanceladoDepoisDaLeitura_NaoViraSemChave_ENaoApaga()
    {
        using var cts = new CancellationTokenSource();
        var apagou = 0;

        var excecao = await Record.ExceptionAsync(() => LeituraChaveApi.LerAsync(
            _ =>
            {
                cts.Cancel();
                return Task.FromResult<string?>(Salva);
            },
            () =>
            {
                apagou++;
                return false;
            },
            cts.Token));

        Assert.IsAssignableFrom<OperationCanceledException>(excecao);
        Assert.Equal(0, apagou);
    }

    [Fact]
    public async Task FalhaComTokenCanceladoDuranteALeitura_NaoApaga()
    {
        using var cts = new CancellationTokenSource();
        var apagou = 0;

        var excecao = await Record.ExceptionAsync(() => LeituraChaveApi.LerAsync(
            _ =>
            {
                cts.Cancel();
                throw new InvalidOperationException("keystore");
            },
            () =>
            {
                apagou++;
                return false;
            },
            cts.Token));

        Assert.IsAssignableFrom<OperationCanceledException>(excecao);
        Assert.Equal(0, apagou);
    }

    [Fact]
    public async Task OperationCanceledException_SemOTokenCancelado_TambemNaoApaga()
    {
        var apagou = 0;

        var excecao = await Record.ExceptionAsync(() => LeituraChaveApi.LerAsync(
            _ => throw new OperationCanceledException(),
            () =>
            {
                apagou++;
                return false;
            }));

        Assert.IsAssignableFrom<OperationCanceledException>(excecao);
        Assert.Equal(0, apagou);
    }

    [Fact]
    public async Task ComChaveSalva_InformeAChaveSoQuandoNaoHaChave()
    {
        using var cliente = new ClienteMensagens(new RespostaOk());

        var comChave = await ResolucaoChaveApi.ResolverAsync(new CofreDeTeste(Salva), null);
        var leitura = await cliente.TestarConexaoAsync(Url, comChave, PedidoMensagem.ModeloAnthropic);
        var texto = ClienteMensagens.TextoExibicao(leitura, comChave);
        Assert.NotEqual(ResultadoMensagem.TextoChaveNaoInformada, texto);
        Assert.DoesNotContain(Salva, texto, StringComparison.Ordinal);
        Assert.Equal(ResultadoMensagem.TextoConexaoOk, texto);

        var semChave = await ResolucaoChaveApi.ResolverAsync(new CofreDeTeste(null), null);
        var pedido = await cliente.TestarConexaoAsync(Url, semChave, PedidoMensagem.ModeloAnthropic);
        Assert.Equal(ResultadoMensagem.TextoChaveNaoInformada, pedido.Mensagem);
        Assert.Equal(
            ResultadoMensagem.TextoChaveNaoInformada,
            ClienteMensagens.TextoExibicao(pedido, semChave));

        var ilegivel = CofreComFalha(new CryptographicException("keystore"));
        var lida = await ResolucaoChaveApi.ResolverAsync(ilegivel, null);
        var falha = await cliente.TestarConexaoAsync(Url, lida, PedidoMensagem.ModeloAnthropic);
        Assert.Equal(ResultadoMensagem.TextoChaveNaoInformada, falha.Mensagem);
        Assert.Equal(1, ilegivel.Remocoes);
        Assert.DoesNotContain(Salva, falha.Mensagem, StringComparison.Ordinal);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var salva = new CofreDeTeste(Salva);
        var cancelou = await Record.ExceptionAsync(() => ResolucaoChaveApi.ResolverAsync(salva, null, cts.Token));
        Assert.IsAssignableFrom<OperationCanceledException>(cancelou);
        Assert.Equal(0, salva.Remocoes);
        Assert.Equal(Salva, salva.Chave);
    }

    [Fact]
    public void ClientePainelECofre_NaoGravamChaveNemFotoNoLog()
    {
        var cliente = Ler("OrcaFacil.Core", "Llm", "ClienteMensagens.cs");
        var leitura = Ler("OrcaFacil.Core", "Llm", "LeituraChaveApi.cs");
        var resolucao = Ler("OrcaFacil.Core", "Llm", "ResolucaoChaveApi.cs");
        var cofre = Ler("OrcaFacil", "Services", "CofreChaveApiSecureStorage.cs");
        var painel = Ler("OrcaFacil", "Components", "Pages", "Configuracoes", "InteligenciaArtificialPanel.razor");
        var foto = Ler("OrcaFacil", "Services", "FotoPerfilService.cs");
        var csv = Ler("OrcaFacil", "Services", "CsvExportService.cs");

        foreach (var texto in new[] { cliente, leitura, resolucao, cofre, painel })
        {
            Assert.DoesNotContain("Log.", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("ILogger", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("LogInformation", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("LogWarning", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("LogError", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("LogDebug", texto, StringComparison.Ordinal);
        }

        Assert.Contains("LeituraChaveApi.LerAsync", cofre, StringComparison.Ordinal);
        Assert.Contains("SecureStorage.Default.GetAsync(ChaveArmazenamento)", cofre, StringComparison.Ordinal);
        Assert.Contains("SecureStorage.Default.Remove(ChaveArmazenamento)", cofre, StringComparison.Ordinal);
        Assert.Contains("SecureStorage.Default.RemoveAll()", cofre, StringComparison.Ordinal);
        Assert.Contains(
            "ResolucaoChaveApi.ResolverAsync(CofreChave, _chaveDigitada, cancellationToken)",
            painel,
            StringComparison.Ordinal);
        Assert.DoesNotContain("return null;", painel, StringComparison.Ordinal);
        Assert.Contains("ClienteMensagens.TextoExibicao(resultado, chave)", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("Snackbar.Add($", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("Snackbar.Add(chave", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("Snackbar.Add(_chaveDigitada", painel, StringComparison.Ordinal);

        var linhaLog = foto.Split('\n').Single(linha => linha.Contains("LogWarning", StringComparison.Ordinal));
        Assert.Contains("Falha ao redimensionar a foto; salvando o original.", linhaLog, StringComparison.Ordinal);
        Assert.DoesNotContain("base64", linhaLog, StringComparison.Ordinal);
        Assert.DoesNotContain("Convert.ToBase64String", linhaLog, StringComparison.Ordinal);
        Assert.DoesNotContain('$', linhaLog);

        Assert.DoesNotContain("Ia.ChaveApi", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("ICofreChaveApi", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("SecureStorage", csv, StringComparison.Ordinal);
    }

    private static CofreDeTeste CofreComFalha(Exception falha) => new(Salva) { Falha = falha };

    private static string Ler(params string[] partes)
    {
        var relativo = Path.Combine(partes);
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var caminho = Path.Combine(dir.FullName, relativo);
            if (File.Exists(caminho))
                return File.ReadAllText(caminho);

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar " + relativo);
    }

    private sealed class CofreDeTeste : ICofreChaveApi
    {
        public CofreDeTeste(string? chave) => Chave = chave;

        public string? Chave { get; private set; }

        public Exception? Falha { get; init; }

        public int Remocoes { get; private set; }

        public int Leituras { get; private set; }

        public Task<string?> LerAsync(CancellationToken cancellationToken = default)
            => LeituraChaveApi.LerAsync(
                token =>
                {
                    Leituras++;
                    token.ThrowIfCancellationRequested();
                    if (Falha is not null)
                        throw Falha;

                    return Task.FromResult(Chave);
                },
                RemoverTudo,
                cancellationToken);

        public Task GuardarAsync(string chave, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Chave = chave;
            return Task.CompletedTask;
        }

        public bool RemoverTudo()
        {
            Remocoes++;
            Chave = null;
            return true;
        }
    }

    private sealed class RespostaOk : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });
    }
}
