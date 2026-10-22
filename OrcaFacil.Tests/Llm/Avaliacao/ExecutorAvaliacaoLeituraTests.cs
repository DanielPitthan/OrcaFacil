using System.Net;
using System.Text;
using OrcaFacil.Core.Llm;
using OrcaFacil.Core.Llm.Avaliacao;

namespace OrcaFacil.Tests.Llm.Avaliacao;

public class ExecutorAvaliacaoLeituraTests
{
    [Fact]
    public async Task FakePorId_ConsolidaSemRede()
    {
        var data = new DateOnly(2026, 10, 18);
        var porId = new Dictionary<string, ResultadoMensagem>
        {
            ["sint-001"] = ResultadoMensagem.Ok(Cupom(data, 12.90m, ("Arroz", 10m), ("Feijão", 2.90m))),
            ["sint-002"] = ResultadoMensagem.Falha(TipoResultadoMensagem.Timeout, ResultadoMensagem.TextoTimeout)
        };
        var executor = new ExecutorAvaliacaoLeitura(new LeitorPorId(porId));
        var casos = new (FotoComprovantePronta Foto, GabaritoComprovante Gabarito)[]
        {
            (Foto("sint-001"), Gabarito("sint-001", data, 12.90m, ("Arroz", 10m), ("Feijão", 2.90m))),
            (Foto("sint-002"), Gabarito("sint-002", data, 4m, ("Bala", 4m)))
        };

        var resultado = await executor.ExecutarAsync(casos, MetasLeituraComprovante.Padrao);

        Assert.Equal(2, resultado.Casos.Count);
        Assert.True(resultado.Casos[0].LeituraOk);
        Assert.True(resultado.Casos[0].TotalCorreto);
        Assert.Equal(2, resultado.Casos[0].PrecosCorretos);
        Assert.False(resultado.Casos[1].LeituraOk);
        Assert.False(resultado.AtingeMetas);
        var relatorio = AvaliadorLeituraComprovante.Relatorio(resultado);
        Assert.DoesNotContain("sk-", relatorio, StringComparison.Ordinal);
        Assert.DoesNotContain(ResultadoMensagem.TextoTimeout, relatorio, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandlerFalso_LeFixtureQwenPeloLeitorReal()
    {
        var diretorio = DiretorioFixtures();
        var resposta1 = await File.ReadAllTextAsync(Path.Combine(diretorio, "sint-001.resposta.json"));
        var resposta2 = await File.ReadAllTextAsync(Path.Combine(diretorio, "sint-002.resposta.json"));
        Assert.Contains("registrar_comprovante", resposta1, StringComparison.Ordinal);
        Assert.Contains("registrar_comprovante", resposta2, StringComparison.Ordinal);
        Assert.DoesNotContain("output_config", resposta1, StringComparison.Ordinal);
        Assert.DoesNotContain("output_config", resposta2, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-", resposta1, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-", resposta2, StringComparison.Ordinal);

        var handler = new HandlerFalso(id => id == "sint-002" ? resposta2 : resposta1);
        using var cliente = new ClienteMensagens(handler);
        var leitor = new LeitorPeloCliente(cliente);
        var executor = new ExecutorAvaliacaoLeitura(leitor);
        var casos = new (FotoComprovantePronta Foto, GabaritoComprovante Gabarito)[]
        {
            (Foto("sint-001"), LeitorGabarito.Ler(await File.ReadAllTextAsync(Path.Combine(diretorio, "sint-001.gabarito.json")))),
            (Foto("sint-002"), LeitorGabarito.Ler(await File.ReadAllTextAsync(Path.Combine(diretorio, "sint-002.gabarito.json"))))
        };

        var resultado = await executor.ExecutarAsync(casos, MetasLeituraComprovante.Padrao);

        Assert.Equal(2, handler.Chamadas);
        Assert.Equal(ChipsInteligencia.UrlAlibaba.TrimEnd('/') + "/v1/messages", handler.Uri);
        Assert.Equal(PedidoMensagem.ModeloAlibaba, handler.Modelo);
        Assert.DoesNotContain("output_config", handler.Corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("chave-teste", handler.Corpo, StringComparison.Ordinal);
        Assert.Equal(2, resultado.Casos.Count);
        Assert.All(resultado.Casos, caso =>
        {
            Assert.True(caso.LeituraOk);
            Assert.True(caso.TotalCorreto);
            Assert.True(caso.DataCorreta);
            Assert.Equal(caso.ItensNoGabarito, caso.PrecosCorretos);
        });
        Assert.True(resultado.AtingeMetas);

        var relatorio = AvaliadorLeituraComprovante.Relatorio(resultado);
        Assert.Contains("sint-001", relatorio, StringComparison.Ordinal);
        Assert.Contains("sint-002", relatorio, StringComparison.Ordinal);
        Assert.DoesNotContain("Loja Sintetica", relatorio, StringComparison.Ordinal);
        Assert.DoesNotContain("chave-teste", relatorio, StringComparison.Ordinal);
        Assert.DoesNotContain("output_config", relatorio, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToBase64String(casos[0].Foto.Jpeg!), relatorio, StringComparison.Ordinal);
        AssertSemSegredo(relatorio);
    }

    [Fact]
    public void FixturesSinteticas_NaoTemSegredoNemOutputConfig()
    {
        var diretorio = DiretorioFixtures();
        var arquivos = Directory.GetFiles(diretorio, "*.json");

        Assert.Equal(4, arquivos.Length);
        foreach (var arquivo in arquivos)
        {
            var nome = Path.GetFileName(arquivo);
            Assert.StartsWith("sint-", nome, StringComparison.Ordinal);
            var texto = File.ReadAllText(arquivo);
            Assert.DoesNotContain("output_config", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("sk-", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("x-api-key", texto, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Authorization", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("base64", texto, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("cnpj", texto, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task TokenCancelado_Propaga()
    {
        var chamadas = 0;
        var leitor = new LeitorDelegado((_, ct) =>
        {
            chamadas++;
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(ResultadoMensagem.ConexaoOk());
        });
        var executor = new ExecutorAvaliacaoLeitura(leitor);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var excecao = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            executor.ExecutarAsync([CasoUnico()], MetasLeituraComprovante.Padrao, cts.Token));

        Assert.True(cts.IsCancellationRequested);
        Assert.Equal(0, chamadas);
        Assert.IsAssignableFrom<OperationCanceledException>(excecao);
    }

    [Fact]
    public async Task CancelamentoDuranteALeitura_Propaga()
    {
        var leitor = new LeitorDelegado((_, ct) => throw new OperationCanceledException(ct));
        var executor = new ExecutorAvaliacaoLeitura(leitor);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            executor.ExecutarAsync([CasoUnico()], MetasLeituraComprovante.Padrao));
    }

    [Fact]
    public async Task HandlerCancelado_PropagaSemHttpReal()
    {
        var handler = new HandlerFalso(_ => throw new InvalidOperationException("não devia responder"));
        using var cliente = new ClienteMensagens(handler);
        var executor = new ExecutorAvaliacaoLeitura(new LeitorPeloCliente(cliente));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            executor.ExecutarAsync([CasoUnico()], MetasLeituraComprovante.Padrao, cts.Token));

        Assert.Equal(0, handler.Chamadas);
    }

    private static void AssertSemSegredo(string texto)
    {
        Assert.DoesNotContain("sk-", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("x-api-key", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("base64", texto, StringComparison.OrdinalIgnoreCase);
    }

    private static (FotoComprovantePronta Foto, GabaritoComprovante Gabarito) CasoUnico() =>
        (Foto("sint-001"), Gabarito("sint-001", new DateOnly(2026, 10, 18), 10m, ("Arroz", 10m)));

    private static FotoComprovantePronta Foto(string id) =>
        FotoComprovantePronta.ComJpeg(Encoding.UTF8.GetBytes(id));

    private static GabaritoComprovante Gabarito(
        string id,
        DateOnly data,
        decimal total,
        params (string Descricao, decimal Preco)[] itens) =>
        new(id, data, total, itens.Select(item => new GabaritoItem(item.Descricao, item.Preco)).ToArray());

    private static ComprovanteLido Cupom(
        DateOnly data,
        decimal total,
        params (string Descricao, decimal Preco)[] itens) =>
        new(
            null,
            data,
            total,
            true,
            itens.Select(item => new ItemComprovanteLido(
                item.Descricao,
                item.Descricao,
                true,
                item.Preco,
                null,
                null,
                null,
                ConfiancaLeitura.Alta)).ToArray());

    private static string DiretorioFixtures()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidato = Path.Combine(dir.FullName, "Fixtures", "Llm", "Avaliacao");
            if (Directory.Exists(candidato))
                return candidato;

            var noProjeto = Path.Combine(dir.FullName, "OrcaFacil.Tests", "Fixtures", "Llm", "Avaliacao");
            if (Directory.Exists(noProjeto))
                return noProjeto;

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Fixtures do L2 não encontradas a partir de " + AppContext.BaseDirectory);
    }

    private sealed class LeitorPorId(Dictionary<string, ResultadoMensagem> porId) : ILeitorComprovante
    {
        public Task<ResultadoMensagem> LerAsync(FotoComprovantePronta foto, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var id = Encoding.UTF8.GetString(foto.Jpeg ?? []);
            return Task.FromResult(porId[id]);
        }
    }

    private sealed class LeitorDelegado(
        Func<FotoComprovantePronta, CancellationToken, Task<ResultadoMensagem>> ler) : ILeitorComprovante
    {
        public Task<ResultadoMensagem> LerAsync(FotoComprovantePronta foto, CancellationToken ct = default) =>
            ler(foto, ct);
    }

    private sealed class LeitorPeloCliente(ClienteMensagens cliente) : ILeitorComprovante
    {
        public Task<ResultadoMensagem> LerAsync(FotoComprovantePronta foto, CancellationToken ct = default)
        {
            var base64 = Convert.ToBase64String(foto.Jpeg ?? []);
            return cliente.LerComprovanteAsync(
                ChipsInteligencia.UrlAlibaba,
                "chave-teste",
                PedidoMensagem.ModeloAlibaba,
                "image/jpeg",
                base64,
                ct);
        }
    }

    private sealed class HandlerFalso : HttpMessageHandler
    {
        private readonly Func<string, string> _corpoPorId;

        public HandlerFalso(Func<string, string> corpoPorId)
        {
            _corpoPorId = corpoPorId;
        }

        public int Chamadas { get; private set; }
        public string Uri { get; private set; } = string.Empty;
        public string? Modelo { get; private set; }
        public string Corpo { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Chamadas++;
            Uri = request.RequestUri?.AbsoluteUri ?? string.Empty;
            Corpo = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            using var doc = System.Text.Json.JsonDocument.Parse(Corpo);
            Modelo = doc.RootElement.GetProperty("model").GetString();
            var id = Encoding.UTF8.GetString(JpegDoPedido(Corpo));
            var json = _corpoPorId(id);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }

        private static byte[] JpegDoPedido(string corpo)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(corpo);
            foreach (var bloco in doc.RootElement.GetProperty("messages")[0].GetProperty("content").EnumerateArray())
            {
                if (bloco.GetProperty("type").GetString() != "image")
                    continue;

                var dados = bloco.GetProperty("source").GetProperty("data").GetString() ?? string.Empty;
                return Convert.FromBase64String(dados);
            }

            return [];
        }
    }
}
