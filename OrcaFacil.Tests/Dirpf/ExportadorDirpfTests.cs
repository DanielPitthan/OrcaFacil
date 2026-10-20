using ClosedXML.Excel;
using OrcaFacil.Core.Dirpf;
using OrcaFacil.Core.Planilha;

namespace OrcaFacil.Tests.Dirpf;

public class ExportadorDirpfTests : IDisposable
{
    private readonly TestDbContextFactory _fabrica = new();

    public void Dispose() => _fabrica.Dispose();

    [Fact]
    public void Exportar_GravaDuasAbas_ComCodigoTextoCnpjVazioELarguraFixa()
    {
        using (var contexto = _fabrica.CreateDbContext())
        {
            var ana = new OrcaFacil.Core.Entities.MembroFamilia { Nome = "Ana", Apelido = "Nica", Avatar = "A" };
            var petr = new OrcaFacil.Core.Entities.AtivoFinanceiro { Ticker = "PETR4", Nome = "Petrobras", Tipo = OrcaFacil.Core.Enums.TipoAtivo.Acao };
            var fii = new OrcaFacil.Core.Entities.AtivoFinanceiro { Ticker = "MXRF11", Nome = "Maxi Renda", Tipo = OrcaFacil.Core.Enums.TipoAtivo.Fii };
            contexto.AddRange(ana, petr, fii);
            contexto.SaveChanges();
            contexto.OperacoesAtivo.Add(new OrcaFacil.Core.Entities.OperacaoAtivo
            {
                MembroFamiliaId = ana.Id,
                AtivoId = petr.Id,
                Tipo = OrcaFacil.Core.Enums.TipoOperacao.Compra,
                Data = new DateTime(2024, 1, 10),
                Quantidade = 10,
                PrecoUnitario = 1,
                ValorOperacao = 100m
            });
            contexto.Proventos.AddRange(
                new OrcaFacil.Core.Entities.Provento
                {
                    MembroFamiliaId = ana.Id,
                    AtivoId = petr.Id,
                    Tipo = OrcaFacil.Core.Enums.TipoProvento.Rendimento,
                    DataPagamento = new DateTime(2024, 12, 31),
                    ValorLiquido = 10m
                },
                new OrcaFacil.Core.Entities.Provento
                {
                    MembroFamiliaId = ana.Id,
                    AtivoId = fii.Id,
                    Tipo = OrcaFacil.Core.Enums.TipoProvento.Rendimento,
                    DataPagamento = new DateTime(2024, 6, 1),
                    ValorLiquido = 4m
                },
                new OrcaFacil.Core.Entities.Provento
                {
                    MembroFamiliaId = ana.Id,
                    AtivoId = petr.Id,
                    Tipo = OrcaFacil.Core.Enums.TipoProvento.Jcp,
                    DataPagamento = new DateTime(2024, 7, 1),
                    ValorLiquido = 80m
                });
            contexto.SaveChanges();
        }

        using var destino = new MemoryStream();
        using (var contexto = _fabrica.CreateDbContext())
            ExportadorDirpf.Exportar(contexto, 2024, destino);

        using var pasta = new XLWorkbook(destino);
        Assert.Equal(ExportadorDirpf.NomesDeAba, pasta.Worksheets.Select(aba => aba.Name).ToArray());
        Assert.Equal(2, pasta.Worksheets.Count);
        Assert.DoesNotContain("AdjustToContents", File.ReadAllText(Fonte()), StringComparison.Ordinal);

        var bens = pasta.Worksheet(ExportadorDirpf.AbaBens);
        Assert.Equal(PlanilhaPtBr.LarguraColuna, bens.Column(1).Width);
        Assert.Equal("03", bens.Cell(2, 6).GetText());
        Assert.Equal("01", bens.Cell(2, 7).GetText());
        Assert.True(bens.Cell(2, 9).IsEmpty());
        Assert.Equal(100m, (decimal)bens.Cell(2, 11).GetDouble());
        Assert.Equal(2, bens.LastRowUsed()!.RowNumber());

        var isentos = pasta.Worksheet(ExportadorDirpf.AbaRendimentosIsentos);
        Assert.Equal(PlanilhaPtBr.LarguraColuna, isentos.Column(1).Width);
        Assert.Equal("Rendimento de FII", isentos.Cell(2, 5).GetText());
        Assert.Equal("99", isentos.Cell(2, 6).GetText());
        Assert.True(isentos.Cell(2, 8).IsEmpty());
        Assert.Equal(4m, (decimal)isentos.Cell(2, 9).GetDouble());
        Assert.Equal("Dividendo", isentos.Cell(3, 5).GetText());
        Assert.Equal("09", isentos.Cell(3, 6).GetText());
        Assert.Equal(10m, (decimal)isentos.Cell(3, 9).GetDouble());
        Assert.Equal(3, isentos.LastRowUsed()!.RowNumber());
    }

    private static string Fonte()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null && !File.Exists(Path.Combine(diretorio.FullName, "OrcaFacil.slnx")))
            diretorio = diretorio.Parent;

        Assert.NotNull(diretorio);
        return Path.Combine(diretorio!.FullName, "OrcaFacil.Core", "Dirpf", "ExportadorDirpf.cs");
    }
}
