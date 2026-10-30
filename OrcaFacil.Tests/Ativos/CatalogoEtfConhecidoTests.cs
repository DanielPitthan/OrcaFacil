using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.B3;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Dirpf;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Tests.Ativos;

public class CatalogoEtfConhecidoTests : IDisposable
{
    private readonly TestDbContextFactory _fabrica = new();

    public void Dispose() => _fabrica.Dispose();

    [Fact]
    public void EtfConhecido_ClassificaComoEtf_EDirpfUsa0706()
    {
        var tabela = new TabelaCodigosDirpf(CatalogoCodigosDirpf.Linhas());
        var dia = new DateTime(2026, 5, 26);

        foreach (var ticker in new[] { "BOVA11", "bova11", " BOVA11 ", "GOLD11", "SMAL11", "IVVB11", "BOVA11F" })
        {
            var tipo = CatalogoEtfConhecido.TipoNaImportacao(ticker, ClasseAtivoB3.Acao);
            Assert.Equal(TipoAtivo.Etf, tipo);
            var codigo = tabela.Exigir(TipoFichaDirpf.Bem, tipo, dia);
            Assert.Equal("07", codigo.Grupo);
            Assert.Equal("06", codigo.Codigo);
            Assert.Equal("ETF - Entidade de investimento", codigo.Descricao);
        }

        // Ticker conhecido vence a heurística de FII do extrato (XFIX11 cita fundo imobiliário).
        var xfix = CatalogoEtfConhecido.TipoNaImportacao("XFIX11", ClasseAtivoB3.FII);
        Assert.Equal(TipoAtivo.Etf, xfix);
        Assert.Equal("06", tabela.Exigir(TipoFichaDirpf.Bem, xfix, dia).Codigo);

        var acao = CatalogoEtfConhecido.TipoNaImportacao("PETR4", ClasseAtivoB3.Acao);
        Assert.Equal(TipoAtivo.Acao, acao);
        var codigoAcao = tabela.Exigir(TipoFichaDirpf.Bem, acao, dia);
        Assert.Equal("03", codigoAcao.Grupo);
        Assert.Equal("01", codigoAcao.Codigo);

        var fii = CatalogoEtfConhecido.TipoNaImportacao("MXRF11", ClasseAtivoB3.FII);
        Assert.Equal(TipoAtivo.Fii, fii);
        Assert.Equal("03", tabela.Exigir(TipoFichaDirpf.Bem, fii, dia).Codigo);

        Assert.False(CatalogoEtfConhecido.EhConhecido("TAEE11"));
        Assert.False(CatalogoEtfConhecido.EhConhecido("PETR4F"));
        Assert.False(CatalogoEtfConhecido.EhConhecido(null));
        Assert.False(CatalogoEtfConhecido.EhConhecido(""));
        Assert.Equal(TipoAtivo.Etf, CatalogoEtfConhecido.TipoNoCadastro("bova11", TipoAtivo.Acao));
        Assert.Equal(TipoAtivo.Fii, CatalogoEtfConhecido.TipoNoCadastro("MXRF11", TipoAtivo.Fii));
    }

    [Fact]
    public void Reclassificar_SoAcaoConhecida_ESegundaPassagemNaoAltera()
    {
        var bova = new AtivoFinanceiro { Ticker = "BOVA11", Nome = "Ibovespa", Tipo = TipoAtivo.Acao };
        var fracionario = new AtivoFinanceiro { Ticker = "SMAL11F", Nome = "Small", Tipo = TipoAtivo.Acao };
        var petr = new AtivoFinanceiro { Ticker = "PETR4", Nome = "Petrobras", Tipo = TipoAtivo.Acao };
        var fii = new AtivoFinanceiro { Ticker = "MXRF11", Nome = "Maxi", Tipo = TipoAtivo.Fii };
        var jaEtf = new AtivoFinanceiro { Ticker = "GOLD11", Nome = "Ouro", Tipo = TipoAtivo.Etf };
        var bdr = new AtivoFinanceiro { Ticker = "IVVB11", Nome = "S&P", Tipo = TipoAtivo.Bdr };
        var ativos = new[] { bova, fracionario, petr, fii, jaEtf, bdr };

        Assert.Equal(2, CatalogoEtfConhecido.Reclassificar(ativos));
        Assert.Equal(TipoAtivo.Etf, bova.Tipo);
        Assert.Equal(TipoAtivo.Etf, fracionario.Tipo);
        Assert.Equal(TipoAtivo.Acao, petr.Tipo);
        Assert.Equal(TipoAtivo.Fii, fii.Tipo);
        Assert.Equal(TipoAtivo.Etf, jaEtf.Tipo);
        Assert.Equal(TipoAtivo.Bdr, bdr.Tipo);

        Assert.Equal(0, CatalogoEtfConhecido.Reclassificar(ativos));
        Assert.Equal(TipoAtivo.Etf, bova.Tipo);
        Assert.Equal(TipoAtivo.Acao, petr.Tipo);
        Assert.Equal(TipoAtivo.Bdr, bdr.Tipo);
    }

