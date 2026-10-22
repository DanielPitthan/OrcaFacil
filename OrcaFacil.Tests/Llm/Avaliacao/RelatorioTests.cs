using System.Globalization;
using System.Text;
using OrcaFacil.Core.Llm;
using OrcaFacil.Core.Llm.Avaliacao;

namespace OrcaFacil.Tests.Llm.Avaliacao;

public class RelatorioTests
{
    private const string MarcadorFoto = "FOTO-SINTETICA-L2-NAO-RELATORIO";

    [Fact]
    public void ContemAsTresTaxasEOsIds()
    {
        var casos = new[]
        {
            new AvaliacaoComprovante("sint-001", true, true, true, 2, 2),
            new AvaliacaoComprovante("sint-002", true, false, true, 1, 0)
        };
        var resultado = AvaliadorLeituraComprovante.Consolidar(casos, MetasLeituraComprovante.Padrao);
        var texto = AvaliadorLeituraComprovante.Relatorio(resultado);
        var cultura = CultureInfo.InvariantCulture;

        Assert.Contains("sint-001", texto, StringComparison.Ordinal);
        Assert.Contains("sint-002", texto, StringComparison.Ordinal);
        Assert.Contains("Taxa do total: " + resultado.TaxaTotal.ToString("0.00", cultura) + "%", texto, StringComparison.Ordinal);
        Assert.Contains("Taxa da data: " + resultado.TaxaData.ToString("0.00", cultura) + "%", texto, StringComparison.Ordinal);
        Assert.Contains("Taxa do preço por item: " + resultado.TaxaPrecoItem.ToString("0.00", cultura) + "%", texto, StringComparison.Ordinal);
        Assert.Equal(50.00m, resultado.TaxaTotal);
        Assert.Equal(100.00m, resultado.TaxaData);
        Assert.Equal(66.67m, resultado.TaxaPrecoItem);
        Assert.Contains("não atingidas", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("Loja Sintetica", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("Fixtures", texto, StringComparison.Ordinal);
        Assert.DoesNotContain(".json", texto, StringComparison.Ordinal);
        AssertSemSegredo(texto);
    }

    [Fact]
    public async Task NaoIncluiChaveImagemNemBase64DoCaso()
    {
        var mensagem = "sk-segredo x-api-key Authorization base64 " + MarcadorFoto;
        var leitor = new LeitorEnvenenado(mensagem);
        var executor = new ExecutorAvaliacaoLeitura(leitor);
        var foto = FotoComprovantePronta.ComJpeg(Encoding.UTF8.GetBytes(MarcadorFoto));
        var gabarito = new GabaritoComprovante(
            "sint-001",
            new DateOnly(2026, 10, 18),
            10m,
            [new GabaritoItem("Arroz", 10m)]);

        var resultado = await executor.ExecutarAsync([(foto, gabarito)], MetasLeituraComprovante.Padrao);
        var texto = AvaliadorLeituraComprovante.Relatorio(resultado);

        Assert.Contains("sint-001", texto, StringComparison.Ordinal);
        Assert.Contains("Taxa do total:", texto, StringComparison.Ordinal);
        Assert.Contains("Taxa da data:", texto, StringComparison.Ordinal);
        Assert.Contains("Taxa do preço por item:", texto, StringComparison.Ordinal);
        Assert.DoesNotContain(MarcadorFoto, texto, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToBase64String(foto.Jpeg!), texto, StringComparison.Ordinal);
        Assert.DoesNotContain(mensagem, texto, StringComparison.Ordinal);
        AssertSemSegredo(texto);
        Assert.Equal("AvaliacaoComprovante", resultado.Casos[0].ToString());
        Assert.Equal("ResultadoAvaliacaoLeitura", resultado.ToString());
    }

    private static void AssertSemSegredo(string texto)
    {
        Assert.DoesNotContain("sk-", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("x-api-key", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("base64", texto, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class LeitorEnvenenado(string mensagem) : ILeitorComprovante
    {
        public Task<ResultadoMensagem> LerAsync(FotoComprovantePronta foto, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(ResultadoMensagem.Falha(TipoResultadoMensagem.Timeout, mensagem));
        }
    }
}
