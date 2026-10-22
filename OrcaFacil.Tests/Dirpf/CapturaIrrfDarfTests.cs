using ClosedXML.Excel;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Dirpf;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Exportacao;
using OrcaFacil.Core.Repositories;

namespace OrcaFacil.Tests.Dirpf;

public class CapturaIrrfDarfTests : IDisposable
{
    private readonly TestDbContextFactory _fabrica = new();

    public void Dispose() => _fabrica.Dispose();

    [Fact]
    public void Salvar_InsereEAtualizaAMesmaChave_SemDuplicar()
    {
        var ana = Perfil("Ana", "Nica");
        var bruno = Perfil("Bruno", null);
        using (var contexto = _fabrica.CreateDbContext())
        {
            contexto.MembrosFamilia.AddRange(ana, bruno);
            contexto.SaveChanges();
            CapturaIrrfDarf.Salvar(contexto, 2024,
            [
                Campo(ana.Id, 2024, 6, CestaApuracaoIr.Comum, 10m),
                Campo(ana.Id, 2024, 6, CestaApuracaoIr.DayTrade, 1.50m),
                Campo(bruno.Id, 2024, 1, CestaApuracaoIr.Fii, 1.005m)
            ]);
        }

        using (var contexto = _fabrica.CreateDbContext())
        {
            Assert.Equal(3, contexto.IrrfInformados.Count());
            Assert.Equal(10m, Valor(contexto, ana.Id, 6, CestaApuracaoIr.Comum));
            Assert.Equal(1.01m, Valor(contexto, bruno.Id, 1, CestaApuracaoIr.Fii));

            CapturaIrrfDarf.Salvar(contexto, 2024,
            [
                Campo(ana.Id, 2024, 6, CestaApuracaoIr.Comum, 0m),
                Campo(ana.Id, 2024, 6, CestaApuracaoIr.DayTrade, 1.004m)
            ]);
        }

        using var leitura = _fabrica.CreateDbContext();
        Assert.Equal(3, leitura.IrrfInformados.Count());
        Assert.Equal(0m, Valor(leitura, ana.Id, 6, CestaApuracaoIr.Comum));
        Assert.Equal(1.00m, Valor(leitura, ana.Id, 6, CestaApuracaoIr.DayTrade));
        Assert.Equal(1.01m, Valor(leitura, bruno.Id, 1, CestaApuracaoIr.Fii));
        Assert.Equal(1.01m, CapturaIrrfDarf.Normalizar(1.005m));
        Assert.Equal(1.00m, CapturaIrrfDarf.Normalizar(1.004m));
    }

    [Fact]
    public void Salvar_RecusaNegativoDuplicadoAnoErradoEPerfilInexistente()
    {
        var ana = Perfil("Ana", null);
        using (var contexto = _fabrica.CreateDbContext())
        {
            contexto.MembrosFamilia.Add(ana);
            contexto.SaveChanges();

            var negativo = Assert.Throws<InvalidOperationException>(() =>
                CapturaIrrfDarf.Salvar(contexto, 2024, [Campo(ana.Id, 2024, 6, CestaApuracaoIr.Comum, -0.01m)]));
            Assert.Equal("O IRRF informado não pode ser negativo.", negativo.Message);

            var duplicado = Assert.Throws<InvalidOperationException>(() =>
                CapturaIrrfDarf.Salvar(contexto, 2024,
                [
                    Campo(ana.Id, 2024, 6, CestaApuracaoIr.Comum, 1m),
                    Campo(ana.Id, 2024, 6, CestaApuracaoIr.Comum, 2m)
                ]));
            Assert.Equal("Há dois IRRF informados para o mesmo mês e a mesma cesta.", duplicado.Message);

            var outroAno = Assert.Throws<InvalidOperationException>(() =>
                CapturaIrrfDarf.Salvar(contexto, 2024, [Campo(ana.Id, 2023, 6, CestaApuracaoIr.Comum, 1m)]));
            Assert.Equal("O IRRF informado é de outro ano-base.", outroAno.Message);

            var semPerfil = Assert.Throws<InvalidOperationException>(() =>
                CapturaIrrfDarf.Salvar(contexto, 2024, [Campo(ana.Id + 9, 2024, 6, CestaApuracaoIr.Comum, 1m)]));
            Assert.Equal("O perfil informado não existe.", semPerfil.Message);

            Assert.Empty(contexto.IrrfInformados);
        }
    }

