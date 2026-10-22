using OrcaFacil.Core.Llm;
using OrcaFacil.Core.Llm.Avaliacao;

namespace OrcaFacil.Tests.Llm.Avaliacao;

public class AvaliadorLeituraComprovanteTests
{
    private static readonly DateOnly Data = new(2026, 10, 18);

    [Fact]
    public void LeituraPerfeita_TudoCerto()
    {
        var gabarito = Gabarito(12.90m, ("Arroz", 10.00m), ("Feijão", 2.90m));

        var avaliacao = AvaliadorLeituraComprovante.Avaliar(
            gabarito,
            Lido(12.90m, Data, true, ("Arroz", 10.00m), ("Feijão", 2.90m)));

        Assert.Equal("sint-001", avaliacao.Id);
        Assert.True(avaliacao.LeituraOk);
        Assert.True(avaliacao.TotalCorreto);
        Assert.True(avaliacao.DataCorreta);
        Assert.Equal(2, avaliacao.ItensNoGabarito);
        Assert.Equal(2, avaliacao.PrecosCorretos);
    }

    [Fact]
    public void Total_1290Contra1291_Errado()
    {
        var gabarito = Gabarito(12.90m, ("Arroz", 12.90m));

        var avaliacao = AvaliadorLeituraComprovante.Avaliar(
            gabarito,
            Lido(12.91m, Data, true, ("Arroz", 12.90m)));

        Assert.False(avaliacao.TotalCorreto);
        Assert.True(avaliacao.DataCorreta);
        Assert.Equal(1, avaliacao.PrecosCorretos);
    }

    [Fact]
    public void Total_12905_ArredondaAwayFromZero_Para1291()
    {
        // 12,905 → 12,91 com AwayFromZero. ToEven ficaria em 12,90 e erraria o total.
        Assert.Equal(12.91m, Math.Round(12.905m, 2, MidpointRounding.AwayFromZero));

        var gabarito = Gabarito(12.91m, ("Arroz", 12.91m));
        var avaliacao = AvaliadorLeituraComprovante.Avaliar(
            gabarito,
            Lido(12.905m, Data, true, ("Arroz", 12.905m)));

        Assert.True(avaliacao.TotalCorreto);
        Assert.Equal(1, avaliacao.PrecosCorretos);
    }

    [Fact]
    public void Total_0125_ArredondaAwayFromZero_Para013()
    {
        // 0,125 → 0,13 com AwayFromZero. ToEven ficaria em 0,12 e este teste cairia.
        Assert.Equal(0.13m, Math.Round(0.125m, 2, MidpointRounding.AwayFromZero));

        var gabarito = Gabarito(0.13m, ("Arroz", 0.13m));
        var avaliacao = AvaliadorLeituraComprovante.Avaliar(
            gabarito,
            Lido(0.125m, Data, true, ("Arroz", 0.13m)));

        Assert.True(avaliacao.TotalCorreto);
    }

    [Fact]
    public void PrecoDoItem_0125_ArredondaAwayFromZero_Para013()
    {
        var gabarito = Gabarito(1m, ("Arroz", 0.13m));
        var avaliacao = AvaliadorLeituraComprovante.Avaliar(
            gabarito,
            Lido(1m, Data, true, ("Arroz", 0.125m)));

        Assert.Equal(1, avaliacao.PrecosCorretos);
    }

    [Fact]
    public void TotalNulo_Errado()
    {
        var gabarito = Gabarito(10m, ("Arroz", 10m));
        var avaliacao = AvaliadorLeituraComprovante.Avaliar(
            gabarito,
            Lido(null, Data, true, ("Arroz", 10m)));

        Assert.True(avaliacao.LeituraOk);
        Assert.False(avaliacao.TotalCorreto);
        Assert.True(avaliacao.DataCorreta);
    }

    [Fact]
    public void DataNula_Errada()
    {
        var gabarito = Gabarito(10m, ("Arroz", 10m));
        var avaliacao = AvaliadorLeituraComprovante.Avaliar(
            gabarito,
            Lido(10m, null, true, ("Arroz", 10m)));

        Assert.False(avaliacao.DataCorreta);
        Assert.True(avaliacao.TotalCorreto);
    }

    [Fact]
    public void DataComDiaTrocado_Errada()
    {
        var gabarito = Gabarito(10m, ("Arroz", 10m));
        var avaliacao = AvaliadorLeituraComprovante.Avaliar(
            gabarito,
            Lido(10m, new DateOnly(2026, 10, 19), true, ("Arroz", 10m)));

        Assert.False(avaliacao.DataCorreta);
    }

    [Fact]
    public void ItemFaltando_CasaPelaDescricao()
    {
        var gabarito = Gabarito(15m, ("Arroz", 10m), ("Feijão", 5m));
        var avaliacao = AvaliadorLeituraComprovante.Avaliar(
            gabarito,
            Lido(15m, Data, true, ("Feijão", 5m)));

        Assert.Equal(2, avaliacao.ItensNoGabarito);
        Assert.Equal(1, avaliacao.PrecosCorretos);
    }

    [Fact]
    public void ItemAMais_NaoSomaAcerto()
    {
        var gabarito = Gabarito(10m, ("Arroz", 10m));
        var avaliacao = AvaliadorLeituraComprovante.Avaliar(
            gabarito,
            Lido(11m, Data, true, ("Arroz", 10m), ("Bala", 1m)));

        Assert.Equal(1, avaliacao.ItensNoGabarito);
        Assert.Equal(1, avaliacao.PrecosCorretos);
    }

