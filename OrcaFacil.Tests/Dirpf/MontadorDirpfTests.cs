using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.B3;
using OrcaFacil.Core.Dirpf;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Tests.Dirpf;

public class MontadorDirpfTests : IDisposable
{
    private readonly TestDbContextFactory _fabrica = new();

    public void Dispose() => _fabrica.Dispose();

    [Fact]
    public void AnoBase_PosicaoEm31DeDezembro_SeparaDividendoDeFii_EIgnoraPosicaoZerada()
    {
        Semear();

        var dirpf = MontadorDirpf.Montar(_fabrica.CreateDbContext(), 2024);

        Assert.Equal(
            ["AAPL34", "BOVA11", "MXRF11", "PETR4", "ROUND3", "PETR4"],
            dirpf.Bens.Select(linha => linha.Ticker).ToArray());
        Assert.Equal(["Ana (Nica)", "Ana (Nica)", "Ana (Nica)", "Ana (Nica)", "Ana (Nica)", "Bob"], dirpf.Bens.Select(linha => linha.Perfil).ToArray());

        var petr = dirpf.Bens.Single(linha => linha.Ticker == "PETR4" && linha.Perfil == "Ana (Nica)");
        Assert.Equal(150m, petr.Quantidade);
        Assert.Equal(2287.50m, petr.CustoAquisicao);
        Assert.Equal(15.25m, petr.PrecoMedio);
        Assert.Equal("03", petr.Grupo);
        Assert.Equal("01", petr.Codigo);
        Assert.Equal("Ações (inclusive as listadas em bolsa)", petr.DescricaoCodigo);
        Assert.Equal("", petr.Cnpj);
        Assert.Equal(2024, petr.AnoBase);

        var bob = dirpf.Bens.Single(linha => linha.Ticker == "PETR4" && linha.Perfil == "Bob");
        Assert.Equal(1m, bob.Quantidade);
        Assert.Equal(10m, bob.CustoAquisicao);
        Assert.Equal("03", bob.Grupo);
        Assert.Equal("01", bob.Codigo);

        var fii = dirpf.Bens.Single(linha => linha.Ticker == "MXRF11");
        Assert.Equal("FII", fii.Tipo);
        Assert.Equal("07", fii.Grupo);
        Assert.Equal("03", fii.Codigo);
        Assert.Equal(10m, fii.Quantidade);
        Assert.Equal(100m, fii.CustoAquisicao);

        var etf = dirpf.Bens.Single(linha => linha.Ticker == "BOVA11");
        Assert.Equal("07", etf.Grupo);
        Assert.Equal("06", etf.Codigo);
        Assert.Equal("ETF - Entidade de investimento", etf.DescricaoCodigo);

        var bdr = dirpf.Bens.Single(linha => linha.Ticker == "AAPL34");
        Assert.Equal("04", bdr.Grupo);
        Assert.Equal("04", bdr.Codigo);

        Assert.DoesNotContain(dirpf.Bens, linha => linha.Ticker == "VALE3");
        Assert.DoesNotContain(dirpf.Bens, linha => linha.Ticker == "WEGE3");

        Assert.Equal(["", "MXRF11", "PETR4"], dirpf.RendimentosIsentos.Select(linha => linha.Ticker).ToArray());
        var ganhoIsento = dirpf.RendimentosIsentos.Single(linha => linha.Codigo == "20");
        Assert.Equal("Ana (Nica)", ganhoIsento.Perfil);
        Assert.Equal("Ganho líquido isento", ganhoIsento.Natureza);
        Assert.Equal(CatalogoCodigosDirpf.DescricaoGanhoIsentoAcoes, ganhoIsento.DescricaoCodigo);
        Assert.Equal(1229.50m, ganhoIsento.Valor);
        Assert.Equal("", ganhoIsento.Cnpj);

        var dividendo = dirpf.RendimentosIsentos.Single(linha => linha.Ticker == "PETR4");
        Assert.Equal("Dividendo", dividendo.Natureza);
        Assert.Equal("09", dividendo.Codigo);
        Assert.Equal("Lucros e dividendos recebidos", dividendo.DescricaoCodigo);
        Assert.Equal(14m, dividendo.Valor);
        Assert.Equal("", dividendo.Cnpj);
        Assert.Equal(2024, dividendo.AnoBase);

        var rendimentoFii = dirpf.RendimentosIsentos.Single(linha => linha.Ticker == "MXRF11");
        Assert.Equal("Rendimento de FII", rendimentoFii.Natureza);
        Assert.Equal("99", rendimentoFii.Codigo);
        Assert.Equal("Outros", rendimentoFii.DescricaoCodigo);
        Assert.Equal(5m, rendimentoFii.Valor);
        Assert.NotEqual(dividendo.Codigo, rendimentoFii.Codigo);
        Assert.NotEqual(dividendo.Natureza, rendimentoFii.Natureza);

        Assert.DoesNotContain(dirpf.RendimentosIsentos, linha => linha.Natureza == "JCP");
        var jcp = Assert.Single(dirpf.RendimentosExclusivos);
        Assert.Equal("PETR4", jcp.CodigoAtivo);
        Assert.Equal("JCP", jcp.Natureza);
        Assert.Equal("10", jcp.Codigo);
        Assert.Equal(CatalogoCodigosDirpf.DescricaoJcp, jcp.DescricaoCodigo);
        Assert.Equal(100m, jcp.ValorLiquido);
        Assert.Null(jcp.IrRetido);
        Assert.Equal("", jcp.Cnpj);
        Assert.Empty(dirpf.RendaFixa);
    }