    [Fact]
    public void Montar_PreencheOQueJaFoiSalvo_SoNosMesesComVenda()
    {
        var ana = Perfil("Ana", "Nica");
        var bruno = Perfil("Bruno", null);
        var petr = new AtivoFinanceiro { Ticker = "PETR4", Nome = "Petrobras", Tipo = TipoAtivo.Acao };
        using (var contexto = _fabrica.CreateDbContext())
        {
            contexto.AddRange(ana, bruno, petr);
            contexto.SaveChanges();
            contexto.OperacoesAtivo.AddRange(
                Operacao(ana.Id, petr.Id, TipoOperacao.Compra, new DateTime(2024, 3, 4), 10m),
                Operacao(ana.Id, petr.Id, TipoOperacao.Venda, new DateTime(2024, 6, 21), 10m),
                Operacao(ana.Id, petr.Id, TipoOperacao.Venda, new DateTime(2024, 6, 28), 5m),
                Operacao(ana.Id, petr.Id, TipoOperacao.Compra, new DateTime(2024, 8, 2), 1m),
                Operacao(ana.Id, petr.Id, TipoOperacao.Venda, new DateTime(2023, 12, 20), 1m),
                Operacao(ana.Id, petr.Id, TipoOperacao.Venda, new DateTime(2025, 1, 2), 1m),
                Operacao(bruno.Id, petr.Id, TipoOperacao.Venda, new DateTime(2024, 1, 15), 1m));
            contexto.IrrfInformados.AddRange(
                new IrrfInformado { MembroFamiliaId = ana.Id, Ano = 2024, Mes = 6, Cesta = CestaApuracaoIr.Comum, Valor = 12.34m },
                new IrrfInformado { MembroFamiliaId = ana.Id, Ano = 2024, Mes = 6, Cesta = CestaApuracaoIr.Fii, Valor = 0m },
                new IrrfInformado { MembroFamiliaId = ana.Id, Ano = 2024, Mes = 3, Cesta = CestaApuracaoIr.Comum, Valor = 9m },
                new IrrfInformado { MembroFamiliaId = ana.Id, Ano = 2023, Mes = 12, Cesta = CestaApuracaoIr.Comum, Valor = 8m });
            contexto.SaveChanges();
        }

        using var leitura = _fabrica.CreateDbContext();
        var campos = CapturaIrrfDarf.Montar(leitura, 2024);

        Assert.Equal(["Ana (Nica)", "Bruno"], campos.GroupBy(campo => campo.PerfilId).Select(grupo => grupo.First().Perfil).ToArray());
        Assert.Equal([6, 1], campos.Select(campo => campo.Mes).Distinct().ToArray());
        Assert.Equal(CapturaIrrfDarf.Cestas, campos.Where(campo => campo.Mes == 6).Select(campo => campo.Cesta).ToArray());
        Assert.Equal(Enum.GetValues<CestaApuracaoIr>(), CapturaIrrfDarf.Cestas.ToArray());

        var comum = campos.Single(campo => campo.PerfilId == ana.Id && campo.Mes == 6 && campo.Cesta == CestaApuracaoIr.Comum);
        var fii = campos.Single(campo => campo.PerfilId == ana.Id && campo.Mes == 6 && campo.Cesta == CestaApuracaoIr.Fii);
        var day = campos.Single(campo => campo.PerfilId == ana.Id && campo.Mes == 6 && campo.Cesta == CestaApuracaoIr.DayTrade);
        Assert.Equal(12.34m, comum.Valor);
        Assert.True(comum.FoiSalvo);
        Assert.Equal(0m, fii.Valor);
        Assert.True(fii.FoiSalvo);
        Assert.Equal(0m, day.Valor);
        Assert.False(day.FoiSalvo);
        Assert.DoesNotContain(campos, campo => campo.Mes == 3 || campo.Mes == 8 || campo.Ano != 2024);
        Assert.Equal("Janeiro", CapturaIrrfDarf.NomeDoMes(1));
        Assert.Equal("Março", CapturaIrrfDarf.NomeDoMes(3));

        var listados = CapturaIrrfDarf.Listar(leitura, 2024);
        Assert.Equal(2, listados.Count);
        Assert.Contains(listados, item => item.PerfilId == ana.Id && item.Mes == 6 && item.Cesta == CestaApuracaoIr.Comum && item.Valor == 12.34m);
        Assert.Contains(listados, item => item.PerfilId == ana.Id && item.Mes == 6 && item.Cesta == CestaApuracaoIr.Fii && item.Valor == 0m);
        Assert.DoesNotContain(listados, item => item.Mes == 3);
        Assert.Equal(9m, leitura.IrrfInformados.Single(irrf => irrf.Mes == 3).Valor);
    }

