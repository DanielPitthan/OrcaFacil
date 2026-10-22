using OrcaFacil.Core.Llm;
using OrcaFacil.Core.Llm.Avaliacao;

namespace OrcaFacil.Tests.Llm.Avaliacao;

public class ConsolidacaoTests
{
    [Fact]
    public void Padrao_E95_95_90()
    {
        var metas = MetasLeituraComprovante.Padrao;

        Assert.Equal(95m, metas.TotalMinimo);
        Assert.Equal(95m, metas.DataMinima);
        Assert.Equal(90m, metas.PrecoItemMinimo);
        Assert.Equal("MetasLeituraComprovante", metas.ToString());
    }

    [Fact]
    public void DezenoveDeVinteTotais_95_Atinge()
    {
        var resultado = AvaliadorLeituraComprovante.Consolidar(
            Gerar(20, totaisCertos: 19, datasCertas: 20),
            MetasLeituraComprovante.Padrao);

        Assert.Equal(95.00m, resultado.TaxaTotal);
        Assert.Equal(100.00m, resultado.TaxaData);
        Assert.Equal(100.00m, resultado.TaxaPrecoItem);
        Assert.True(resultado.AtingeMetas);
    }

    [Fact]
    public void DezoitoDeVinteTotais_90_NaoAtinge()
    {
        var resultado = AvaliadorLeituraComprovante.Consolidar(
            Gerar(20, totaisCertos: 18, datasCertas: 20),
            MetasLeituraComprovante.Padrao);

        Assert.Equal(90.00m, resultado.TaxaTotal);
        Assert.Equal(100.00m, resultado.TaxaData);
        Assert.False(resultado.AtingeMetas);
    }

    [Fact]
    public void DezenoveDeVinteDatas_95_Atinge()
    {
        var resultado = AvaliadorLeituraComprovante.Consolidar(
            Gerar(20, totaisCertos: 20, datasCertas: 19),
            MetasLeituraComprovante.Padrao);

        Assert.Equal(100.00m, resultado.TaxaTotal);
        Assert.Equal(95.00m, resultado.TaxaData);
        Assert.True(resultado.AtingeMetas);
    }

    [Fact]
    public void DezoitoDeVinteDatas_90_NaoAtinge()
    {
        var resultado = AvaliadorLeituraComprovante.Consolidar(
            Gerar(20, totaisCertos: 20, datasCertas: 18),
            MetasLeituraComprovante.Padrao);

        Assert.Equal(90.00m, resultado.TaxaData);
        Assert.False(resultado.AtingeMetas);
    }

    [Fact]
    public void PrecoNoveDeDez_90_Atinge()
    {
        var casos = new[]
        {
            new AvaliacaoComprovante("sint-001", true, true, true, ItensNoGabarito: 10, PrecosCorretos: 9)
        };

        var resultado = AvaliadorLeituraComprovante.Consolidar(casos, MetasLeituraComprovante.Padrao);

        Assert.Equal(100.00m, resultado.TaxaTotal);
        Assert.Equal(100.00m, resultado.TaxaData);
        Assert.Equal(90.00m, resultado.TaxaPrecoItem);
        Assert.True(resultado.AtingeMetas);
    }

    [Fact]
    public void ZeroCasos_TaxaZeroENaoAtinge()
    {
        var resultado = AvaliadorLeituraComprovante.Consolidar([], MetasLeituraComprovante.Padrao);

        Assert.Empty(resultado.Casos);
        Assert.Equal(0m, resultado.TaxaTotal);
        Assert.Equal(0m, resultado.TaxaData);
        Assert.Equal(0m, resultado.TaxaPrecoItem);
        Assert.False(resultado.AtingeMetas);
    }

    [Fact]
    public void ZeroItens_TaxaDePrecoZeroENaoAtinge()
    {
        var casos = new[]
        {
            new AvaliacaoComprovante("sint-001", true, true, true, ItensNoGabarito: 0, PrecosCorretos: 0)
        };

        var resultado = AvaliadorLeituraComprovante.Consolidar(casos, MetasLeituraComprovante.Padrao);

        Assert.Equal(100.00m, resultado.TaxaTotal);
        Assert.Equal(100.00m, resultado.TaxaData);
        Assert.Equal(0m, resultado.TaxaPrecoItem);
        Assert.False(resultado.AtingeMetas);
    }

    [Fact]
    public void DoisDeTres_ArredondaPara6667()
    {
        var casos = Gerar(3, totaisCertos: 2, datasCertas: 3);

        var resultado = AvaliadorLeituraComprovante.Consolidar(casos, MetasLeituraComprovante.Padrao);

        Assert.Equal(66.67m, resultado.TaxaTotal);
        Assert.Equal(100.00m, resultado.TaxaData);
        Assert.False(resultado.AtingeMetas);
    }

    [Fact]
    public void FalhaDeLeitura_NaoEhDescartada()
    {
        var data = new DateOnly(2026, 10, 18);
        var certo = new GabaritoComprovante("sint-ok", data, 10m, [new GabaritoItem("Arroz", 10m)]);
        var falho = new GabaritoComprovante("sint-falha", data, 10m, [new GabaritoItem("Arroz", 10m)]);
        var casos = new[]
        {
            AvaliadorLeituraComprovante.Avaliar(
                certo,
                ResultadoMensagem.Ok(new ComprovanteLido(null, data, 10m, true, [Item("Arroz", 10m)]))),
            AvaliadorLeituraComprovante.Avaliar(
                falho,
                ResultadoMensagem.Falha(TipoResultadoMensagem.RespostaInvalida, ResultadoMensagem.TextoRespostaInvalida))
        };

        var resultado = AvaliadorLeituraComprovante.Consolidar(casos, MetasLeituraComprovante.Padrao);

        Assert.Equal(2, resultado.Casos.Count);
        Assert.Contains(resultado.Casos, caso => caso.Id == "sint-falha" && !caso.LeituraOk);
        Assert.Equal(50.00m, resultado.TaxaTotal);
        Assert.Equal(50.00m, resultado.TaxaData);
        Assert.Equal(50.00m, resultado.TaxaPrecoItem);
        Assert.Equal(1, resultado.Casos[1].ItensNoGabarito);
        Assert.Equal(0, resultado.Casos[1].PrecosCorretos);
        Assert.False(resultado.AtingeMetas);
    }

    [Fact]
    public void Consolidar_UsaAsMetasRecebidas()
    {
        var casos = new[]
        {
            new AvaliacaoComprovante("sint-001", true, true, true, 1, 1)
        };
        var exigente = new MetasLeituraComprovante(100.01m, 95m, 90m);

        var resultado = AvaliadorLeituraComprovante.Consolidar(casos, exigente);

        Assert.Equal(100m, resultado.TaxaTotal);
        Assert.False(resultado.AtingeMetas);
    }

    private static AvaliacaoComprovante[] Gerar(int total, int totaisCertos, int datasCertas)
    {
        var casos = new AvaliacaoComprovante[total];
        for (var i = 0; i < total; i++)
        {
            casos[i] = new AvaliacaoComprovante(
                $"sint-{i + 1:000}",
                LeituraOk: true,
                TotalCorreto: i < totaisCertos,
                DataCorreta: i < datasCertas,
                ItensNoGabarito: 1,
                PrecosCorretos: 1);
        }

        return casos;
    }

    private static ItemComprovanteLido Item(string descricao, decimal preco) =>
        new(descricao, descricao, true, preco, null, null, null, ConfiancaLeitura.Alta);
}