    [Fact]
    public void AnoSeguinte_UsaSoOPagamentoDaVirada_EIncluiACompraDepoisDoCorte()
    {
        Semear();

        var dirpf = MontadorDirpf.Montar(_fabrica.CreateDbContext(), 2025);

        var dividendo = Assert.Single(dirpf.RendimentosIsentos);
        Assert.Equal("PETR4", dividendo.Ticker);
        Assert.Equal("Dividendo", dividendo.Natureza);
        Assert.Equal(7m, dividendo.Valor);
        Assert.Equal(2025, dividendo.AnoBase);

        var petr = dirpf.Bens.Single(linha => linha.Ticker == "PETR4" && linha.Perfil == "Ana (Nica)");
        Assert.Equal(160m, petr.Quantidade);
        Assert.Equal(2297.50m, petr.CustoAquisicao);
        Assert.Empty(dirpf.RendimentosExclusivos);
        Assert.Empty(dirpf.RendaFixa);
    }

    [Fact]
    public void Arredondamento_AfastaDoZero_EObedeceAsCasasDaRegra()
    {
        Semear();

        var comDuasCasas = MontadorDirpf.Montar(_fabrica.CreateDbContext(), 2024);
        var arredondado = comDuasCasas.Bens.Single(linha => linha.Ticker == "ROUND3");
        Assert.Equal(1.01m, arredondado.CustoAquisicao);
        Assert.Equal(1.01m, arredondado.PrecoMedio);

        using (var contexto = _fabrica.CreateDbContext())
        {
            contexto.RegrasFiscais.Single(regra => regra.Codigo == CodigosRegraFiscal.CasasDecimaisResultado).Valor = 3m;
            contexto.SaveChanges();
        }

        var comTresCasas = MontadorDirpf.Montar(_fabrica.CreateDbContext(), 2024);
        var exato = comTresCasas.Bens.Single(linha => linha.Ticker == "ROUND3");
        Assert.Equal(1.005m, exato.CustoAquisicao);
        Assert.Equal(1.005m, exato.PrecoMedio);
    }

    [Fact]
    public void VigenciaMaisRecente_SubstituiOCodigoDaSemente()
    {
        Semear();
        using (var contexto = _fabrica.CreateDbContext())
        {
            contexto.CodigosDirpf.Add(new CodigoDirpf
            {
                Ficha = TipoFichaDirpf.Bem,
                TipoAtivo = TipoAtivo.Acao,
                Grupo = "03",
                Codigo = "02",
                Descricao = "Ações (vigência nova)",
                VigenciaInicio = new DateTime(2024, 7, 1),
                EhSistema = false
            });
            contexto.CodigosDirpf.Add(new CodigoDirpf
            {
                Ficha = TipoFichaDirpf.RendimentoIsento,
                TipoAtivo = TipoAtivo.Fii,
                Grupo = "",
                Codigo = "26",
                Descricao = "Outros (vigência nova)",
                VigenciaInicio = new DateTime(2024, 1, 1),
                EhSistema = false
            });
            contexto.SaveChanges();
        }

        var dirpf = MontadorDirpf.Montar(_fabrica.CreateDbContext(), 2024);
        Assert.Equal("02", dirpf.Bens.Single(linha => linha.Ticker == "PETR4" && linha.Perfil == "Ana (Nica)").Codigo);
        Assert.Equal("26", dirpf.RendimentosIsentos.Single(linha => linha.Ticker == "MXRF11").Codigo);
        Assert.Equal("03", dirpf.Bens.Single(linha => linha.Ticker == "MXRF11").Codigo);
    }