    [Fact]
    public void Montar_VendaComPregaoEm31DeJaneiroELiquidacaoEmFevereiro_PedeJaneiro()
    {
        var ana = Perfil("Ana", null);
        var bova = new AtivoFinanceiro { Ticker = "BOVA11", Nome = "Ibovespa", Tipo = TipoAtivo.Etf };
        using (var contexto = _fabrica.CreateDbContext())
        {
            contexto.AddRange(ana, bova);
            contexto.SaveChanges();
            contexto.OperacoesAtivo.AddRange(
                Operacao(ana.Id, bova.Id, TipoOperacao.Compra, new DateTime(2024, 1, 8), 100m, 100m),
                Operacao(
                    ana.Id,
                    bova.Id,
                    TipoOperacao.Venda,
                    new DateTime(2024, 2, 2),
                    200m,
                    200m,
                    pregao: new DateTime(2024, 1, 31)));
            contexto.SaveChanges();
        }

        using var leitura = _fabrica.CreateDbContext();
        var campos = CapturaIrrfDarf.Montar(leitura, 2024);

        Assert.Equal([1], campos.Select(campo => campo.Mes).Distinct().ToArray());
        Assert.Equal(CapturaIrrfDarf.Cestas.Count, campos.Count);
        Assert.All(campos, campo => Assert.Equal(2024, campo.Ano));
        Assert.Empty(CapturaIrrfDarf.Montar(leitura, 2025));

        var apuracao = ApuradorIrMensal.Apurar(leitura, 2024);
        var janeiro = apuracao.Darfs.Single(linha => linha.Mes == 1);
        Assert.Equal(15m, janeiro.Valor);
        Assert.Equal(new DateTime(2024, 2, 29), janeiro.Vencimento);
        Assert.Equal(0m, apuracao.Darfs.Single(linha => linha.Mes == 2).Valor);
        Assert.Null(apuracao.Darfs.Single(linha => linha.Mes == 2).Vencimento);
    }

    [Fact]
    public void Montar_LiquidacaoNoInicioDeJaneiroComPregaoEmDezembro_PedeDezembroDoAnoAnterior()
    {
        var ana = Perfil("Ana", null);
        var bova = new AtivoFinanceiro { Ticker = "BOVA11", Nome = "Ibovespa", Tipo = TipoAtivo.Etf };
        using (var contexto = _fabrica.CreateDbContext())
        {
            contexto.AddRange(ana, bova);
            contexto.SaveChanges();
            contexto.OperacoesAtivo.AddRange(
                Operacao(ana.Id, bova.Id, TipoOperacao.Compra, new DateTime(2024, 12, 2), 100m, 100m),
                Operacao(
                    ana.Id,
                    bova.Id,
                    TipoOperacao.Venda,
                    new DateTime(2025, 1, 2),
                    200m,
                    200m,
                    pregao: new DateTime(2024, 12, 30)));
            contexto.SaveChanges();
        }

        using var leitura = _fabrica.CreateDbContext();
        var dezembro = CapturaIrrfDarf.Montar(leitura, 2024);

        Assert.Equal([12], dezembro.Select(campo => campo.Mes).Distinct().ToArray());
        Assert.Equal(CapturaIrrfDarf.Cestas.Count, dezembro.Count);
        Assert.All(dezembro, campo => Assert.Equal(2024, campo.Ano));
        Assert.Empty(CapturaIrrfDarf.Montar(leitura, 2025));

        var apuracao = ApuradorIrMensal.Apurar(leitura, 2024);
        var darf = apuracao.Darfs.Single(linha => linha.Mes == 12);
        Assert.Equal(15m, darf.Valor);
        Assert.Equal(new DateTime(2025, 1, 31), darf.Vencimento);

        var seguinte = ApuradorIrMensal.Apurar(leitura, 2025);
        Assert.DoesNotContain(seguinte.Darfs, linha => linha.Valor > 0m);
        Assert.DoesNotContain(seguinte.Darfs, linha => linha.Vencimento is not null);
    }

