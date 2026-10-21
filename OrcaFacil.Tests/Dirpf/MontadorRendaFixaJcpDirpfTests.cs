using ClosedXML.Excel;
using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.Dirpf;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Services;

namespace OrcaFacil.Tests.Dirpf;

public class MontadorRendaFixaJcpDirpfTests : IDisposable
{
    private readonly TestDbContextFactory _fabrica = new();

    public void Dispose() => _fabrica.Dispose();

    [Fact]
    public void Jcp_UsaOAnoDoPagamento_OCodigo10_EOValorLiquido_SemGrossUp()
    {
        var ana = Perfil(1, "Ana", "Nica");
        var petr = Ativo(1, "PETR4", "Petrobras", TipoAtivo.Acao);
        var proventos = new[]
        {
            Jcp(ana, petr, new DateTime(2024, 12, 31), 10.005m),
            Jcp(ana, petr, new DateTime(2025, 1, 1), 85m),
            Jcp(ana, petr, new DateTime(2024, 3, 1), 0m)
        };

        var em2024 = Montar(2024, proventos);
        var linha = Assert.Single(em2024.RendimentosExclusivos);
        Assert.Equal("10", linha.Codigo);
        Assert.Equal(CatalogoCodigosDirpf.DescricaoJcp, linha.DescricaoCodigo);
        Assert.Equal("JCP", linha.Natureza);
        Assert.Equal("Ana (Nica)", linha.Perfil);
        Assert.Equal("PETR4", linha.CodigoAtivo);
        Assert.Equal(10.01m, linha.ValorLiquido);
        Assert.Null(linha.IrRetido);
        Assert.Equal("", linha.Cnpj);
        Assert.NotEqual(11.78m, linha.ValorLiquido);
        Assert.Empty(em2024.RendimentosIsentos);

        var em2025 = Assert.Single(Montar(2025, proventos).RendimentosExclusivos);
        Assert.Equal(85m, em2025.ValorLiquido);
        Assert.NotEqual(100m, em2025.ValorLiquido);
    }

    [Fact]
    public void Jcp_DeBdr_EntraNaExclusiva_EDividendoContinuaIsento()
    {
        var ana = Perfil(1, "Ana", null);
        var bob = Perfil(2, "Bob", null);
        var bdr = Ativo(1, "AAPL34", "Apple", TipoAtivo.Bdr);
        var petr = Ativo(2, "PETR4", "Petrobras", TipoAtivo.Acao);
        var proventos = new[]
        {
            Jcp(ana, bdr, new DateTime(2024, 5, 1), 3m),
            Jcp(bob, petr, new DateTime(2024, 5, 2), 4m),
            Provento(ana, petr, TipoProvento.Rendimento, new DateTime(2024, 5, 3), 8m)
        };

        var dirpf = Montar(2024, proventos);
        Assert.Equal(["AAPL34", "PETR4"], dirpf.RendimentosExclusivos.Select(linha => linha.CodigoAtivo).ToArray());
        Assert.All(dirpf.RendimentosExclusivos, linha => Assert.Equal("10", linha.Codigo));
        Assert.Equal(["Ana", "Bob"], dirpf.RendimentosExclusivos.Select(linha => linha.Perfil).ToArray());

        var dividendo = Assert.Single(dirpf.RendimentosIsentos);
        Assert.Equal("09", dividendo.Codigo);
        Assert.Equal(8m, dividendo.Valor);
    }

