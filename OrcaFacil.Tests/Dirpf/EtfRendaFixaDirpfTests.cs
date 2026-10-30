using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.Dirpf;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Tests.Dirpf;

public class EtfRendaFixaDirpfTests
{
    [Fact]
    public void GanhoPositivo_VaiParaExclusiva06_EBem0708_SemReconstituirBruto()
    {
        var ana = Perfil(1, "Ana", "Nica");
        var imab = Ativo(1, "IMAB11", "IMA-B", TipoAtivo.Etf);
        var bova = Ativo(2, "BOVA11", "Ibovespa", TipoAtivo.Etf);
        var operacoes = new[]
        {
            Operacao(1, ana, imab, TipoOperacao.Compra, new DateTime(2024, 2, 1), new DateTime(2024, 2, 1), 10m, 100m, 1000m),
            Operacao(2, ana, imab, TipoOperacao.Venda, new DateTime(2025, 1, 2), new DateTime(2024, 12, 30), 4m, 110m, 440m),
            Operacao(3, ana, imab, TipoOperacao.Venda, new DateTime(2024, 6, 3), new DateTime(2024, 6, 3), 2m, 90m, 180m),
            Operacao(4, ana, bova, TipoOperacao.Compra, new DateTime(2024, 3, 1), new DateTime(2024, 3, 1), 1m, 100m, 100m),
            Operacao(5, ana, bova, TipoOperacao.Venda, new DateTime(2024, 3, 20), new DateTime(2024, 3, 20), 1m, 150m, 150m)
        };

        var em2024 = Montar(2024, operacoes);
        var exclusivo = Assert.Single(em2024.RendimentosExclusivos);
        Assert.Equal("06", exclusivo.Codigo);
        Assert.Equal(CatalogoCodigosDirpf.DescricaoRendimentoRendaFixa, exclusivo.DescricaoCodigo);
        Assert.Equal("Rendimento de ETF de renda fixa", exclusivo.Natureza);
        Assert.Equal("IMAB11", exclusivo.CodigoAtivo);
        Assert.Equal("Ana (Nica)", exclusivo.Perfil);
        Assert.Null(exclusivo.IrRetido);
        Assert.Equal(MontadorDirpf.AvisoCnpjFontePagadora, exclusivo.Cnpj);
        Assert.Equal(2024, exclusivo.AnoBase);
        Assert.Equal(20m, exclusivo.ValorLiquido);
        Assert.DoesNotContain(em2024.RendimentosExclusivos, linha => linha.CodigoAtivo == "BOVA11");

        var bem = Assert.Single(em2024.Bens, linha => linha.Ticker == "IMAB11");
        Assert.Equal("07", bem.Grupo);
        Assert.Equal("08", bem.Codigo);
        Assert.Equal(CatalogoCodigosDirpf.DescricaoBemEtfRendaFixa, bem.DescricaoCodigo);
        Assert.Equal(8m, bem.Quantidade);

        var em2025 = Montar(2025, operacoes);
        Assert.DoesNotContain(em2025.RendimentosExclusivos, linha => linha.CodigoAtivo == "IMAB11");
    }

    [Fact]
    public void PrejuizoEAcaoAindaNaoReclassificada_FicamForaDaExclusiva()
    {
        var ana = Perfil(1, "Ana", null);
        var prejuizo = Ativo(1, "IRFM11", "IRF-M", TipoAtivo.Etf);
        var aindaAcao = Ativo(2, "LFTB11", "LFT", TipoAtivo.Acao);
        var operacoes = new[]
        {
            Operacao(1, ana, prejuizo, TipoOperacao.Compra, new DateTime(2024, 1, 2), new DateTime(2024, 1, 2), 1m, 100m, 100m),
            Operacao(2, ana, prejuizo, TipoOperacao.Venda, new DateTime(2024, 2, 2), new DateTime(2024, 2, 2), 1m, 80m, 80m),
            Operacao(3, ana, aindaAcao, TipoOperacao.Compra, new DateTime(2024, 1, 2), new DateTime(2024, 1, 2), 2m, 100m, 200m),
            Operacao(4, ana, aindaAcao, TipoOperacao.Venda, new DateTime(2024, 2, 2), new DateTime(2024, 2, 2), 1m, 130m, 130m)
        };

        var dirpf = Montar(2024, operacoes);
        Assert.Empty(dirpf.RendimentosExclusivos);
        Assert.DoesNotContain(dirpf.Bens, linha => linha.Ticker == "IRFM11");
        var bem = Assert.Single(dirpf.Bens);
        Assert.Equal("LFTB11", bem.Ticker);
        Assert.Equal("03", bem.Grupo);
        Assert.Equal("01", bem.Codigo);
    }

    [Fact]
    public void Mensagem_CabeEm300_EListaOMesQuandoCouber()
    {
        var curta = AvisoDarfComplementar.Mensagem([(2024, 3), (2024, 11)]);
        Assert.StartsWith(AvisoDarfComplementar.Prefixo, curta);
        Assert.Contains("03/2024", curta);
        Assert.Contains("11/2024", curta);
        Assert.Contains("DARF complementar", curta);
        Assert.True(curta.Length <= 300);

        var muitos = Enumerable.Range(0, 40).Select(indice => (Ano: 2000 + indice, Mes: 12)).ToList();
        var longa = AvisoDarfComplementar.Mensagem(muitos);
        Assert.Equal(AvisoDarfComplementar.Prefixo, longa);
        Assert.True(longa.Length <= 300);
    }

    private static DirpfAnoBase Montar(int ano, IEnumerable<OperacaoAtivo> operacoes)
        => MontadorDirpf.Montar(
            ano,
            operacoes,
            Array.Empty<Provento>(),
            CatalogoCodigosDirpf.Linhas(),
            CatalogoRegrasFiscais.Linhas());

    private static MembroFamilia Perfil(int id, string nome, string? apelido)
        => new() { Id = id, Nome = nome, Apelido = apelido };

    private static AtivoFinanceiro Ativo(int id, string ticker, string nome, TipoAtivo tipo)
        => new() { Id = id, Ticker = ticker, Nome = nome, Tipo = tipo };

    private static OperacaoAtivo Operacao(
        int id,
        MembroFamilia membro,
        AtivoFinanceiro ativo,
        TipoOperacao tipo,
        DateTime data,
        DateTime pregao,
        decimal quantidade,
        decimal preco,
        decimal valor) => new()
    {
        Id = id,
        MembroFamiliaId = membro.Id,
        MembroFamilia = membro,
        AtivoId = ativo.Id,
        Ativo = ativo,
        Tipo = tipo,
        Data = data,
        DataPregao = pregao,
        Quantidade = quantidade,
        PrecoUnitario = preco,
        ValorOperacao = valor
    };
}