    [Fact]
    public void IrrfGravado_AbateNoMaximoOImpostoDoMes_ELevaORestoAteDezembro()
    {
        var ana = Perfil("Ana", "Nica");
        var bova = new AtivoFinanceiro { Ticker = "BOVA11", Nome = "Ibovespa", Tipo = TipoAtivo.Etf };
        using (var contexto = _fabrica.CreateDbContext())
        {
            contexto.AddRange(ana, bova);
            contexto.SaveChanges();
            contexto.OperacoesAtivo.AddRange(
                Operacao(ana.Id, bova.Id, TipoOperacao.Compra, new DateTime(2024, 1, 8), 100m, 100m),
                Operacao(ana.Id, bova.Id, TipoOperacao.Venda, new DateTime(2024, 1, 22), 200m, 200m),
                Operacao(ana.Id, bova.Id, TipoOperacao.Compra, new DateTime(2025, 2, 3), 100m, 100m),
                Operacao(ana.Id, bova.Id, TipoOperacao.Venda, new DateTime(2025, 2, 18), 180m, 180m));
            contexto.SaveChanges();
            CapturaIrrfDarf.Salvar(contexto, 2024,
            [
                Campo(ana.Id, 2024, 1, CestaApuracaoIr.Comum, 40m)
            ]);
        }

        using var leitura = _fabrica.CreateDbContext();
        var manuais = CapturaIrrfDarf.Listar(leitura, 2024);
        var ano2024 = ApuradorIrMensal.Apurar(leitura, 2024, manuais);
        var janeiro = Linha(ano2024, 2024, 1, CestaApuracaoIr.Comum);
        var usado = Math.Min(janeiro.Irrf, janeiro.Imposto);
        Assert.Equal(15m, janeiro.Imposto);
        Assert.Equal(40m, janeiro.Irrf);
        Assert.Equal(15m, usado);
        Assert.Equal(janeiro.Imposto - usado, janeiro.ImpostoLiquido);
        Assert.Equal(0m, janeiro.ImpostoLiquido);
        Assert.Equal(25m, Linha(ano2024, 2024, 12, CestaApuracaoIr.Comum).IrrfALevarParaDeclaracao);
        Assert.Equal(0m, Linha(ano2024, 2024, 1, CestaApuracaoIr.Comum).IrrfALevarParaDeclaracao);

        var ano2025 = ApuradorIrMensal.Apurar(leitura, 2025, manuais);
        var fevereiro = Linha(ano2025, 2025, 2, CestaApuracaoIr.Comum);
        Assert.Equal(12m, fevereiro.Imposto);
        Assert.Equal(12m, fevereiro.ImpostoLiquido);
        Assert.Equal(0m, Linha(ano2025, 2025, 1, CestaApuracaoIr.Comum).IrrfALevarParaDeclaracao);
        Assert.Equal(0m, Linha(ano2025, 2025, 12, CestaApuracaoIr.Comum).IrrfALevarParaDeclaracao);
        Assert.Equal(0m, Linha(ano2025, 2025, 12, CestaApuracaoIr.Fii).IrrfALevarParaDeclaracao);

        using var destino = new MemoryStream();
        ExportadorDarf.Exportar(leitura, 2024, destino, manuais);
        using var pasta = new XLWorkbook(destino);
        var apuracao = pasta.Worksheet(ExportadorDarf.AbaApuracao);
        var linhaJaneiro = apuracao.RowsUsed().Single(linha =>
            linha.RowNumber() > 1 && linha.Cell(2).GetValue<int>() == 1 && linha.Cell(4).GetText() == "Operações comuns");
        var linhaDezembro = apuracao.RowsUsed().Single(linha =>
            linha.RowNumber() > 1 && linha.Cell(2).GetValue<int>() == 12 && linha.Cell(4).GetText() == "Operações comuns");
        Assert.Equal(40d, linhaJaneiro.Cell(11).GetDouble());
        Assert.Equal(0d, linhaJaneiro.Cell(12).GetDouble());
        Assert.Equal(25d, linhaDezembro.Cell(15).GetDouble());
    }

    [Fact]
    public async Task LimparTudo_ApagaOIrrfJuntoComOPerfil()
    {
        var ana = Perfil("Ana", null);
        using (var contexto = _fabrica.CreateDbContext())
        {
            contexto.MembrosFamilia.Add(ana);
            contexto.SaveChanges();
            CapturaIrrfDarf.Salvar(contexto, 2024, [Campo(ana.Id, 2024, 6, CestaApuracaoIr.Comum, 3m)]);
        }

        await new ManutencaoDadosRepository(_fabrica).LimparAsync(TipoLimpezaDados.Tudo);

        using var leitura = _fabrica.CreateDbContext();
        Assert.Empty(leitura.IrrfInformados);
        Assert.Empty(leitura.MembrosFamilia);
    }

