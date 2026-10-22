using OrcaFacil.Core.Armazenamento.Espelho.Pasta;

namespace OrcaFacil.Tests.Armazenamento;

public class DestinoEspelhoPastaTests
{
    [Fact]
    public async Task PublicacaoEspelhoException_ComResultadoNulo_PreservaPromocaoEfetuada()
    {
        var volume = new VolumeMemoria { ExcecaoNoDescarteReserva = new InvalidOperationException("descarte") };
        volume.Arquivos[volume.NomeArquivo] = [1];
        var destino = new DestinoEspelhoPasta(new ProtocoloPublicacaoEspelho(volume));

        var resultado = await destino.PublicarComResultadoAsync(Arquivo([2, 2]));

        Assert.False(resultado.Sucesso);
        Assert.True(resultado.PromocaoEfetuada);
        Assert.Equal(MensagensPastaEspelho.Para(resultado.Codigo), resultado.Mensagem);

        var excecao = await Assert.ThrowsAsync<PublicacaoEspelhoException>(() => destino.PublicarAsync(Arquivo([3, 3])));
        Assert.True(excecao.PromocaoEfetuada);
        Assert.DoesNotContain("descarte", excecao.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublicacaoEspelhoException_SemPromocao_NaoLigaAMarca()
    {
        var volume = new VolumeMemoria { ExcecaoNaPromocao = new InvalidOperationException("rename") };
        volume.Arquivos[volume.NomeArquivo] = [4, 4];
        var destino = new DestinoEspelhoPasta(new ProtocoloPublicacaoEspelho(volume));

        var resultado = await destino.PublicarComResultadoAsync(Arquivo([5]));

        Assert.False(resultado.PromocaoEfetuada);
        Assert.Equal(new byte[] { 4, 4 }, volume.Arquivos[volume.NomeArquivo]);

        var excecao = await Assert.ThrowsAsync<IOException>(() => destino.PublicarAsync(Arquivo([6])));
        Assert.IsNotType<PublicacaoEspelhoException>(excecao);
    }

    private static string Arquivo(byte[] bytes)
    {
        var caminho = Path.Combine(Path.GetTempPath(), "orcafacil-destino-" + Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(caminho, bytes);
        return caminho;
    }
}
