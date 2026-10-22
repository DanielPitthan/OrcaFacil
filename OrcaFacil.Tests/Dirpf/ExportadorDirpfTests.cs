using ClosedXML.Excel;
using OrcaFacil.Core.Dirpf;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Planilha;

namespace OrcaFacil.Tests.Dirpf;

public class ExportadorDirpfTests : IDisposable
{
    private readonly TestDbContextFactory _fabrica = new();

    public void Dispose() => _fabrica.Dispose();

    [Fact]
    public void Exportar_GravaAsAbas_ComCodigoTextoCnpjVazioELarguraFixa()
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
        Assert.Equal(4, pasta.Worksheets.Count);
        Assert.DoesNotContain("AdjustToContents", File.ReadAllText(Fonte()), StringComparison.Ordinal);

        var bens = pasta.Worksheet(ExportadorDirpf.AbaBens);
        Assert.Equal(PlanilhaPtBr.LarguraColuna, bens.Column(1).Width);
        Assert.Equal("03", bens.Cell(2, 6).GetText());
        Assert.Equal("01", bens.Cell(2, 7).GetText());
        Assert.True(bens.Cell(2, 9).IsEmpty());
        Assert.Equal(10m, (decimal)bens.Cell(2, 11).GetDouble());
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
        Assert.DoesNotContain(
            Enumerable.Range(2, isentos.LastRowUsed()!.RowNumber() - 1).Select(linha => isentos.Cell(linha, 5).GetText()),
            texto => texto == "JCP");

        var exclusivos = pasta.Worksheet(ExportadorDirpf.AbaRendimentosExclusivos);
        Assert.Equal(PlanilhaPtBr.LarguraColuna, exclusivos.Column(1).Width);
        Assert.Equal("JCP", exclusivos.Cell(2, 5).GetText());
        Assert.Equal("10", exclusivos.Cell(2, 6).GetText());
        Assert.Equal(MontadorDirpf.AvisoCnpjFontePagadora, exclusivos.Cell(2, 8).GetText());
        Assert.Equal(80m, (decimal)exclusivos.Cell(2, 9).GetDouble());
        Assert.True(exclusivos.Cell(2, 10).IsEmpty());
        Assert.Equal(2, exclusivos.LastRowUsed()!.RowNumber());

