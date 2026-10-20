using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Services;

namespace OrcaFacil.Tests.Investimentos;

public class ResultadoRealizadoServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly ResultadoRealizadoService _servico;
    private readonly int _membroId;
    private readonly int _petroId;

    public ResultadoRealizadoServiceTests()
    {
        _servico = new ResultadoRealizadoService(_factory);
        using var contexto = _factory.CreateDbContext();
        var membro = new MembroFamilia { Nome = "Usuário", Avatar = "U" };
        var petro = new AtivoFinanceiro { Ticker = "PETR4", Nome = "Petrobras", Tipo = TipoAtivo.Acao };
        contexto.AddRange(membro, petro);
        contexto.SaveChanges();
        _membroId = membro.Id;
        _petroId = petro.Id;
    }

    [Fact]
    public async Task TelaManual_PosicaoDepoisDoServico_IgualaOCalculoAnterior()
    {
        var movimentos = new[]
        {
            Movimento(1, new DateTime(2026, 1, 10), TipoOperacao.Compra, 100, 10m, 10m),
            Movimento(2, new DateTime(2026, 2, 10), TipoOperacao.Compra, 100, 12m, 0m),
            Movimento(3, new DateTime(2026, 3, 10), TipoOperacao.Venda, 50, 15m, 0m)
        };
        var antes = PosicaoCalculator.Calcular(movimentos);

        await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Compra, new DateTime(2026, 1, 10), 100, 10m, 10m), _membroId);
        await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Compra, new DateTime(2026, 2, 10), 100, 12m, 0m), _membroId);
        await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Venda, new DateTime(2026, 3, 10), 50, 15m, 0m), _membroId);

        using var contexto = _factory.CreateDbContext();
        var gravadas = contexto.OperacoesAtivo.OrderBy(o => o.Id).ToList();
        var depois = PosicaoCalculator.Calcular(gravadas.Select(ParaMovimento));

        Assert.Equal(antes.Quantidade, depois.Quantidade);
        Assert.Equal(antes.PrecoMedio, depois.PrecoMedio);
        Assert.Equal(antes.CustoTotal, depois.CustoTotal);
        Assert.Equal(150m, depois.Quantidade);
        Assert.Equal(11.05m, depois.PrecoMedio);
        Assert.Equal(1657.50m, depois.CustoTotal);
        Assert.Equal(750m, gravadas.Single(o => o.Tipo == TipoOperacao.Venda).ValorOperacao);

        var lancamento = Assert.Single(contexto.Transacoes);
        Assert.Equal(TipoTransacao.Receita, lancamento.Tipo);
        Assert.Equal(197.50m, lancamento.Valor);
        Assert.Equal(new DateTime(2026, 3, 10), lancamento.Data);
        Assert.Equal("Ganho realizado em ativos — PETR4", lancamento.Descricao);
        Assert.True(lancamento.Pago);
        Assert.Equal(TipoRecorrencia.Unica, lancamento.Recorrencia);
        Assert.Empty(contexto.ItensLancamento);
        Assert.Equal(gravadas.Single(o => o.Tipo == TipoOperacao.Venda).Id, lancamento.OperacaoAtivoId);
        Assert.Equal("Ganho realizado em ativos", contexto.Categorias.Single(c => c.Id == lancamento.CategoriaId).Nome);
    }

    [Fact]
    public async Task CustosDaVenda_NaoMudamAPosicao_EEntramNoResultadoPelaRegra()
    {
        var semCustoNaVenda = new[]
        {
            Movimento(1, new DateTime(2026, 1, 10), TipoOperacao.Compra, 100, 10m, 10m),
            Movimento(2, new DateTime(2026, 2, 10), TipoOperacao.Compra, 100, 12m, 0m),
            Movimento(3, new DateTime(2026, 3, 10), TipoOperacao.Venda, 50, 15m, 0m)
        };
        var comCustoNaVenda = new[]
        {
            Movimento(1, new DateTime(2026, 1, 10), TipoOperacao.Compra, 100, 10m, 10m),
            Movimento(2, new DateTime(2026, 2, 10), TipoOperacao.Compra, 100, 12m, 0m),
            Movimento(3, new DateTime(2026, 3, 10), TipoOperacao.Venda, 50, 15m, 4m)
        };

        Assert.Equal(PosicaoCalculator.Calcular(semCustoNaVenda).CustoTotal, PosicaoCalculator.Calcular(comCustoNaVenda).CustoTotal);
        Assert.Equal(PosicaoCalculator.Calcular(semCustoNaVenda).PrecoMedio, PosicaoCalculator.Calcular(comCustoNaVenda).PrecoMedio);

        await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Compra, new DateTime(2026, 1, 10), 100, 10m, 10m), _membroId);
        await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Compra, new DateTime(2026, 2, 10), 100, 12m, 0m), _membroId);
        await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Venda, new DateTime(2026, 3, 10), 50, 15m, 4m), _membroId);

        using var contexto = _factory.CreateDbContext();
        var depois = PosicaoCalculator.Calcular(contexto.OperacoesAtivo.ToList().Select(ParaMovimento));
        Assert.Equal(1657.50m, depois.CustoTotal);
        Assert.Equal(193.50m, Assert.Single(contexto.Transacoes).Valor);
    }

    [Fact]
    public async Task ValorOperacaoInformado_DiferenteDeQuantidadeVezesPreco_EntraNoResultado()
    {
        await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Compra, new DateTime(2026, 1, 10), 10, 10m, 0m), _membroId);

        var venda = Operacao(_petroId, TipoOperacao.Venda, new DateTime(2026, 3, 10), 10, 15.01m, 0m);
        venda.ValorOperacao = 149.97m;
        await _servico.SalvarAsync(venda, _membroId);

        using var contexto = _factory.CreateDbContext();
        var gravada = contexto.OperacoesAtivo.Single(o => o.Tipo == TipoOperacao.Venda);
        Assert.Equal(15.01m, gravada.PrecoUnitario);
        Assert.Equal(150.10m, gravada.Quantidade * gravada.PrecoUnitario);
        Assert.Equal(149.97m, gravada.ValorOperacao);

        var posicao = PosicaoCalculator.Calcular(contexto.OperacoesAtivo.ToList().Select(ParaMovimento));
        Assert.Equal(0m, posicao.Quantidade);
        Assert.Equal(0m, posicao.CustoTotal);

        var lancamento = Assert.Single(contexto.Transacoes);
        Assert.Equal(TipoTransacao.Receita, lancamento.Tipo);
        Assert.Equal(49.97m, lancamento.Valor);
    }

    [Fact]
    public async Task Prejuizo_ViraDespesa_ECompraNaoGeraLancamento()
    {
        await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Compra, new DateTime(2026, 1, 10), 100, 10m, 10m), _membroId);
        await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Compra, new DateTime(2026, 2, 10), 100, 12m, 0m), _membroId);

        using (var antes = _factory.CreateDbContext())
            Assert.Empty(antes.Transacoes);

        await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Venda, new DateTime(2026, 3, 10), 50, 10m, 0m), _membroId);

        using var contexto = _factory.CreateDbContext();
        var lancamento = Assert.Single(contexto.Transacoes);
        Assert.Equal(TipoTransacao.Despesa, lancamento.Tipo);
        Assert.Equal(52.50m, lancamento.Valor);
        Assert.Equal("Perda realizada em ativos — PETR4", lancamento.Descricao);
        Assert.Equal("Perda realizada em ativos", contexto.Categorias.Single(c => c.Id == lancamento.CategoriaId).Nome);
    }

    [Fact]
    public async Task Editar_AtualizaAMesmaLinha_ReprocessarNaoDuplica_ExcluirRemove()
    {
        await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Compra, new DateTime(2026, 1, 10), 100, 10m, 0m), _membroId);
        var venda = await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Venda, new DateTime(2026, 2, 10), 40, 12m, 0m), _membroId);

        int idLancamento;
        using (var contexto = _factory.CreateDbContext())
            idLancamento = Assert.Single(contexto.Transacoes).Id;

        await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Venda, new DateTime(2026, 2, 10), 40, 8m, 0m, venda.Id), _membroId);
        await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Venda, new DateTime(2026, 2, 10), 40, 8m, 0m, venda.Id), _membroId);

        using (var contexto = _factory.CreateDbContext())
        {
            var lancamento = Assert.Single(contexto.Transacoes);
            Assert.Equal(idLancamento, lancamento.Id);
            Assert.Equal(TipoTransacao.Despesa, lancamento.Tipo);
            Assert.Equal(80m, lancamento.Valor);
            Assert.Equal(venda.Id, lancamento.OperacaoAtivoId);
        }

        await _servico.ExcluirAsync(venda.Id, _membroId);

        using var depois = _factory.CreateDbContext();
        Assert.Empty(depois.Transacoes);
        Assert.DoesNotContain(depois.OperacoesAtivo, o => o.Id == venda.Id);
    }

    [Fact]
    public async Task ResultadoZero_RemoveOLancamento()
    {
        await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Compra, new DateTime(2026, 1, 10), 100, 10m, 0m), _membroId);
        var venda = await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Venda, new DateTime(2026, 2, 10), 40, 12m, 0m), _membroId);
        await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Venda, new DateTime(2026, 2, 10), 40, 10m, 0m, venda.Id), _membroId);

        using var contexto = _factory.CreateDbContext();
        Assert.Empty(contexto.Transacoes);
        Assert.Equal(2, contexto.OperacoesAtivo.Count());
    }

    [Fact]
    public async Task DoisManuaisIguais_NaoColidem_EHashNuloNaoColideComImportada()
    {
        var manual = Operacao(_petroId, TipoOperacao.Compra, new DateTime(2026, 1, 10), 10, 10m, 0m);
        await _servico.SalvarAsync(manual, _membroId);
        await _servico.SalvarAsync(manual, _membroId);

        var importada = Operacao(_petroId, TipoOperacao.Compra, new DateTime(2026, 1, 10), 10, 10m, 0m);
        importada.Origem = OrigemOperacaoAtivo.ExtratoB3;
        importada.HashImportacao = new string('A', 64);
        await _servico.SalvarAsync(importada, _membroId);

        using var contexto = _factory.CreateDbContext();
        Assert.Equal(3, contexto.OperacoesAtivo.Count());
        Assert.Equal(2, contexto.OperacoesAtivo.Count(o => o.HashImportacao == null && o.Origem == OrigemOperacaoAtivo.Manual));
        Assert.Equal(1, contexto.OperacoesAtivo.Count(o => o.Origem == OrigemOperacaoAtivo.ExtratoB3));
    }

    [Fact]
    public async Task HashDuplicado_ERejeitado_ENaoGravaASegunda()
    {
        var primeira = Operacao(_petroId, TipoOperacao.Compra, new DateTime(2026, 1, 10), 10, 10m, 0m);
        primeira.Origem = OrigemOperacaoAtivo.ExtratoB3;
        primeira.HashImportacao = "hash-da-linha";
        await _servico.SalvarAsync(primeira, _membroId);

        var repetida = Operacao(_petroId, TipoOperacao.Compra, new DateTime(2026, 2, 10), 5, 11m, 0m);
        repetida.Origem = OrigemOperacaoAtivo.ExtratoB3;
        repetida.HashImportacao = "hash-da-linha";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _servico.SalvarAsync(repetida, _membroId));
        Assert.Equal(ResultadoRealizadoService.MensagemHashRepetido, ex.Message);

        using var contexto = _factory.CreateDbContext();
        Assert.Equal(1, contexto.OperacoesAtivo.Count());
        Assert.Empty(contexto.Transacoes);
    }

    [Fact]
    public async Task EditarImportada_PreservaOrigemEHash()
    {
        var modelo = Operacao(_petroId, TipoOperacao.Compra, new DateTime(2026, 1, 10), 10, 10m, 0m);
        modelo.Origem = OrigemOperacaoAtivo.ExtratoB3;
        modelo.HashImportacao = "hash-preservado";
        var gravada = await _servico.SalvarAsync(modelo, _membroId);

        await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Compra, new DateTime(2026, 1, 11), 12, 11m, 1m, gravada.Id), _membroId);

        using var contexto = _factory.CreateDbContext();
        var atual = Assert.Single(contexto.OperacoesAtivo);
        Assert.Equal(OrigemOperacaoAtivo.ExtratoB3, atual.Origem);
        Assert.Equal("hash-preservado", atual.HashImportacao);
        Assert.Equal(12m, atual.Quantidade);
        Assert.Equal(new DateTime(2026, 1, 11), atual.Data);
    }

    [Fact]
    public async Task MesFechado_BloqueiaAlteracaoDoLancamento_ENaoGrava()
    {
        await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Compra, new DateTime(2026, 3, 10), 100, 10m, 0m), _membroId);
        var venda = await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Venda, new DateTime(2026, 4, 10), 40, 12m, 0m), _membroId);
        using (var contexto = _factory.CreateDbContext())
        {
            contexto.FechamentosPeriodo.Add(new FechamentoPeriodo { Mes = 4, Ano = 2026, Fechado = true });
            contexto.SaveChanges();
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Venda, new DateTime(2026, 4, 10), 40, 20m, 0m, venda.Id), _membroId));
        Assert.Contains("Mês fechado", ex.Message);
        Assert.Contains("Nada foi gravado", ex.Message);

        using var depois = _factory.CreateDbContext();
        Assert.Equal(12m, depois.OperacoesAtivo.Single(o => o.Id == venda.Id).PrecoUnitario);
        Assert.Equal(80m, Assert.Single(depois.Transacoes).Valor);
    }

    [Fact]
    public async Task MesFechado_PermiteCorretoraQuandoOLancamentoNaoMuda()
    {
        await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Compra, new DateTime(2026, 3, 10), 100, 10m, 0m), _membroId);
        var venda = await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Venda, new DateTime(2026, 4, 10), 40, 12m, 0m), _membroId);
        using (var contexto = _factory.CreateDbContext())
        {
            contexto.FechamentosPeriodo.Add(new FechamentoPeriodo { Mes = 4, Ano = 2026, Fechado = true });
            contexto.SaveChanges();
        }

        var ajuste = Operacao(_petroId, TipoOperacao.Venda, new DateTime(2026, 4, 10), 40, 12m, 0m, venda.Id);
        ajuste.Corretora = "XP";
        await _servico.SalvarAsync(ajuste, _membroId);

        using var depois = _factory.CreateDbContext();
        Assert.Equal("XP", depois.OperacoesAtivo.Single(o => o.Id == venda.Id).Corretora);
        var lancamento = Assert.Single(depois.Transacoes);
        Assert.Equal(80m, lancamento.Valor);
        Assert.Null(lancamento.AtualizadoEm);
    }

    [Fact]
    public async Task CompraRetroativa_QueMudariaResultadoEmMesFechado_NaoGrava()
    {
        await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Compra, new DateTime(2026, 3, 10), 100, 10m, 0m), _membroId);
        await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Venda, new DateTime(2026, 4, 10), 40, 12m, 0m), _membroId);
        using (var contexto = _factory.CreateDbContext())
        {
            contexto.FechamentosPeriodo.Add(new FechamentoPeriodo { Mes = 4, Ano = 2026, Fechado = true });
            contexto.SaveChanges();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Compra, new DateTime(2026, 3, 1), 100, 30m, 0m), _membroId));

        using var depois = _factory.CreateDbContext();
        Assert.Equal(2, depois.OperacoesAtivo.Count());
        Assert.Equal(80m, Assert.Single(depois.Transacoes).Valor);
    }

    [Fact]
    public async Task VendaNovaEmMesFechado_NaoGrava()
    {
        await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Compra, new DateTime(2026, 3, 10), 100, 10m, 0m), _membroId);
        using (var contexto = _factory.CreateDbContext())
        {
            contexto.FechamentosPeriodo.Add(new FechamentoPeriodo { Mes = 4, Ano = 2026, Fechado = true });
            contexto.SaveChanges();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Venda, new DateTime(2026, 4, 10), 10, 12m, 0m), _membroId));

        using var depois = _factory.CreateDbContext();
        Assert.Equal(1, depois.OperacoesAtivo.Count());
        Assert.Empty(depois.Transacoes);
    }

    [Fact]
    public async Task SemRegraVigente_RecusaAVenda_ENaoGrava()
    {
        await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Compra, new DateTime(2026, 1, 10), 10, 10m, 0m), _membroId);
        using (var contexto = _factory.CreateDbContext())
        {
            var aliquota = contexto.RegrasFiscais.Single(r => r.Codigo == CodigosRegraFiscal.AliquotaAcao);
            contexto.RegrasFiscais.Remove(aliquota);
            contexto.SaveChanges();
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Venda, new DateTime(2026, 2, 10), 5, 12m, 0m), _membroId));
        Assert.Contains("Não há regra fiscal vigente", ex.Message);

        using var depois = _factory.CreateDbContext();
        Assert.Equal(1, depois.OperacoesAtivo.Count());
        Assert.Empty(depois.Transacoes);
    }

    [Fact]
    public async Task RegrasDaTabela_MudamCustosCasasENaoAplicamAliquota()
    {
        using (var contexto = _factory.CreateDbContext())
        {
            contexto.RegrasFiscais.Single(r => r.Codigo == CodigosRegraFiscal.CustosReduzemResultado).Valor = 0m;
            contexto.RegrasFiscais.Single(r => r.Codigo == CodigosRegraFiscal.CasasDecimaisResultado).Valor = 0m;
            contexto.RegrasFiscais.Single(r => r.Codigo == CodigosRegraFiscal.AliquotaAcao).Valor = 0.99m;
            contexto.SaveChanges();
        }

        await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Compra, new DateTime(2026, 1, 10), 1, 10m, 0m), _membroId);
        await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Venda, new DateTime(2026, 2, 10), 1, 10.5m, 4m), _membroId);

        using var depois = _factory.CreateDbContext();
        Assert.Equal(1m, Assert.Single(depois.Transacoes).Valor);
    }

    [Fact]
    public async Task VendaAcimaDaPosicao_NaoGrava_ETrocarDeAtivoSemPosicaoTambemNao()
    {
        await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Compra, new DateTime(2026, 1, 10), 10, 10m, 0m), _membroId);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Venda, new DateTime(2026, 2, 10), 11, 12m, 0m), _membroId));
        Assert.Contains("excede a quantidade", ex.Message);

        var venda = await _servico.SalvarAsync(Operacao(_petroId, TipoOperacao.Venda, new DateTime(2026, 2, 10), 4, 12m, 0m), _membroId);
        int valeId;
        using (var contexto = _factory.CreateDbContext())
        {
            var vale = new AtivoFinanceiro { Ticker = "VALE3", Nome = "Vale", Tipo = TipoAtivo.Acao };
            contexto.AtivosFinanceiros.Add(vale);
            contexto.SaveChanges();
            valeId = vale.Id;
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _servico.SalvarAsync(Operacao(valeId, TipoOperacao.Venda, new DateTime(2026, 2, 10), 4, 12m, 0m, venda.Id), _membroId));

        using var depois = _factory.CreateDbContext();
        Assert.Equal(_petroId, depois.OperacoesAtivo.Single(o => o.Id == venda.Id).AtivoId);
        Assert.Equal(8m, Assert.Single(depois.Transacoes).Valor);
    }

    [Fact]
    public void LancamentoGerado_NaoSeEditaPelaTela()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ProtecaoLancamentoAtivo.GarantirEditavelNaTela(4));
        Assert.Equal(ProtecaoLancamentoAtivo.Mensagem, ex.Message);
        ProtecaoLancamentoAtivo.GarantirEditavelNaTela(null);
    }

    [Fact]
    public void Calculadora_NaoTemAliquotaFixa()
    {
        var raiz = EncontrarRaiz();
        var calculadora = File.ReadAllText(Path.Combine(raiz, "OrcaFacil.Core", "Ativos", "CalculadoraResultadoRealizado.cs"));
        var servico = File.ReadAllText(Path.Combine(raiz, "OrcaFacil.Core", "Services", "ResultadoRealizadoService.cs"));
        Assert.DoesNotContain("0.15", calculadora, StringComparison.Ordinal);
        Assert.DoesNotContain("0.20", calculadora, StringComparison.Ordinal);
        Assert.DoesNotContain("0.15", servico, StringComparison.Ordinal);
        Assert.DoesNotContain("0.20", servico, StringComparison.Ordinal);
    }

    [Fact]
    public void MesmoDia_MantemOrdemPorId_CompraNovaNaoPulaNaFrente()
    {
        var vendaAntes = new[]
        {
            Movimento(1, new DateTime(2026, 1, 10), TipoOperacao.Venda, 1, 12m, 0m),
            Movimento(2, new DateTime(2026, 1, 10), TipoOperacao.Compra, 1, 10m, 0m)
        };
        Assert.Throws<InvalidOperationException>(() => PosicaoCalculator.Calcular(vendaAntes));

        var compraAntes = PosicaoCalculator.Calcular(new[]
        {
            Movimento(1, new DateTime(2026, 1, 10), TipoOperacao.Compra, 1, 10m, 0m),
            Movimento(2, new DateTime(2026, 1, 10), TipoOperacao.Venda, 1, 12m, 0m)
        });
        Assert.Equal(0m, compraAntes.Quantidade);
    }

    public void Dispose() => _factory.Dispose();

    private static OperacaoAtivoFormModel Operacao(
        int ativoId,
        TipoOperacao tipo,
        DateTime data,
        decimal quantidade,
        decimal preco,
        decimal custos,
        int? id = null) => new()
    {
        Id = id,
        AtivoId = ativoId,
        Tipo = tipo,
        Data = data,
        Quantidade = quantidade,
        PrecoUnitario = preco,
        Custos = custos
    };

    private static MovimentoPosicao Movimento(int id, DateTime data, TipoOperacao tipo, decimal quantidade, decimal preco, decimal custos)
        => new() { Id = id, Data = data, Tipo = tipo, Quantidade = quantidade, PrecoUnitario = preco, Custos = custos };

    private static MovimentoPosicao ParaMovimento(OperacaoAtivo operacao) => new()
    {
        Id = operacao.Id,
        Data = operacao.Data,
        Tipo = operacao.Tipo,
        Quantidade = operacao.Quantidade,
        PrecoUnitario = operacao.PrecoUnitario,
        Custos = operacao.Custos
    };

    private static string EncontrarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OrcaFacil.Core", "Ativos", "CalculadoraResultadoRealizado.cs")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar a calculadora.");
    }
}
