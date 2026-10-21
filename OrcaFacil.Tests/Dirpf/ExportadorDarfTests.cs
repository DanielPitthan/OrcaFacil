using ClosedXML.Excel;
using OrcaFacil.Core.Dirpf;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Exportacao;
using OrcaFacil.Core.Planilha;

namespace OrcaFacil.Tests.Dirpf;

public class ExportadorDarfTests : IDisposable
{
    private readonly TestDbContextFactory _fabrica = new();

    public void Dispose() => _fabrica.Dispose();

    [Fact]
    public void Exportar_GravaApuracaoEDarf_ComCodigoLarguraFixaEVencimento()
    {
        using (var contexto = _fabrica.CreateDbContext())
        {
            var ana = new MembroFamilia { Nome = "Ana", Apelido = "Nica", Avatar = "A" };
            var petr = new AtivoFinanceiro { Ticker = "PETR4", Nome = "Petrobras", Tipo = TipoAtivo.Acao };
            contexto.AddRange(ana, petr);
            contexto.SaveChanges();
            contexto.OperacoesAtivo.AddRange(
                new OperacaoAtivo
                {
                    MembroFamiliaId = ana.Id,
                    AtivoId = petr.Id,
                    Tipo = TipoOperacao.Compra,
                    Data = new DateTime(2024, 1, 10),
                    Quantidade = 100,
                    PrecoUnitario = 100,
                    ValorOperacao = 10_000m
                },
                new OperacaoAtivo
                {
                    MembroFamiliaId = ana.Id,
                    AtivoId = petr.Id,
                    Tipo = TipoOperacao.Venda,
                    Data = new DateTime(2024, 1, 31),
                    Quantidade = 100,
                    PrecoUnitario = 250,
                    ValorOperacao = 25_000m
                });
            contexto.SaveChanges();
        }

        using var destino = new MemoryStream();
        using (var contexto = _fabrica.CreateDbContext())
            ExportadorDarf.Exportar(contexto, 2024, destino);

        using var pasta = new XLWorkbook(destino);
        Assert.Equal(ExportadorDarf.NomesDeAba, pasta.Worksheets.Select(aba => aba.Name).ToArray());
        Assert.DoesNotContain("AdjustToContents", File.ReadAllText(Fonte()), StringComparison.Ordinal);

        var apuracao = pasta.Worksheet(ExportadorDarf.AbaApuracao);
        Assert.Equal(PlanilhaPtBr.LarguraColuna, apuracao.Column(1).Width);
        var comum = apuracao.RowsUsed().Single(linha => linha.RowNumber() > 1 && linha.Cell(4).GetText() == "Operações comuns" && linha.Cell(2).GetValue<int>() == 1);
        Assert.Equal(2_250d, comum.Cell(10).GetDouble());
        Assert.Equal("Ana (Nica)", comum.Cell(3).GetText());

        var darf = pasta.Worksheet(ExportadorDarf.AbaDarfs);
        Assert.Equal(PlanilhaPtBr.LarguraColuna, darf.Column(1).Width);
        var janeiro = darf.RowsUsed().Single(linha => linha.RowNumber() > 1 && linha.Cell(2).GetValue<int>() == 1);
        Assert.Equal(6015d, janeiro.Cell(4).GetDouble());
        Assert.Equal(2_250d, janeiro.Cell(8).GetDouble());
        Assert.Equal(new DateTime(2024, 2, 29), janeiro.Cell(9).GetDateTime().Date);
        Assert.Equal("Valor a levar para a declaração", apuracao.Cell(1, 15).GetText());
        Assert.Equal(0d, comum.Cell(11).GetDouble());
        Assert.Equal(0d, comum.Cell(15).GetDouble());
    }