    [Fact]
    public void SemCodigoVigente_RecusaAPosicao_EAnoVazioNaoRecusa()
    {
        Semear();
        using (var contexto = _fabrica.CreateDbContext())
        {
            var bdr = contexto.CodigosDirpf.Single(codigo =>
                codigo.Ficha == TipoFichaDirpf.Bem && codigo.TipoAtivo == TipoAtivo.Bdr);
            bdr.VigenciaFim = new DateTime(2023, 12, 31);
            contexto.SaveChanges();
        }

        var excecao = Assert.Throws<InvalidOperationException>(() => MontadorDirpf.Montar(_fabrica.CreateDbContext(), 2024));
        Assert.Contains("Bdr", excecao.Message, StringComparison.Ordinal);

        var vazio = MontadorDirpf.Montar(_fabrica.CreateDbContext(), 2020);
        Assert.Empty(vazio.Bens);
        Assert.Empty(vazio.RendimentosIsentos);
        Assert.Empty(vazio.RendimentosExclusivos);
        Assert.Empty(vazio.RendaFixa);
    }

    [Fact]
    public void Fiagro_NaExportacaoUsa0702_ENoClassificadorContinuaFii()
    {
        Semear();
        using (var contexto = _fabrica.CreateDbContext())
        {
            var ana = contexto.MembrosFamilia.Single(membro => membro.Nome == "Ana");
            var fiagro = new AtivoFinanceiro { Ticker = "SNAG11", Nome = "Suno Agro fiagro", Tipo = TipoAtivo.Fii };
            var acao = new AtivoFinanceiro { Ticker = "AGRO3", Nome = "BrasilAgro Fiagro", Tipo = TipoAtivo.Acao };
            contexto.AddRange(fiagro, acao);
            contexto.SaveChanges();
            contexto.OperacoesAtivo.AddRange(
                Operacao(ana, fiagro, TipoOperacao.Compra, new DateTime(2024, 2, 1), 8, 80m, 0m, 10m),
                Operacao(ana, acao, TipoOperacao.Compra, new DateTime(2024, 2, 2), 3, 30m, 0m, 10m));
            contexto.Proventos.Add(Provento(ana, fiagro, TipoProvento.Rendimento, new DateTime(2024, 6, 1), 6m, new DateTime(2024, 6, 1)));
            contexto.SaveChanges();
        }

        Assert.Equal(ClasseAtivoB3.FII, ClassificadorAtivoB3.Classificar("SNAG11", "SNAG11 - SUNO AGRO FIAGRO"));
        Assert.Equal(ClasseAtivoB3.Acao, ClassificadorAtivoB3.Classificar("AGRO3", "AGRO3 - BRASILAGRO"));

        var dirpf = MontadorDirpf.Montar(_fabrica.CreateDbContext(), 2024);
        var bem = dirpf.Bens.Single(linha => linha.Ticker == "SNAG11");
        Assert.Equal("FII", bem.Tipo);
        Assert.Equal("07", bem.Grupo);
        Assert.Equal("02", bem.Codigo);
        Assert.Equal("Fundos de Investimento nas Cadeias Produtivas Agroindustriais (Fiagro)", bem.DescricaoCodigo);
        Assert.Equal(8m, bem.Quantidade);
        Assert.Equal(80m, bem.CustoAquisicao);
        Assert.Equal(10m, bem.PrecoMedio);

        var fii = dirpf.Bens.Single(linha => linha.Ticker == "MXRF11");
        Assert.Equal("07", fii.Grupo);
        Assert.Equal("03", fii.Codigo);

        var acaoComNome = dirpf.Bens.Single(linha => linha.Ticker == "AGRO3");
        Assert.Equal("03", acaoComNome.Grupo);
        Assert.Equal("01", acaoComNome.Codigo);

        var rendimento = dirpf.RendimentosIsentos.Single(linha => linha.Ticker == "SNAG11");
        Assert.Equal("99", rendimento.Codigo);
        Assert.Equal("Rendimento de Fiagro", rendimento.Natureza);
        Assert.Equal(6m, rendimento.Valor);
        Assert.Equal("Rendimento de FII", dirpf.RendimentosIsentos.Single(linha => linha.Ticker == "MXRF11").Natureza);
    }