    [Fact]
    public void OrdemTrocada_CasaPelaDescricao()
    {
        var gabarito = Gabarito(13.50m, ("Açúcar", 3.50m), ("Arroz", 10m));
        var avaliacao = AvaliadorLeituraComprovante.Avaliar(
            gabarito,
            Lido(13.50m, Data, true, ("ARROZ", 10m), ("acucar", 3.50m)));

        Assert.Equal(2, avaliacao.PrecosCorretos);
        Assert.True(avaliacao.TotalCorreto);
        Assert.True(avaliacao.DataCorreta);
    }

    [Fact]
    public void ContagemDiferenteEOrdemTrocada_CasaPelaDescricao()
    {
        var gabarito = Gabarito(14m, ("Arroz", 10m), ("Feijão", 4m));
        var avaliacao = AvaliadorLeituraComprovante.Avaliar(
            gabarito,
            Lido(15m, Data, true, ("Feijão", 4m), ("Arroz", 10m), ("Bala", 1m)));

        Assert.Equal(2, avaliacao.ItensNoGabarito);
        Assert.Equal(2, avaliacao.PrecosCorretos);
    }

    [Fact]
    public void ItemDuplicado_NaoCasaDuasVezes()
    {
        var gabarito = Gabarito(20m, ("Arroz", 10m), ("Arroz", 10m));
        var avaliacao = AvaliadorLeituraComprovante.Avaliar(
            gabarito,
            Lido(10m, Data, true, ("Arroz", 10m)));

        Assert.Equal(2, avaliacao.ItensNoGabarito);
        Assert.Equal(1, avaliacao.PrecosCorretos);
    }

    [Theory]
    [InlineData(TipoResultadoMensagem.Timeout)]
    [InlineData(TipoResultadoMensagem.RespostaInvalida)]
    [InlineData(TipoResultadoMensagem.Falha)]
    [InlineData(TipoResultadoMensagem.SemRede)]
    [InlineData(TipoResultadoMensagem.NaoAutorizado)]
    public void FalhaDeLeitura_ErroNasTresMetricas(TipoResultadoMensagem tipo)
    {
        var gabarito = Gabarito(10m, ("Arroz", 10m), ("Feijão", 4m));
        var avaliacao = AvaliadorLeituraComprovante.Avaliar(
            gabarito,
            ResultadoMensagem.Falha(tipo, "frase fixa"));

        Assert.False(avaliacao.LeituraOk);
        Assert.False(avaliacao.TotalCorreto);
        Assert.False(avaliacao.DataCorreta);
        Assert.Equal(0, avaliacao.PrecosCorretos);
        Assert.Equal(2, avaliacao.ItensNoGabarito);
        Assert.Equal("sint-001", avaliacao.Id);
    }

    [Fact]
    public void FalhaComComprovantePreenchido_NaoPontua()
    {
        var gabarito = Gabarito(10m, ("Arroz", 10m));
        var comprovante = new ComprovanteLido("Loja Sintetica", Data, 10m, true, [Item("Arroz", 10m)]);
        var resultado = new ResultadoMensagem(TipoResultadoMensagem.Timeout, ResultadoMensagem.TextoTimeout, comprovante);

        var avaliacao = AvaliadorLeituraComprovante.Avaliar(gabarito, resultado);

        Assert.False(avaliacao.LeituraOk);
        Assert.False(avaliacao.TotalCorreto);
        Assert.False(avaliacao.DataCorreta);
        Assert.Equal(0, avaliacao.PrecosCorretos);
        Assert.Equal(1, avaliacao.ItensNoGabarito);
    }

    [Fact]
    public void LegivelFalso_ContaComoLido_MetricasPeloConteudo()
    {
        var gabarito = Gabarito(10m, ("Arroz", 10m));
        var avaliacao = AvaliadorLeituraComprovante.Avaliar(
            gabarito,
            Lido(10m, Data, legivel: false, ("Arroz", 10m)));

        Assert.True(avaliacao.LeituraOk);
        Assert.True(avaliacao.TotalCorreto);
        Assert.True(avaliacao.DataCorreta);
        Assert.Equal(1, avaliacao.PrecosCorretos);
    }

    [Fact]
    public void OkSemComprovante_ErroNasTres()
    {
        var gabarito = Gabarito(10m, ("Arroz", 10m));
        var avaliacao = AvaliadorLeituraComprovante.Avaliar(gabarito, ResultadoMensagem.ConexaoOk());

        Assert.False(avaliacao.LeituraOk);
        Assert.False(avaliacao.TotalCorreto);
        Assert.False(avaliacao.DataCorreta);
        Assert.Equal(0, avaliacao.PrecosCorretos);
        Assert.Equal(1, avaliacao.ItensNoGabarito);
    }

    private static GabaritoComprovante Gabarito(decimal total, params (string Descricao, decimal Preco)[] itens) =>
        new(
            "sint-001",
            Data,
            total,
            itens.Select(item => new GabaritoItem(item.Descricao, item.Preco)).ToArray());

    private static ResultadoMensagem Lido(
        decimal? total,
        DateOnly? data,
        bool legivel,
        params (string Descricao, decimal Preco)[] itens) =>
        ResultadoMensagem.Ok(new ComprovanteLido(
            "Loja Sintetica",
            data,
            total,
            legivel,
            itens.Select(item => Item(item.Descricao, item.Preco)).ToArray()));

    private static ItemComprovanteLido Item(string descricao, decimal preco) =>
        new(descricao, descricao, true, preco, null, null, null, ConfiancaLeitura.Alta);
}