        var rendaFixa = pasta.Worksheet(ExportadorDirpf.AbaRendaFixa);
        Assert.Equal(PlanilhaPtBr.LarguraColuna, rendaFixa.Column(1).Width);
        Assert.Equal(1, rendaFixa.LastRowUsed()!.RowNumber());
    }

    [Fact]
    public void JcpPagoPorEtf_SaiSoNaAbaDeExclusivos_NoAnoDoPagamento()
    {
        using (var contexto = _fabrica.CreateDbContext())
        {
            var ana = new MembroFamilia { Nome = "Ana", Avatar = "A" };
            var etf = new AtivoFinanceiro { Ticker = "BOVA11", Nome = "iShares Ibovespa", Tipo = TipoAtivo.Etf };
            contexto.AddRange(ana, etf);
            contexto.SaveChanges();
            contexto.Proventos.AddRange(
                new Provento
                {
                    MembroFamiliaId = ana.Id,
                    AtivoId = etf.Id,
                    Tipo = TipoProvento.Jcp,
                    DataPagamento = new DateTime(2024, 12, 31),
                    ValorLiquido = 12.345m
                },
                new Provento
                {
                    MembroFamiliaId = ana.Id,
                    AtivoId = etf.Id,
                    Tipo = TipoProvento.Jcp,
                    DataPagamento = new DateTime(2025, 1, 1),
                    ValorLiquido = 4m
                },
                new Provento
                {
                    MembroFamiliaId = ana.Id,
                    AtivoId = etf.Id,
                    Tipo = TipoProvento.Rendimento,
                    DataPagamento = new DateTime(2024, 6, 1),
                    ValorLiquido = 50m
                });
            contexto.SaveChanges();
        }

        using var pasta = Abrir(2024);
        Assert.Equal(ExportadorDirpf.NomesDeAba, pasta.Worksheets.Select(aba => aba.Name).ToArray());

        var exclusivos = pasta.Worksheet(ExportadorDirpf.AbaRendimentosExclusivos);
        Assert.Equal(2, exclusivos.LastRowUsed()!.RowNumber());
        Assert.Equal(2024d, exclusivos.Cell(2, 1).GetDouble());
        Assert.Equal("Ana", exclusivos.Cell(2, 2).GetText());
        Assert.Equal("BOVA11", exclusivos.Cell(2, 3).GetText());
        Assert.Equal("iShares Ibovespa", exclusivos.Cell(2, 4).GetText());
        Assert.Equal("JCP", exclusivos.Cell(2, 5).GetText());
        Assert.Equal("10", exclusivos.Cell(2, 6).GetText());
        Assert.Equal("Juros sobre capital próprio", exclusivos.Cell(2, 7).GetText());
        Assert.Equal(MontadorDirpf.AvisoCnpjFontePagadora, exclusivos.Cell(2, 8).GetText());
        Assert.Equal(12.35m, (decimal)exclusivos.Cell(2, 9).GetDouble());
        Assert.NotEqual(12.34m, (decimal)exclusivos.Cell(2, 9).GetDouble());
        Assert.True(exclusivos.Cell(2, 10).IsEmpty());
        Assert.Equal("JCP — BOVA11 — iShares Ibovespa.", exclusivos.Cell(2, 11).GetText());

        var isentos = pasta.Worksheet(ExportadorDirpf.AbaRendimentosIsentos);
        Assert.Equal(1, isentos.LastRowUsed()!.RowNumber());
        Assert.Equal(1, pasta.Worksheet(ExportadorDirpf.AbaBens).LastRowUsed()!.RowNumber());
        Assert.Equal(1, pasta.Worksheet(ExportadorDirpf.AbaRendaFixa).LastRowUsed()!.RowNumber());

        using var seguinte = Abrir(2025);
        var exclusivosSeguintes = seguinte.Worksheet(ExportadorDirpf.AbaRendimentosExclusivos);
        Assert.Equal(2, exclusivosSeguintes.LastRowUsed()!.RowNumber());
        Assert.Equal(2025d, exclusivosSeguintes.Cell(2, 1).GetDouble());
        Assert.Equal("BOVA11", exclusivosSeguintes.Cell(2, 3).GetText());
        Assert.Equal(4m, (decimal)exclusivosSeguintes.Cell(2, 9).GetDouble());
        Assert.NotEqual(12.35m, (decimal)exclusivosSeguintes.Cell(2, 9).GetDouble());
    }

    [Fact]
    public void CnpjVazio_NasAbasDeExclusivosERendaFixa_SaiComoAviso_EAsOutrasContinuamVazias()
    {
        using (var contexto = _fabrica.CreateDbContext())
        {
            var ana = new MembroFamilia { Nome = "Ana", Avatar = "A" };
            var petr = new AtivoFinanceiro { Ticker = "PETR4", Nome = "Petrobras", Tipo = TipoAtivo.Acao };
            var cdb = new TituloRendaFixa { Codigo = "CDB1", Nome = "CDB do banco", Tipo = TipoTituloRendaFixa.CDB, Corretora = "XP" };
            var lci = new TituloRendaFixa { Codigo = "LCI1", Nome = "Letra", Tipo = TipoTituloRendaFixa.LCI, Corretora = "XP" };
            contexto.AddRange(ana, petr, cdb, lci);
            contexto.SaveChanges();
            contexto.OperacoesAtivo.Add(new OperacaoAtivo
            {
                MembroFamiliaId = ana.Id,
                AtivoId = petr.Id,
                Tipo = TipoOperacao.Compra,
                Data = new DateTime(2024, 1, 10),
                Quantidade = 10,
                PrecoUnitario = 1,
                ValorOperacao = 100m
            });
            contexto.Proventos.AddRange(
                new Provento
                {
                    MembroFamiliaId = ana.Id,
                    AtivoId = petr.Id,
                    Tipo = TipoProvento.Rendimento,
                    DataPagamento = new DateTime(2024, 6, 1),
                    ValorLiquido = 10m
                },
                new Provento
                {
                    MembroFamiliaId = ana.Id,
                    AtivoId = petr.Id,
                    Tipo = TipoProvento.Jcp,
                    DataPagamento = new DateTime(2024, 7, 1),
                    ValorLiquido = 80m
                });
            contexto.OperacoesRendaFixa.AddRange(
                new OperacaoRendaFixa
                {
                    TituloRendaFixaId = cdb.Id,
                    Tipo = TipoOperacaoRendaFixa.Aplicacao,
                    Data = new DateTime(2024, 2, 1),
                    Quantidade = 1,
                    PrecoUnitario = 1,
                    Valor = 200m
                },
                new OperacaoRendaFixa
                {
                    TituloRendaFixaId = lci.Id,
                    Tipo = TipoOperacaoRendaFixa.Aplicacao,
                    Data = new DateTime(2024, 1, 10),
                    Quantidade = 1,
                    PrecoUnitario = 1,
                    Valor = 100m
                },
                new OperacaoRendaFixa
                {
                    TituloRendaFixaId = lci.Id,
                    Tipo = TipoOperacaoRendaFixa.Resgate,
                    Data = new DateTime(2024, 9, 1),
                    Quantidade = 1,
                    PrecoUnitario = 1,
                    Valor = 140m
                });
            contexto.SaveChanges();
        }

        using var pasta = Abrir(2024);
        Assert.Equal(4, pasta.Worksheets.Count);

        var exclusivos = pasta.Worksheet(ExportadorDirpf.AbaRendimentosExclusivos);
        Assert.Equal(2, exclusivos.LastRowUsed()!.RowNumber());
        Assert.Equal("JCP", exclusivos.Cell(2, 5).GetText());
        Assert.Equal(MontadorDirpf.AvisoCnpjFontePagadora, exclusivos.Cell(2, 8).GetText());
        Assert.Equal("JCP — PETR4 — Petrobras.", exclusivos.Cell(2, 11).GetText());

        var rendaFixa = pasta.Worksheet(ExportadorDirpf.AbaRendaFixa);
        Assert.Equal(2, rendaFixa.LastRowUsed()!.RowNumber());
        Assert.Equal("CDB1", rendaFixa.Cell(2, 3).GetText());
        Assert.Equal(MontadorDirpf.AvisoCnpjFontePagadora, rendaFixa.Cell(2, 10).GetText());
        Assert.DoesNotContain(MontadorDirpf.AvisoCnpjFontePagadora, rendaFixa.Cell(2, 16).GetText(), StringComparison.Ordinal);

        var bens = pasta.Worksheet(ExportadorDirpf.AbaBens);
        Assert.Equal(2, bens.LastRowUsed()!.RowNumber());
        Assert.True(bens.Cell(2, 9).IsEmpty());

        var isentos = pasta.Worksheet(ExportadorDirpf.AbaRendimentosIsentos);
        Assert.Equal(3, isentos.LastRowUsed()!.RowNumber());
        Assert.Equal("LCI1", isentos.Cell(2, 3).GetText());
        Assert.Equal("12", isentos.Cell(2, 6).GetText());
        Assert.True(isentos.Cell(2, 8).IsEmpty());
        Assert.Equal("PETR4", isentos.Cell(3, 3).GetText());
        Assert.Equal("Dividendo", isentos.Cell(3, 5).GetText());
        Assert.True(isentos.Cell(3, 8).IsEmpty());
    }

    private XLWorkbook Abrir(int anoBase)
    {
        var destino = new MemoryStream();
        using (var contexto = _fabrica.CreateDbContext())
            ExportadorDirpf.Exportar(contexto, anoBase, destino);
        return new XLWorkbook(destino);
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