    private void Semear()
    {
        using var contexto = _fabrica.CreateDbContext();
        var ana = new MembroFamilia { Nome = "Ana", Apelido = "Nica", Avatar = "A" };
        var bob = new MembroFamilia { Nome = "Bob", Avatar = "B" };
        var petr = new AtivoFinanceiro { Ticker = "PETR4", Nome = "Petrobras", Tipo = TipoAtivo.Acao };
        var vale = new AtivoFinanceiro { Ticker = "VALE3", Nome = "Vale", Tipo = TipoAtivo.Acao };
        var wege = new AtivoFinanceiro { Ticker = "WEGE3", Nome = "WEG", Tipo = TipoAtivo.Acao };
        var fii = new AtivoFinanceiro { Ticker = "MXRF11", Nome = "Maxi Renda", Tipo = TipoAtivo.Fii };
        var etf = new AtivoFinanceiro { Ticker = "BOVA11", Nome = "Ibovespa", Tipo = TipoAtivo.Etf };
        var bdr = new AtivoFinanceiro { Ticker = "AAPL34", Nome = "Apple", Tipo = TipoAtivo.Bdr };
        var arredondado = new AtivoFinanceiro { Ticker = "ROUND3", Nome = "Arredonda", Tipo = TipoAtivo.Acao };
        contexto.AddRange(ana, bob, petr, vale, wege, fii, etf, bdr, arredondado);
        contexto.SaveChanges();

        contexto.OperacoesAtivo.AddRange(
            Operacao(ana, petr, TipoOperacao.Compra, new DateTime(2023, 11, 1), 100, 1500m, 0m, 10m),
            Operacao(ana, petr, TipoOperacao.Compra, new DateTime(2024, 2, 10), 100, 2200m, 50m, 20m),
            Operacao(ana, petr, TipoOperacao.Venda, new DateTime(2024, 6, 1), 50, 2000m, 8m, 40m),
            Operacao(ana, petr, TipoOperacao.Compra, new DateTime(2025, 1, 15), 10, 9999m, 0m, 1m),
            Operacao(ana, vale, TipoOperacao.Compra, new DateTime(2024, 1, 5), 10, 100m, 0m, 10m),
            Operacao(ana, vale, TipoOperacao.Venda, new DateTime(2024, 8, 1), 10, 90m, 0m, 9m),
            Operacao(ana, wege, TipoOperacao.Compra, new DateTime(2025, 2, 1), 5, 500m, 0m, 100m),
            Operacao(ana, fii, TipoOperacao.Compra, new DateTime(2024, 1, 20), 10, 100m, 0m, 10m),
            Operacao(ana, etf, TipoOperacao.Compra, new DateTime(2024, 3, 1), 2, 200m, 0m, 100m),
            Operacao(ana, bdr, TipoOperacao.Compra, new DateTime(2024, 3, 2), 1, 80m, 0m, 80m),
            Operacao(ana, arredondado, TipoOperacao.Compra, new DateTime(2024, 4, 1), 1, 1.005m, 0m, 1.005m),
            Operacao(bob, petr, TipoOperacao.Compra, new DateTime(2024, 4, 1), 1, 10m, 0m, 10m));

        contexto.Proventos.AddRange(
            Provento(ana, petr, TipoProvento.Rendimento, new DateTime(2024, 12, 31), 10m, new DateTime(2026, 2, 1)),
            Provento(ana, petr, TipoProvento.Rendimento, new DateTime(2024, 6, 15), 4m, new DateTime(2024, 6, 15)),
            Provento(ana, petr, TipoProvento.Rendimento, new DateTime(2025, 1, 1), 7m, new DateTime(2024, 12, 1)),
            Provento(ana, petr, TipoProvento.Jcp, new DateTime(2024, 8, 1), 100m, new DateTime(2024, 8, 1)),
            Provento(ana, fii, TipoProvento.Rendimento, new DateTime(2024, 3, 1), 3m, new DateTime(2024, 3, 1)),
            Provento(ana, fii, TipoProvento.Rendimento, new DateTime(2024, 11, 30), 2m, new DateTime(2024, 11, 30)),
            Provento(ana, etf, TipoProvento.Rendimento, new DateTime(2024, 5, 1), 50m, new DateTime(2024, 5, 1)),
            Provento(ana, bdr, TipoProvento.Rendimento, new DateTime(2024, 5, 2), 40m, new DateTime(2024, 5, 2)));
        contexto.SaveChanges();
    }

    private static OperacaoAtivo Operacao(
        MembroFamilia membro,
        AtivoFinanceiro ativo,
        TipoOperacao tipo,
        DateTime data,
        decimal quantidade,
        decimal valor,
        decimal custos,
        decimal preco) => new()
    {
        MembroFamiliaId = membro.Id,
        MembroFamilia = membro,
        AtivoId = ativo.Id,
        Ativo = ativo,
        Tipo = tipo,
        Data = data,
        Quantidade = quantidade,
        ValorOperacao = valor,
        Custos = custos,
        PrecoUnitario = preco
    };

    private static Provento Provento(
        MembroFamilia membro,
        AtivoFinanceiro ativo,
        TipoProvento tipo,
        DateTime pagamento,
        decimal valor,
        DateTime criadoEm) => new()
    {
        MembroFamiliaId = membro.Id,
        MembroFamilia = membro,
        AtivoId = ativo.Id,
        Ativo = ativo,
        Tipo = tipo,
        DataPagamento = pagamento,
        ValorLiquido = valor,
        CriadoEm = criadoEm
    };
}