    [Fact]
    public async Task Reclassificar_NoBanco_AlteraSoAcaoConhecida_ERepetirNaoMudaNada()
    {
        await using (var ctx = _fabrica.CreateDbContext())
        {
            ctx.AtivosFinanceiros.AddRange(
                new AtivoFinanceiro { Ticker = "BOVA11", Nome = "Ibovespa", Tipo = TipoAtivo.Acao, Ativo = true },
                new AtivoFinanceiro { Ticker = "SMAL11", Nome = "Small", Tipo = TipoAtivo.Acao, Ativo = false },
                new AtivoFinanceiro { Ticker = "GOLD11", Nome = "Ouro", Tipo = TipoAtivo.Etf },
                new AtivoFinanceiro { Ticker = "PETR4", Nome = "Petrobras", Tipo = TipoAtivo.Acao },
                new AtivoFinanceiro { Ticker = "MXRF11", Nome = "Maxi", Tipo = TipoAtivo.Fii });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _fabrica.CreateDbContext())
        {
            Assert.Equal(2, await CatalogoEtfConhecido.ReclassificarAsync(ctx));
            Assert.Equal(0, await CatalogoEtfConhecido.ReclassificarAsync(ctx));
        }

        await using var conferido = _fabrica.CreateDbContext();
        var ativos = await conferido.AtivosFinanceiros.OrderBy(ativo => ativo.Ticker).ToListAsync();
        Assert.Equal(5, ativos.Count);
        Assert.Equal(TipoAtivo.Etf, ativos.Single(ativo => ativo.Ticker == "BOVA11").Tipo);
        Assert.Equal("Ibovespa", ativos.Single(ativo => ativo.Ticker == "BOVA11").Nome);
        Assert.True(ativos.Single(ativo => ativo.Ticker == "BOVA11").Ativo);
        Assert.Equal(TipoAtivo.Etf, ativos.Single(ativo => ativo.Ticker == "SMAL11").Tipo);
        Assert.False(ativos.Single(ativo => ativo.Ticker == "SMAL11").Ativo);
        Assert.Equal(TipoAtivo.Etf, ativos.Single(ativo => ativo.Ticker == "GOLD11").Tipo);
        Assert.Equal(TipoAtivo.Acao, ativos.Single(ativo => ativo.Ticker == "PETR4").Tipo);
        Assert.Equal(TipoAtivo.Fii, ativos.Single(ativo => ativo.Ticker == "MXRF11").Tipo);

        Assert.Equal(0, await CatalogoEtfConhecido.ReclassificarAsync(conferido));
        Assert.Equal(5, await conferido.AtivosFinanceiros.CountAsync());
    }

    [Fact]
    public async Task Preparo_ReclassificaEtfConhecido_ERepetirNaoAltera()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "orcafacil-etf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        var caminho = Path.Combine(pasta, "orcafacil.db");
        var opcoes = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={caminho}").Options;

        try
        {
            await using (var ctx = new AppDbContext(opcoes))
            {
                var preparado = await PreparoBanco.MigrarEVerificarAsync(ctx);
                Assert.False(preparado.ExigeRecuperacao);

                ctx.AtivosFinanceiros.Add(new AtivoFinanceiro { Ticker = "BOVA11", Nome = "Ibovespa", Tipo = TipoAtivo.Acao });
                ctx.AtivosFinanceiros.Add(new AtivoFinanceiro { Ticker = "PETR4", Nome = "Petrobras", Tipo = TipoAtivo.Acao });
                await ctx.SaveChangesAsync();

                var reclassificado = await PreparoBanco.MigrarEVerificarAsync(ctx);
                Assert.False(reclassificado.ExigeRecuperacao);
                var deNovo = await PreparoBanco.MigrarEVerificarAsync(ctx);
                Assert.False(deNovo.ExigeRecuperacao);
            }

            await using var conferido = new AppDbContext(opcoes);
            var ativos = await conferido.AtivosFinanceiros.OrderBy(ativo => ativo.Ticker).ToListAsync();
            Assert.Equal(2, ativos.Count);
            Assert.Equal(TipoAtivo.Etf, ativos.Single(ativo => ativo.Ticker == "BOVA11").Tipo);
            Assert.Equal(TipoAtivo.Acao, ativos.Single(ativo => ativo.Ticker == "PETR4").Tipo);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(pasta))
                Directory.Delete(pasta, recursive: true);
        }
    }
}
