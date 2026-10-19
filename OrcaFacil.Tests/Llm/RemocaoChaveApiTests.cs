using OrcaFacil.Core.Llm;

namespace OrcaFacil.Tests.Llm;

public class RemocaoChaveApiTests
{
    private const string Segredo = "sk-ant-segredo-que-nao-pode-vazar";

    [Fact]
    public async Task RemoveDevolveTrueMasAChaveContinua_EFalha()
    {
        var leituras = 0;

        var ok = await RemocaoChaveApi.ConcluirAsync(
            () => Task.FromResult(true),
            () =>
            {
                leituras++;
                return Task.FromResult<string?>(Segredo);
            });

        Assert.False(ok);
        Assert.Equal(1, leituras);
    }

    [Fact]
    public async Task RemoveDevolveFalseEChaveContinua_EFalha()
    {
        var ok = await RemocaoChaveApi.ConcluirAsync(
            () => Task.FromResult(false),
            () => Task.FromResult<string?>(Segredo));

        Assert.False(ok);
    }

    [Fact]
    public async Task RemoveDevolveFalseELeituraVazia_ConfirmaAusencia()
    {
        var ok = await RemocaoChaveApi.ConcluirAsync(
            () => Task.FromResult(false),
            () => Task.FromResult<string?>(null));

        Assert.True(ok);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task LeituraVaziaDepoisDeRemover_ESucesso(string? restante)
    {
        var ok = await RemocaoChaveApi.ConcluirAsync(
            () => Task.FromResult(true),
            () => Task.FromResult(restante));

        Assert.True(ok);
    }

    [Fact]
    public async Task RemoverLancaComAChave_NaoVaza_ESeAChaveFicouEFalha()
    {
        var ok = await RemocaoChaveApi.ConcluirAsync(
            () => throw new InvalidOperationException("keystore " + Segredo),
            () => Task.FromResult<string?>(Segredo));

        Assert.False(ok);
    }

    [Fact]
    public async Task LeituraLancaComAChave_NaoApagaDeNovo_ENaoVaza()
    {
        var remocoes = 0;

        var ok = await RemocaoChaveApi.ConcluirAsync(
            () =>
            {
                remocoes++;
                return Task.FromResult(true);
            },
            () => throw new IOException("não leu " + Segredo));

        Assert.False(ok);
        Assert.Equal(1, remocoes);
    }

    [Fact]
    public async Task SegundaTentativa_ApagaQuandoALeituraFicaVazia()
    {
        var guardada = Segredo;

        var primeira = await RemocaoChaveApi.ConcluirAsync(
            () => Task.FromResult(true),
            () => Task.FromResult<string?>(guardada));

        guardada = null;
        var segunda = await RemocaoChaveApi.ConcluirAsync(
            () => Task.FromResult(true),
            () => Task.FromResult(guardada));

        Assert.False(primeira);
        Assert.True(segunda);
    }
}