    [Fact]
    public void RendaFixa_UsaOIrGravado_IgnoraATabelaRegressiva_EPosicionaEm0402()
    {
        var ana = Perfil(1, "Ana", "Nica");
        var cdb = Titulo(1, "CDB1", "CDB do banco", TipoTituloRendaFixa.CDB, "XP");
        var tesouro = Titulo(2, "Tesouro IPCA+ 2035", "Tesouro", TipoTituloRendaFixa.TesouroIPCA, "Clear");
        var operacoes = new[]
        {
            Operacao(1, cdb, new DateTime(2024, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 1m, 1000m, 0m),
            Operacao(2, cdb, new DateTime(2024, 2, 10), TipoOperacaoRendaFixa.Resgate, 1m, 1100m, 10m),
            Operacao(3, tesouro, new DateTime(2024, 3, 1), TipoOperacaoRendaFixa.Aplicacao, 0.06m, 50m, 0m),
            Operacao(4, tesouro, new DateTime(2025, 1, 2), TipoOperacaoRendaFixa.Aplicacao, 1m, 500m, 0m)
        };
        var regras = Regras();
        regras.Single(regra => regra.Codigo == CodigosRegraFiscal.AliquotaAcao).Valor = 0.225m;

        var dirpf = Montar(2024, [], [cdb, tesouro], operacoes, [ana], regras);

        var exclusivo = Assert.Single(dirpf.RendimentosExclusivos);
        Assert.Equal("06", exclusivo.Codigo);
        Assert.Equal(CatalogoCodigosDirpf.DescricaoRendimentoRendaFixa, exclusivo.DescricaoCodigo);
        Assert.Equal("Rendimento de renda fixa", exclusivo.Natureza);
        Assert.Equal("CDB1", exclusivo.CodigoAtivo);
        Assert.Equal(90m, exclusivo.ValorLiquido);
        Assert.Equal(10m, exclusivo.IrRetido);
        Assert.NotEqual(77.50m, exclusivo.ValorLiquido);
        Assert.Equal("", exclusivo.Cnpj);
        Assert.Empty(dirpf.RendimentosIsentos);

        var posicao = Assert.Single(dirpf.RendaFixa);
        Assert.Equal("Tesouro IPCA+ 2035", posicao.Codigo);
        Assert.Equal("Tesouro IPCA", posicao.Tipo);
        Assert.Equal("Clear", posicao.Corretora);
        Assert.Equal("04", posicao.Grupo);
        Assert.Equal("02", posicao.CodigoDirpf);
        Assert.Equal(CatalogoCodigosDirpf.DescricaoBemRendaFixa, posicao.DescricaoCodigo);
        Assert.Equal(0.06m, posicao.Quantidade);
        Assert.Equal(50m, posicao.CustoAquisicao);
        Assert.Equal(0m, posicao.IrRetidoNoAno);
        Assert.Equal("", posicao.Cnpj);
        Assert.Equal("Ana (Nica)", posicao.Perfil);
        Assert.DoesNotContain(MontadorDirpf.ObservacaoTituloOutro, exclusivo.Descricao);
        Assert.DoesNotContain(MontadorDirpf.ObservacaoTituloOutro, posicao.Discriminacao);

        var seguinte = Montar(2025, [], [cdb, tesouro], operacoes, [ana], regras);
        var aberta = Assert.Single(seguinte.RendaFixa);
        Assert.Equal(1.06m, aberta.Quantidade);
        Assert.Equal(550m, aberta.CustoAquisicao);
        Assert.Empty(seguinte.RendimentosExclusivos);
    }

    [Fact]
    public void PrejuizoNaoEntraNaExclusiva_EPosicaoDeCustoMeioCentavoAfastaDoZero()
    {
        var ana = Perfil(1, "Ana", null);
        var perda = Titulo(1, "CDBPERDA", "CDB", TipoTituloRendaFixa.CDB, "XP");
        var miudo = Titulo(2, "CDBMIUDO", "CDB miúdo", TipoTituloRendaFixa.CDB, "XP");
        var operacoes = new[]
        {
            Operacao(1, perda, new DateTime(2024, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 1m, 1000m, 0m),
            Operacao(2, perda, new DateTime(2024, 6, 1), TipoOperacaoRendaFixa.Resgate, 1m, 900m, 0m),
            Operacao(3, miudo, new DateTime(2024, 4, 1), TipoOperacaoRendaFixa.Aplicacao, 1m, 1.005m, 0m)
        };

        var dirpf = Montar(2024, [], [perda, miudo], operacoes, [ana]);
        Assert.Empty(dirpf.RendimentosExclusivos);
        var linha = Assert.Single(dirpf.RendaFixa);
        Assert.Equal("CDBMIUDO", linha.Codigo);
        Assert.Equal(1.01m, linha.CustoAquisicao);
        Assert.Equal(1.01m, linha.PrecoMedio);
    }

    [Fact]
    public void CustoProporcional_ECasasDaRegra_AfastamDoZero()
    {
        var ana = Perfil(1, "Ana", null);
        var titulo = Titulo(1, "CDBPARC", "CDB", TipoTituloRendaFixa.CDB, "XP");
        var operacoes = new[]
        {
            Operacao(1, titulo, new DateTime(2024, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 3m, 10m, 0m),
            Operacao(2, titulo, new DateTime(2024, 6, 1), TipoOperacaoRendaFixa.Resgate, 1m, 4m, 0m)
        };

        var dirpf = Montar(2024, [], [titulo], operacoes, [ana]);
        var posicao = Assert.Single(dirpf.RendaFixa);
        Assert.Equal(2m, posicao.Quantidade);
        Assert.Equal(6.67m, posicao.CustoAquisicao);
        Assert.Equal(3.33m, posicao.PrecoMedio);
        var exclusivo = Assert.Single(dirpf.RendimentosExclusivos);
        Assert.Equal(0.67m, exclusivo.ValorLiquido);
        Assert.Equal(0m, exclusivo.IrRetido);

        var regras = Regras();
        regras.Single(regra => regra.Codigo == CodigosRegraFiscal.CasasDecimaisResultado).Valor = 0m;
        var inteiro = Montar(2024, [], [titulo], operacoes, [ana], regras);
        Assert.Equal(7m, Assert.Single(inteiro.RendaFixa).CustoAquisicao);
        Assert.Equal(1m, Assert.Single(inteiro.RendimentosExclusivos).ValorLiquido);
    }

    [Fact]
    public void Outro_NaoViraRendimentoIsento()
    {
        var ana = Perfil(1, "Ana", null);
        var lci = Titulo(1, "LCI1", "Letra", TipoTituloRendaFixa.Outro, "XP");
        var operacoes = new[]
        {
            Operacao(1, lci, new DateTime(2024, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 2m, 200m, 0m),
            Operacao(2, lci, new DateTime(2024, 8, 1), TipoOperacaoRendaFixa.Resgate, 1m, 130m, 0m)
        };

        var dirpf = Montar(2024, [], [lci], operacoes, [ana]);
        Assert.Empty(dirpf.RendimentosIsentos);
        var exclusivo = Assert.Single(dirpf.RendimentosExclusivos);
        Assert.Equal("06", exclusivo.Codigo);
        Assert.Equal(30m, exclusivo.ValorLiquido);
        var posicao = Assert.Single(dirpf.RendaFixa);
        Assert.Equal("Outro", posicao.Tipo);
        Assert.Equal("04", posicao.Grupo);
        Assert.Equal("02", posicao.CodigoDirpf);
        Assert.Equal(100m, posicao.CustoAquisicao);
        Assert.EndsWith(" " + MontadorDirpf.ObservacaoTituloOutro, exclusivo.Descricao);
        Assert.EndsWith(" " + MontadorDirpf.ObservacaoTituloOutro, posicao.Discriminacao);
    }

    [Fact]
    public void Outro_GravaAObservacaoDeLciLca_OndeALinhaApareceNaPlanilha()
    {
        const string observacao =
            "Se for LCI/LCA, é isento: declarar em Rendimentos Isentos (código 12) e no grupo 04/03";
        Assert.Equal(observacao, MontadorDirpf.ObservacaoTituloOutro);

        var ana = Perfil(1, "Ana", null);
        var aberto = Titulo(1, "LCIABERTO", "Letra aberta", TipoTituloRendaFixa.Outro, "XP");
        var soRendimento = Titulo(2, "LCIFECHADA", "Letra resgatada", TipoTituloRendaFixa.Outro, "XP");
        var soPosicao = Titulo(3, "CRIABERTO", "CRI sem rendimento", TipoTituloRendaFixa.Outro, "XP");
        var cdb = Titulo(4, "CDB1", "CDB", TipoTituloRendaFixa.CDB, "XP");
        var tesouro = Titulo(5, "TESOURO", "Tesouro", TipoTituloRendaFixa.TesouroSelic, "XP");
        var operacoes = new[]
        {
            Operacao(1, aberto, new DateTime(2024, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 2m, 200m, 0m),
            Operacao(2, aberto, new DateTime(2024, 8, 1), TipoOperacaoRendaFixa.Resgate, 1m, 130m, 0m),
            Operacao(3, soRendimento, new DateTime(2024, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 1m, 100m, 0m),
            Operacao(4, soRendimento, new DateTime(2024, 9, 1), TipoOperacaoRendaFixa.Resgate, 1m, 140m, 0m),
            Operacao(5, soPosicao, new DateTime(2024, 3, 1), TipoOperacaoRendaFixa.Aplicacao, 1m, 80m, 0m),
            Operacao(6, cdb, new DateTime(2024, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 2m, 200m, 0m),
            Operacao(7, cdb, new DateTime(2024, 8, 1), TipoOperacaoRendaFixa.Resgate, 1m, 130m, 0m),
            Operacao(8, tesouro, new DateTime(2024, 2, 1), TipoOperacaoRendaFixa.Aplicacao, 1m, 50m, 0m)
        };
        var titulos = new[] { aberto, soRendimento, soPosicao, cdb, tesouro };

        var dirpf = Montar(2024, [], titulos, operacoes, [ana]);
        Assert.Empty(dirpf.RendimentosIsentos);

        var exclusivoAberto = Assert.Single(dirpf.RendimentosExclusivos, linha => linha.CodigoAtivo == "LCIABERTO");
        Assert.Equal("06", exclusivoAberto.Codigo);
        Assert.Equal(
            $"Rendimento líquido de renda fixa — LCIABERTO — Letra aberta. {observacao}",
            exclusivoAberto.Descricao);
        var exclusivoFechado = Assert.Single(dirpf.RendimentosExclusivos, linha => linha.CodigoAtivo == "LCIFECHADA");
        Assert.Equal(
            $"Rendimento líquido de renda fixa — LCIFECHADA — Letra resgatada. {observacao}",
            exclusivoFechado.Descricao);
        Assert.DoesNotContain(dirpf.RendimentosExclusivos, linha => linha.CodigoAtivo == "CRIABERTO");
        Assert.Equal(
            "Rendimento líquido de renda fixa — CDB1 — CDB.",
            Assert.Single(dirpf.RendimentosExclusivos, linha => linha.CodigoAtivo == "CDB1").Descricao);

        var posicaoAberta = Assert.Single(dirpf.RendaFixa, linha => linha.Codigo == "LCIABERTO");
        Assert.Equal("Outro", posicaoAberta.Tipo);
        Assert.Equal("04", posicaoAberta.Grupo);
        Assert.Equal("02", posicaoAberta.CodigoDirpf);
        Assert.Equal(
            $"LCIABERTO — Letra aberta. Outro. Custo de aquisição em 31/12/2024. {observacao}",
            posicaoAberta.Discriminacao);
        Assert.Equal(
            $"CRIABERTO — CRI sem rendimento. Outro. Custo de aquisição em 31/12/2024. {observacao}",
            Assert.Single(dirpf.RendaFixa, linha => linha.Codigo == "CRIABERTO").Discriminacao);
        Assert.DoesNotContain(dirpf.RendaFixa, linha => linha.Codigo == "LCIFECHADA");
        Assert.Equal(
            "CDB1 — CDB. CDB. Custo de aquisição em 31/12/2024.",
            Assert.Single(dirpf.RendaFixa, linha => linha.Codigo == "CDB1").Discriminacao);
        Assert.Equal(
            "TESOURO — Tesouro. Tesouro Selic. Custo de aquisição em 31/12/2024.",
            Assert.Single(dirpf.RendaFixa, linha => linha.Codigo == "TESOURO").Discriminacao);

        using (var contexto = _fabrica.CreateDbContext())
        {
            contexto.Add(new MembroFamilia { Nome = "Ana", Avatar = "A" });
            foreach (var titulo in titulos)
            {
                titulo.Id = 0;
                contexto.Add(titulo);
            }

            contexto.SaveChanges();
            foreach (var operacao in operacoes)
            {
                operacao.Id = 0;
                operacao.TituloRendaFixaId = operacao.Titulo!.Id;
                contexto.Add(operacao);
            }

            contexto.SaveChanges();
        }

        using var destino = new MemoryStream();
        using (var contexto = _fabrica.CreateDbContext())
            ExportadorDirpf.Exportar(contexto, 2024, destino);

        using var pasta = new XLWorkbook(destino);
        var exclusivos = pasta.Worksheet(ExportadorDirpf.AbaRendimentosExclusivos);
        var rendaFixa = pasta.Worksheet(ExportadorDirpf.AbaRendaFixa);
        Assert.Equal(exclusivoAberto.Descricao, TextoDaLinha(exclusivos, "LCIABERTO", 3, 11));
        Assert.Equal(exclusivoFechado.Descricao, TextoDaLinha(exclusivos, "LCIFECHADA", 3, 11));
        Assert.Equal(
            "Rendimento líquido de renda fixa — CDB1 — CDB.",
            TextoDaLinha(exclusivos, "CDB1", 3, 11));
        Assert.Null(LinhaDoCodigo(exclusivos, "CRIABERTO", 3));
        Assert.Null(LinhaDoCodigo(exclusivos, "TESOURO", 3));
        Assert.Equal(posicaoAberta.Discriminacao, TextoDaLinha(rendaFixa, "LCIABERTO", 3, 16));
        Assert.Equal(
            $"CRIABERTO — CRI sem rendimento. Outro. Custo de aquisição em 31/12/2024. {observacao}",
            TextoDaLinha(rendaFixa, "CRIABERTO", 3, 16));
        Assert.Null(LinhaDoCodigo(rendaFixa, "LCIFECHADA", 3));
        Assert.Equal(
            "CDB1 — CDB. CDB. Custo de aquisição em 31/12/2024.",
            TextoDaLinha(rendaFixa, "CDB1", 3, 16));
        Assert.Equal(
            "TESOURO — Tesouro. Tesouro Selic. Custo de aquisição em 31/12/2024.",
            TextoDaLinha(rendaFixa, "TESOURO", 3, 16));
        Assert.DoesNotContain(observacao, TextoDaLinha(exclusivos, "CDB1", 3, 11));
        Assert.DoesNotContain(observacao, TextoDaLinha(rendaFixa, "TESOURO", 3, 16));
    }

    [Fact]
    public void Perfil_UsaOLancamentoQuandoHaUm_ENaoDuplicaQuandoNaoHa()
    {
        var ana = Perfil(1, "Ana", "Nica");
        var bob = Perfil(2, "Bob", null);
        var comDono = Titulo(1, "CDBDONO", "CDB", TipoTituloRendaFixa.CDB, "XP");
        var semDono = Titulo(2, "CDBSEMDONO", "CDB", TipoTituloRendaFixa.CDB, "XP");
        var operacoes = new[]
        {
            Operacao(1, comDono, new DateTime(2024, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 1m, 100m, 0m),
            Operacao(2, comDono, new DateTime(2024, 6, 1), TipoOperacaoRendaFixa.Resgate, 1m, 140m, 5m),
            Operacao(3, semDono, new DateTime(2024, 2, 1), TipoOperacaoRendaFixa.Aplicacao, 1m, 80m, 0m)
        };
        var lancamentos = new[]
        {
            new Transacao { OperacaoRendaFixaId = 2, MembroFamiliaId = ana.Id, MembroFamilia = ana }
        };

        var dirpf = Montar(2024, [], [comDono, semDono], operacoes, [ana, bob], lancamentos: lancamentos);
        Assert.Equal("Ana (Nica)", Assert.Single(dirpf.RendimentosExclusivos).Perfil);
        Assert.Equal(35m, dirpf.RendimentosExclusivos[0].ValorLiquido);
        Assert.Equal(5m, dirpf.RendimentosExclusivos[0].IrRetido);
        var aberta = Assert.Single(dirpf.RendaFixa);
        Assert.Equal("CDBSEMDONO", aberta.Codigo);
        Assert.Equal(MontadorDirpf.PerfilNaoIdentificado, aberta.Perfil);

        var soAna = Montar(2024, [], [semDono], operacoes.Where(operacao => operacao.TituloRendaFixaId == semDono.Id), [ana]);
        Assert.Equal("Ana (Nica)", Assert.Single(soAna.RendaFixa).Perfil);
    }

    [Fact]
    public void RegraLigada_RecusaEmVezDeCalcular()
    {
        var ana = Perfil(1, "Ana", null);
        var petr = Ativo(1, "PETR4", "Petrobras", TipoAtivo.Acao);
        var titulo = Titulo(1, "CDB1", "CDB", TipoTituloRendaFixa.CDB, "XP");
        var operacoes = new[]
        {
            Operacao(1, titulo, new DateTime(2024, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 1m, 100m, 0m)
        };

        var grossUp = Regras();
        grossUp.Single(regra => regra.Codigo == CodigosRegraFiscal.JcpGrossUp).Valor = 1m;
        var doJcp = Assert.Throws<InvalidOperationException>(() =>
            Montar(2024, [Jcp(ana, petr, new DateTime(2024, 5, 1), 85m)], regras: grossUp));
        Assert.Contains("RF.Jcp.GrossUp", doJcp.Message, StringComparison.Ordinal);

        var regressivo = Regras();
        regressivo.Single(regra => regra.Codigo == CodigosRegraFiscal.IrRegressivoAutomatico).Valor = 1m;
        var daRenda = Assert.Throws<InvalidOperationException>(() =>
            Montar(2024, [], [titulo], operacoes, [ana], regressivo));
        Assert.Contains("RF.IrRegressivo.Automatico", daRenda.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LucroContinuaReceitaEPrejuizoContinuaDespesa_ESoOLiquidoPositivoEntraNaExclusiva()
    {
        var servico = new RendaFixaService(_fabrica);
        int membroId;
        int ganhoId;
        int perdaId;
        using (var contexto = _fabrica.CreateDbContext())
        {
            var ana = new MembroFamilia { Nome = "Ana", Apelido = "Nica", Avatar = "A" };
            contexto.Add(ana);
            contexto.SaveChanges();
            membroId = ana.Id;
            contexto.TitulosRendaFixa.AddRange(
                new TituloRendaFixa { Codigo = "CDBGANHO", Nome = "CDB", Tipo = TipoTituloRendaFixa.CDB, Corretora = "XP" },
                new TituloRendaFixa { Codigo = "CDBPERDA", Nome = "CDB", Tipo = TipoTituloRendaFixa.CDB, Corretora = "XP" });
            contexto.SaveChanges();
            ganhoId = contexto.TitulosRendaFixa.Single(titulo => titulo.Codigo == "CDBGANHO").Id;
            perdaId = contexto.TitulosRendaFixa.Single(titulo => titulo.Codigo == "CDBPERDA").Id;
        }

        await servico.SalvarAsync(Formulario(ganhoId, new DateTime(2024, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 100m, 0m), membroId);
        await servico.SalvarAsync(Formulario(ganhoId, new DateTime(2024, 6, 1), TipoOperacaoRendaFixa.Resgate, 110m, 4m), membroId);
        await servico.SalvarAsync(Formulario(perdaId, new DateTime(2024, 1, 10), TipoOperacaoRendaFixa.Aplicacao, 100m, 0m), membroId);
        await servico.SalvarAsync(Formulario(perdaId, new DateTime(2024, 7, 1), TipoOperacaoRendaFixa.Resgate, 90m, 0m), membroId);

        using var leitura = _fabrica.CreateDbContext();
        var receita = Assert.Single(leitura.Transacoes, transacao => transacao.Descricao.Contains("CDBGANHO", StringComparison.Ordinal));
        var despesa = Assert.Single(leitura.Transacoes, transacao => transacao.Descricao.Contains("CDBPERDA", StringComparison.Ordinal));
        Assert.Equal(TipoTransacao.Receita, receita.Tipo);
        Assert.Equal(6m, receita.Valor);
        Assert.Equal(TipoTransacao.Despesa, despesa.Tipo);
        Assert.Equal(10m, despesa.Valor);

        var dirpf = MontadorDirpf.Montar(leitura, 2024);
        var exclusivo = Assert.Single(dirpf.RendimentosExclusivos);
        Assert.Equal("CDBGANHO", exclusivo.CodigoAtivo);
        Assert.Equal(6m, exclusivo.ValorLiquido);
        Assert.Equal(4m, exclusivo.IrRetido);
        Assert.Equal("Ana (Nica)", exclusivo.Perfil);
        Assert.Empty(dirpf.RendaFixa);
        Assert.Empty(dirpf.RendimentosIsentos);
    }

    private DirpfAnoBase Montar(
        int ano,
        IEnumerable<Provento>? proventos = null,
        IEnumerable<TituloRendaFixa>? titulos = null,
        IEnumerable<OperacaoRendaFixa>? operacoes = null,
        IEnumerable<MembroFamilia>? perfis = null,
        IEnumerable<RegraFiscal>? regras = null,
        IEnumerable<Transacao>? lancamentos = null)
    {
        return MontadorDirpf.Montar(
            ano,
            [],
            proventos ?? [],
            Codigos(),
            regras ?? Regras(),
            titulos ?? [],
            operacoes ?? [],
            lancamentos ?? [],
            perfis ?? []);
    }

    private List<CodigoDirpf> Codigos()
    {
        using var contexto = _fabrica.CreateDbContext();
        return contexto.CodigosDirpf.ToList();
    }

    private List<RegraFiscal> Regras()
    {
        using var contexto = _fabrica.CreateDbContext();
        return contexto.RegrasFiscais.ToList();
    }

    private static MembroFamilia Perfil(int id, string nome, string? apelido) => new()
    {
        Id = id,
        Nome = nome,
        Apelido = apelido,
        Avatar = "A"
    };

    private static AtivoFinanceiro Ativo(int id, string ticker, string nome, TipoAtivo tipo) => new()
    {
        Id = id,
        Ticker = ticker,
        Nome = nome,
        Tipo = tipo
    };

    private static TituloRendaFixa Titulo(int id, string codigo, string nome, TipoTituloRendaFixa tipo, string corretora) => new()
    {
        Id = id,
        Codigo = codigo,
        Nome = nome,
        Tipo = tipo,
        Corretora = corretora
    };

    private static OperacaoRendaFixa Operacao(
        int id,
        TituloRendaFixa titulo,
        DateTime data,
        TipoOperacaoRendaFixa tipo,
        decimal quantidade,
        decimal valor,
        decimal irRetido) => new()
    {
        Id = id,
        TituloRendaFixaId = titulo.Id,
        Titulo = titulo,
        Data = data,
        Tipo = tipo,
        Quantidade = quantidade,
        PrecoUnitario = 1m,
        Valor = valor,
        IrRetido = irRetido
    };

    private static Provento Jcp(MembroFamilia membro, AtivoFinanceiro ativo, DateTime pagamento, decimal valor)
        => Provento(membro, ativo, TipoProvento.Jcp, pagamento, valor);

    private static Provento Provento(
        MembroFamilia membro,
        AtivoFinanceiro ativo,
        TipoProvento tipo,
        DateTime pagamento,
        decimal valor) => new()
    {
        MembroFamiliaId = membro.Id,
        MembroFamilia = membro,
        AtivoId = ativo.Id,
        Ativo = ativo,
        Tipo = tipo,
        DataPagamento = pagamento,
        ValorLiquido = valor
    };

    private static string TextoDaLinha(IXLWorksheet aba, string codigo, int colunaCodigo, int colunaTexto)
    {
        var linha = LinhaDoCodigo(aba, codigo, colunaCodigo);
        Assert.NotNull(linha);
        return aba.Cell(linha.Value, colunaTexto).GetText();
    }

    private static int? LinhaDoCodigo(IXLWorksheet aba, string codigo, int colunaCodigo)
    {
        var ultima = aba.LastRowUsed()?.RowNumber() ?? 1;
        for (var numero = 2; numero <= ultima; numero++)
        {
            if (aba.Cell(numero, colunaCodigo).GetText() == codigo)
                return numero;
        }

        return null;
    }

    private static OperacaoRendaFixaFormModel Formulario(
        int tituloId,
        DateTime data,
        TipoOperacaoRendaFixa tipo,
        decimal valor,
        decimal irRetido) => new()
    {
        TituloRendaFixaId = tituloId,
        Data = data,
        Tipo = tipo,
        Quantidade = 1m,
        PrecoUnitario = 1m,
        Valor = valor,
        IrRetido = irRetido
    };
}