    [Fact]
    public void Exportar_AbateIrrfManual_EListaOSaldoDeDezembroNaDeclaracao()
    {
        using (var contexto = _fabrica.CreateDbContext())
        {
            var ana = new MembroFamilia { Nome = "Ana", Apelido = "Nica", Avatar = "A" };
            var petr = new AtivoFinanceiro { Ticker = "PETR4", Nome = "Petrobras", Tipo = TipoAtivo.Acao };
            var bova = new AtivoFinanceiro { Ticker = "BOVA11", Nome = "Ibovespa", Tipo = TipoAtivo.Etf };
            contexto.AddRange(ana, petr, bova);
            contexto.SaveChanges();
            contexto.OperacoesAtivo.AddRange(
                new OperacaoAtivo
                {
                    MembroFamiliaId = ana.Id,
                    AtivoId = petr.Id,
                    Tipo = TipoOperacao.Compra,
                    Data = new DateTime(2024, 6, 3),
                    Quantidade = 100,
                    PrecoUnitario = 100,
                    ValorOperacao = 10_000m
                },
                new OperacaoAtivo
                {
                    MembroFamiliaId = ana.Id,
                    AtivoId = petr.Id,
                    Tipo = TipoOperacao.Venda,
                    Data = new DateTime(2024, 6, 21),
                    Quantidade = 100,
                    PrecoUnitario = 250,
                    ValorOperacao = 25_000m
                },
                new OperacaoAtivo
                {
                    MembroFamiliaId = ana.Id,
                    AtivoId = bova.Id,
                    Tipo = TipoOperacao.Compra,
                    Data = new DateTime(2024, 1, 8),
                    Quantidade = 1,
                    PrecoUnitario = 100,
                    ValorOperacao = 100m
                },
                new OperacaoAtivo
                {
                    MembroFamiliaId = ana.Id,
                    AtivoId = bova.Id,
                    Tipo = TipoOperacao.Venda,
                    Data = new DateTime(2024, 1, 22),
                    Quantidade = 1,
                    PrecoUnitario = 200,
                    ValorOperacao = 200m
                });
            contexto.SaveChanges();
        }

        using var destino = new MemoryStream();
        using (var contexto = _fabrica.CreateDbContext())
        {
            ExportadorDarf.Exportar(
                contexto,
                2024,
                destino,
                [
                    new IrrfManual(1, 2024, 6, CestaApuracaoIr.Comum, 3m),
                    new IrrfManual(1, 2024, 1, CestaApuracaoIr.Comum, 40m)
                ]);
        }

        using var pasta = new XLWorkbook(destino);
        var apuracao = pasta.Worksheet(ExportadorDarf.AbaApuracao);
        var junho = apuracao.RowsUsed().Single(linha =>
            linha.RowNumber() > 1 && linha.Cell(2).GetValue<int>() == 6 && linha.Cell(4).GetText() == "Operações comuns");
        Assert.Equal(3d, junho.Cell(11).GetDouble());
        Assert.Equal(2_247d, junho.Cell(12).GetDouble());

        var dezembro = apuracao.RowsUsed().Single(linha =>
            linha.RowNumber() > 1 && linha.Cell(2).GetValue<int>() == 12 && linha.Cell(4).GetText() == "Operações comuns");
        Assert.Equal(25d, dezembro.Cell(15).GetDouble());

        var darf = pasta.Worksheet(ExportadorDarf.AbaDarfs);
        var darfJunho = darf.RowsUsed().Single(linha => linha.RowNumber() > 1 && linha.Cell(2).GetValue<int>() == 6);
        Assert.Equal(2_247d, darfJunho.Cell(8).GetDouble());
    }

    [Fact]
    public void Exportar_AnoAnteriorA2005_NaoGravaArquivo()
    {
        using var destino = new MemoryStream();
        using var contexto = _fabrica.CreateDbContext();
        var erro = Assert.Throws<InvalidOperationException>(() => ExportadorDarf.Exportar(contexto, 2004, destino));

        Assert.Equal("A apuração mensal começa em janeiro de 2005.", erro.Message);
        Assert.Equal(ExportadorDarf.MensagemAntesDe2005, erro.Message);
        Assert.Equal(0, destino.Length);
        Assert.Equal(erro.Message, MensagemExportacaoPlanilha.Para(erro));
    }

    private static string Fonte()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null && !File.Exists(Path.Combine(diretorio.FullName, "OrcaFacil.slnx")))
            diretorio = diretorio.Parent;

        Assert.NotNull(diretorio);
        return Path.Combine(diretorio!.FullName, "OrcaFacil.Core", "Dirpf", "ExportadorDarf.cs");
    }
}