    [Fact]
    public void MigrationDoIrrf_SoCriaATabelaNova()
    {
        var pasta = Path.Combine(Raiz(), "OrcaFacil.Core", "Data", "Migrations");
        var arquivo = Assert.Single(Directory.GetFiles(pasta, "*_AddIrrfInformado.cs"));
        var fonte = File.ReadAllText(arquivo);

        Assert.Contains("CreateTable", fonte, StringComparison.Ordinal);
        Assert.Contains("name: \"IrrfInformados\"", fonte, StringComparison.Ordinal);
        Assert.Contains("IX_IrrfInformados_MembroFamiliaId_Ano_Mes_Cesta", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("OperacoesAtivo", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("AddColumn", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("DropColumn", fonte, StringComparison.Ordinal);
    }

    private static string Raiz()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null && !File.Exists(Path.Combine(diretorio.FullName, "OrcaFacil.slnx")))
            diretorio = diretorio.Parent;

        Assert.NotNull(diretorio);
        return diretorio!.FullName;
    }

    private static CampoIrrfDarf Campo(int perfil, int ano, int mes, CestaApuracaoIr cesta, decimal valor) => new()
    {
        PerfilId = perfil,
        Ano = ano,
        Mes = mes,
        Cesta = cesta,
        Valor = valor
    };

    private static MembroFamilia Perfil(string nome, string? apelido) => new()
    {
        Nome = nome,
        Apelido = apelido,
        Avatar = nome[..1]
    };

    private static OperacaoAtivo Operacao(
        int perfilId,
        int ativoId,
        TipoOperacao tipo,
        DateTime data,
        decimal preco,
        decimal? valor = null,
        DateTime? pregao = null) => new()
    {
        MembroFamiliaId = perfilId,
        AtivoId = ativoId,
        Tipo = tipo,
        Data = data,
        DataPregao = pregao ?? data,
        Quantidade = 1,
        PrecoUnitario = preco,
        ValorOperacao = valor ?? preco
    };

    private static decimal Valor(AppDbContext contexto, int perfil, int mes, CestaApuracaoIr cesta)
        => contexto.IrrfInformados.Single(irrf => irrf.MembroFamiliaId == perfil && irrf.Mes == mes && irrf.Cesta == cesta).Valor;

    private static LinhaApuracaoMensal Linha(ApuracaoIr apuracao, int ano, int mes, CestaApuracaoIr cesta)
        => apuracao.Meses.Single(linha => linha.Ano == ano && linha.Mes == mes && linha.Cesta == cesta);
}

public class PreparoGeracaoDarfTests
{
    [Fact]
    public async Task Cancelar_NaoGeraArquivo()
    {
        var abriu = false;
        var decisao = await PreparoGeracaoDarf.DecidirAsync(2024, () =>
        {
            abriu = true;
            return Task.FromResult(false);
        });

        Assert.True(abriu);
        Assert.False(decisao.GerarArquivo);
        Assert.Null(decisao.Aviso);

        var pasta = Directory.CreateTempSubdirectory("orca-darf-cancelar-");
        try
        {
            if (decisao.GerarArquivo)
                File.WriteAllText(Path.Combine(pasta.FullName, "darfs.xlsx"), "planilha");

            Assert.Empty(Directory.EnumerateFiles(pasta.FullName));
        }
        finally
        {
            pasta.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task AnoAnteriorA2005_NaoAbreODialogo_EMostraOAvisoSemArquivo()
    {
        var abriu = false;
        var decisao = await PreparoGeracaoDarf.DecidirAsync(2004, () =>
        {
            abriu = true;
            return Task.FromResult(true);
        });

        Assert.False(abriu);
        Assert.False(decisao.GerarArquivo);
        Assert.Equal("A apuração mensal começa em janeiro de 2005.", decisao.Aviso);
        Assert.Equal(ExportadorDarf.MensagemAntesDe2005, decisao.Aviso);
        Assert.Equal(decisao.Aviso, MensagemExportacaoPlanilha.Para(new InvalidOperationException(decisao.Aviso!)));

        var pasta = Directory.CreateTempSubdirectory("orca-darf-2004-");
        try
        {
            var orquestrador = new OrquestradorExportacaoPlanilha(TimeProvider.System);
            var resultado = await orquestrador.ExportarAsync(
                () => new ConfiguracaoExportavel(),
                () => pasta.FullName,
                (caminho, _) =>
                {
                    ExportadorDarf.ExigirAno(2004);
                    File.WriteAllText(caminho, "planilha");
                },
                _ => Task.FromResult(DesfechoCompartilhamentoPlanilha.FolhaAberta));

            Assert.Equal(decisao.Aviso, resultado.Aviso);
            Assert.Equal(decisao.Aviso, orquestrador.Aviso);
            Assert.False(resultado.FolhaAberta);
            Assert.Empty(Directory.EnumerateFiles(pasta.FullName, "*", SearchOption.AllDirectories));
        }
        finally
        {
            pasta.Delete(recursive: true);
        }
    }
}
