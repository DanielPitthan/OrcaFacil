using System.Globalization;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Lancamentos;

namespace OrcaFacil.Tests.Lancamentos;

public class FaturaCartaoEditorTests
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    [Theory]
    [InlineData(true, 0, StatusFaturaCartao.Paga)]
    [InlineData(false, 0, StatusFaturaCartao.Pendente)]
    [InlineData(false, 1, StatusFaturaCartao.Pendente)]
    [InlineData(false, -1, StatusFaturaCartao.Atrasada)]
    public void Status_ConsideraPagamentoEDia(bool pago, int dias, StatusFaturaCartao esperado)
    {
        var hoje = new DateTime(2026, 10, 16);
        var status = FaturaCartaoEditor.Status(pago, hoje.AddDays(dias).AddHours(18), hoje);

        Assert.Equal(esperado, status);
        Assert.False(string.IsNullOrWhiteSpace(FaturaCartaoEditor.RotuloStatus(status)));
    }

    [Fact]
    public void Resumo_CalculaRestanteSemDeixarExcedenteNegativoNaLegenda()
    {
        Assert.Equal(4230.10m, FaturaCartaoEditor.Restante(4300m, 69.90m));
        Assert.Equal(4230.10m, FaturaCartaoEditor.ADetalhar(4300m, 69.90m));
        Assert.Equal(0m, FaturaCartaoEditor.ADetalhar(100m, 140m));
        Assert.False(FaturaCartaoEditor.Estoura(1, 69.90m, 4300m));
        Assert.True(FaturaCartaoEditor.Estoura(1, 140m, 100m));
        Assert.False(FaturaCartaoEditor.Estoura(0, 0m, 0m));
        Assert.Equal(100d, FaturaCartaoEditor.PercentualClassificado(0m, 10m));
        Assert.InRange(FaturaCartaoEditor.PercentualClassificado(4300m, 69.90m), 1d, 2d);
    }

    [Fact]
    public void PartirMoeda_SeparaSimboloENumeroEmPtBr()
    {
        var (simbolo, numero) = FaturaCartaoEditor.PartirMoeda(4300m, PtBr);

        Assert.Equal("R$", simbolo);
        Assert.Equal("4.300,00", numero);
    }

    [Fact]
    public void Subtitulo_UsaCategoriaENaturezaMesmoSemDescritivo()
    {
        Assert.Equal("Alimentação · Avulsa", FaturaCartaoEditor.Subtitulo("Alimentação", NaturezaItemLancamento.Avulsa, null, null));
        Assert.Equal("Lazer · Assinatura", FaturaCartaoEditor.Subtitulo(" Lazer ", NaturezaItemLancamento.Assinatura, null, null));
        Assert.Equal("Compras · Parcela 2/12", FaturaCartaoEditor.Subtitulo("Compras", NaturezaItemLancamento.Parcelada, 2, 12));
        Assert.Equal("Netflix", ItensFaturaInclusao.Titulo("Netflix", "Lazer"));
        Assert.Equal("Lazer", ItensFaturaInclusao.Titulo(null, "Lazer"));
    }

    [Fact]
    public void ValidarItem_RecusaValorCategoriaEParcelaIncompletos()
    {
        Assert.Equal("Selecione a despesa.", FaturaCartaoEditor.ValidarItem(0, 10m, NaturezaItemLancamento.Avulsa, null, null));
        Assert.Equal("Informe o valor do item.", FaturaCartaoEditor.ValidarItem(2, 0m, NaturezaItemLancamento.Avulsa, null, null));
        Assert.Contains("parcela", FaturaCartaoEditor.ValidarItem(2, 10m, NaturezaItemLancamento.Parcelada, 1, null));
        Assert.Null(FaturaCartaoEditor.ValidarItem(2, 10m, NaturezaItemLancamento.Parcelada, 1, 2));
        Assert.Null(FaturaCartaoEditor.ValidarItem(2, 10m, NaturezaItemLancamento.Assinatura, null, null));
    }

    [Fact]
    public void ValidarItem_MagnitudeNaoPositiva_Recusa()
    {
        Assert.Equal("Informe o valor do item.", FaturaCartaoEditor.ValidarItem(2, 0m, NaturezaItemLancamento.Avulsa, null, null));
        Assert.Equal("Informe o valor do item.", FaturaCartaoEditor.ValidarItem(2, -5m, NaturezaItemLancamento.Avulsa, null, null, ehEstorno: true));
    }

    [Fact]
    public void ValidarItem_EstornoSoAvulso()
    {
        Assert.Equal("Estorno só pode ser avulso.", FaturaCartaoEditor.ValidarItem(2, 10m, NaturezaItemLancamento.Parcelada, 1, 2, ehEstorno: true));
        Assert.Equal("Estorno só pode ser avulso.", FaturaCartaoEditor.ValidarItem(2, 10m, NaturezaItemLancamento.Assinatura, null, null, ehEstorno: true));
        Assert.Null(FaturaCartaoEditor.ValidarItem(2, 10m, NaturezaItemLancamento.Avulsa, null, null, ehEstorno: true));
    }

    [Fact]
    public void Soma_AplicaSinalDoEstorno()
    {
        var itens = new List<ItemLancamentoFormModel>
        {
            new() { Valor = 80m },
            new() { Valor = 30m, EhEstorno = true }
        };

        Assert.Equal(50m, FaturaCartaoEditor.Soma(itens));
    }

    [Fact]
    public void ValidarItens_SomaAssinadaAcimaDoTotal_Recusa()
    {
        var itens = new List<ItemLancamentoFormModel>
        {
            new() { Descricao = "Almoço", Valor = 80m, CategoriaId = 2 },
            new() { Descricao = "Devolução", Valor = 10m, EhEstorno = true, CategoriaId = 3 }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            FaturaCartaoEditor.ValidarItens(60m, categoriaFaturaId: 1, permiteItens: true, TipoTransacao.Despesa, itens));

        Assert.Contains("não pode passar", ex.Message);
    }

    [Fact]
    public void ValidarItens_TotalLiquidoAbaixoDoMinimo_Recusa()
    {
        var itens = new List<ItemLancamentoFormModel>
        {
            new() { Descricao = "Almoço", Valor = 10m, CategoriaId = 2 }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            FaturaCartaoEditor.ValidarItens(0.009m, categoriaFaturaId: 1, permiteItens: true, TipoTransacao.Despesa, itens));

        Assert.Contains("0,01", ex.Message);
    }

    [Theory]
    [InlineData(NaturezaItemLancamento.Parcelada)]
    [InlineData(NaturezaItemLancamento.Assinatura)]
    public void ValidarItens_EstornoForaDeAvulso_Recusa(NaturezaItemLancamento natureza)
    {
        var itens = new List<ItemLancamentoFormModel>
        {
            new()
            {
                Descricao = "Devolução",
                Valor = 20m,
                EhEstorno = true,
                Natureza = natureza,
                ParcelaAtual = natureza == NaturezaItemLancamento.Parcelada ? 1 : null,
                TotalParcelas = natureza == NaturezaItemLancamento.Parcelada ? 2 : null,
                CategoriaId = 2
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            FaturaCartaoEditor.ValidarItens(100m, categoriaFaturaId: 1, permiteItens: true, TipoTransacao.Despesa, itens));

        Assert.Contains("avulso", ex.Message);
    }

    [Fact]
    public void Stepper_NaoDesceDeDuasParcelasEPreservaTotalLegado()
    {
        Assert.Equal(2, FaturaCartaoEditor.AjustarTotal(2, -1));
        Assert.Equal(1, FaturaCartaoEditor.AjustarTotal(1, -1));
        Assert.Equal(3, FaturaCartaoEditor.AjustarTotal(2, 1));
        Assert.Equal(1, FaturaCartaoEditor.AjustarParcelaAtual(1, -1, 12));
        Assert.Equal(12, FaturaCartaoEditor.AjustarParcelaAtual(12, 1, 12));

        var (atual, total) = FaturaCartaoEditor.AoEscolherNatureza(NaturezaItemLancamento.Parcelada, null, null);
        Assert.Equal(1, atual);
        Assert.Equal(2, total);

        var limpa = FaturaCartaoEditor.AoEscolherNatureza(NaturezaItemLancamento.Assinatura, 2, 10);
        Assert.Null(limpa.ParcelaAtual);
        Assert.Null(limpa.TotalParcelas);
    }

    [Fact]
    public void EmDestaque_MantemACategoriaSelecionadaForaDaJanela()
    {
        var categorias = Enumerable.Range(1, 8).Select(i => new CategoriaTeste(i)).ToList();

        var destaque = FaturaCartaoEditor.EmDestaque(categorias, c => c.Id == 8);

        Assert.Equal(7, destaque.Count);
        Assert.Contains(destaque, c => c.Id == 8);
        Assert.Equal(6, FaturaCartaoEditor.EmDestaque(categorias, c => c.Id == 2).Count);
    }

    [Fact]
    public void AplicarEdicao_AtualizaAMesmaLinhaEPermiteCategoriaRepetida()
    {
        var itens = new List<ItemLancamentoFormModel>();
        var primeira = ItensFaturaInclusao.Acrescentar(itens, 2, "Alimentação", 40m, NaturezaItemLancamento.Avulsa, null, null, "McDonald's");
        ItensFaturaInclusao.Acrescentar(itens, 2, "Alimentação", 40m, NaturezaItemLancamento.Avulsa, null, null, "Big Steak");

        FaturaCartaoEditor.AplicarEdicao(primeira, 2, "Alimentação", 55m, NaturezaItemLancamento.Parcelada, 1, 2, " McDonald's ");

        Assert.Equal(2, itens.Count);
        Assert.Equal(55m, primeira.Valor);
        Assert.Equal("McDonald's", primeira.Descritivo);
        Assert.Equal(1, primeira.ParcelaAtual);
        Assert.Equal(2, primeira.TotalParcelas);
        Assert.Equal(2, itens.Count(i => i.CategoriaId == 2));
    }

    [Fact]
    public void ClonarComoNova_RemoveIdsENaoCompartilhaAsLinhas()
    {
        var origem = new TransacaoFormModel
        {
            Id = 9,
            Descricao = "Cartão Itaú",
            Valor = 100m,
            Data = new DateTime(2026, 10, 21),
            CategoriaId = 4,
            CartaoId = 3,
            Recorrencia = TipoRecorrencia.Mensal,
            Pago = true,
            Observacao = "obs",
            Itens =
            [
                new ItemLancamentoFormModel
                {
                    Id = 15,
                    Descricao = "Alimentação",
                    Descritivo = "Refeição",
                    Valor = 69.90m,
                    CategoriaId = 2
                }
            ]
        };

        var copia = FaturaCartaoEditor.ClonarComoNova(origem);
        copia.Itens[0].Valor = 10m;

        Assert.Null(copia.Id);
        Assert.Null(copia.Itens[0].Id);
        Assert.Equal(69.90m, origem.Itens[0].Valor);
        Assert.Equal("Cartão Itaú", copia.Descricao);
        Assert.Equal(3, copia.CartaoId);
        Assert.Equal(TipoRecorrencia.Mensal, copia.Recorrencia);
    }

    private sealed record CategoriaTeste(int Id);
}
