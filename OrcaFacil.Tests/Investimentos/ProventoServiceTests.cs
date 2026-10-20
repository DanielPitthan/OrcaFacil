using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Lancamentos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Services;

namespace OrcaFacil.Tests.Investimentos;

public class ProventoServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly ProventoService _servico;
    private readonly ResultadoRealizadoService _resultado;
    private readonly int _membroId;
    private readonly int _petroId;

    public ProventoServiceTests()
    {
        _servico = new ProventoService(_factory);
        _resultado = new ResultadoRealizadoService(_factory);
        using var contexto = _factory.CreateDbContext();
        var membro = new MembroFamilia { Nome = "Usuário", Avatar = "U" };
        var petro = new AtivoFinanceiro { Ticker = "PETR4", Nome = "Petrobras", Tipo = TipoAtivo.Acao };
        contexto.AddRange(membro, petro);
        contexto.SaveChanges();
        _membroId = membro.Id;
        _petroId = petro.Id;
    }

    [Fact]
    public async Task Provento_ViraReceitaNoMesDoPagamento_NaCategoriaProventos()
    {
        await _resultado.SalvarAsync(Operacao(_petroId, TipoOperacao.Compra, new DateTime(2026, 1, 10), 10, 10m), _membroId);
        await _resultado.SalvarAsync(Operacao(_petroId, TipoOperacao.Venda, new DateTime(2026, 2, 10), 10, 12m), _membroId);

        var rendimento = await _servico.SalvarAsync(
            Provento(_petroId, TipoProvento.Rendimento, new DateTime(2026, 3, 15), 18.40m), _membroId);
        var jcp = await _servico.SalvarAsync(
            Provento(_petroId, TipoProvento.Jcp, new DateTime(2026, 5, 2), 7.25m), _membroId);

        using var contexto = _factory.CreateDbContext();
        var venda = contexto.Transacoes.Single(t => t.OperacaoAtivoId != null);
        Assert.Equal(20m, venda.Valor);
        Assert.Equal(new DateTime(2026, 2, 10), venda.Data);

        var doRendimento = contexto.Transacoes.Single(t => t.ProventoId == rendimento.Id);
        Assert.Equal(TipoTransacao.Receita, doRendimento.Tipo);
        Assert.Equal(18.40m, doRendimento.Valor);
        Assert.Equal(new DateTime(2026, 3, 15), doRendimento.Data);
        Assert.Equal(3, doRendimento.Data.Month);
        Assert.Equal(2026, doRendimento.Data.Year);
        Assert.Equal("Rendimento — PETR4", doRendimento.Descricao);
        Assert.True(doRendimento.Pago);
        Assert.Equal(TipoRecorrencia.Unica, doRendimento.Recorrencia);
        Assert.Empty(contexto.ItensLancamento);

        var doJcp = contexto.Transacoes.Single(t => t.ProventoId == jcp.Id);
        Assert.Equal(TipoTransacao.Receita, doJcp.Tipo);
        Assert.Equal(7.25m, doJcp.Valor);
        Assert.Equal(new DateTime(2026, 5, 2), doJcp.Data);
        Assert.Equal("JCP — PETR4", doJcp.Descricao);

        var proventos = contexto.Categorias.Single(c => c.Nome == LancamentoResultadoAtivo.CategoriaProventos);
        var ganho = contexto.Categorias.Single(c => c.Nome == ResultadoRealizadoService.CategoriaGanho);
        Assert.Equal(TipoTransacao.Receita, proventos.TipoPadrao);
        Assert.True(proventos.EhPadraoDoSistema);
        Assert.False(proventos.PermiteItensFatura);
        Assert.Equal(proventos.Id, doRendimento.CategoriaId);
        Assert.Equal(proventos.Id, doJcp.CategoriaId);
        Assert.NotEqual(ganho.Id, proventos.Id);
        Assert.Equal(ganho.Id, venda.CategoriaId);
        Assert.Equal(1, contexto.Categorias.Count(c => c.Nome == LancamentoResultadoAtivo.CategoriaProventos));

        var mesmo = await _servico.SalvarAsync(
            Provento(_petroId, TipoProvento.Rendimento, new DateTime(2026, 4, 1), 30m, rendimento.Id), _membroId);
        Assert.Equal(rendimento.Id, mesmo.Id);

        using var depois = _factory.CreateDbContext();
        var atualizado = depois.Transacoes.Single(t => t.ProventoId == rendimento.Id);
        Assert.Equal(doRendimento.Id, atualizado.Id);
        Assert.Equal(30m, atualizado.Valor);
        Assert.Equal(new DateTime(2026, 4, 1), atualizado.Data);
        Assert.Equal(4, atualizado.Data.Month);
        Assert.Equal(2, depois.Proventos.Count());
        Assert.Equal(3, depois.Transacoes.Count());
    }

    [Fact]
    public async Task MesFechado_BloqueiaGravacaoEdicaoEExclusao_ENaoGravaNada()
    {
        var gravado = await _servico.SalvarAsync(
            Provento(_petroId, TipoProvento.Rendimento, new DateTime(2026, 3, 15), 18.40m), _membroId);

        using (var contexto = _factory.CreateDbContext())
        {
            contexto.FechamentosPeriodo.Add(new FechamentoPeriodo { Mes = 4, Ano = 2026, Fechado = true });
            contexto.SaveChanges();
        }

        var novo = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _servico.SalvarAsync(Provento(_petroId, TipoProvento.Jcp, new DateTime(2026, 4, 10), 9m), _membroId));
        Assert.Contains("Mês fechado", novo.Message);
        Assert.Contains("Nada foi gravado", novo.Message);

        using (var contexto = _factory.CreateDbContext())
        {
            contexto.FechamentosPeriodo.Add(new FechamentoPeriodo { Mes = 3, Ano = 2026, Fechado = true });
            contexto.SaveChanges();
        }

        var edicao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _servico.SalvarAsync(Provento(_petroId, TipoProvento.Rendimento, new DateTime(2026, 3, 15), 40m, gravado.Id), _membroId));
        Assert.Contains("Mês fechado", edicao.Message);
        Assert.Contains("Nada foi gravado", edicao.Message);

        var mudancaDeMes = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _servico.SalvarAsync(Provento(_petroId, TipoProvento.Rendimento, new DateTime(2026, 6, 1), 18.40m, gravado.Id), _membroId));
        Assert.Contains("Mês fechado", mudancaDeMes.Message);

        var exclusao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _servico.ExcluirAsync(gravado.Id, _membroId));
        Assert.Contains("Mês fechado", exclusao.Message);
        Assert.Contains("Nada foi gravado", exclusao.Message);

        using var depois = _factory.CreateDbContext();
        var provento = Assert.Single(depois.Proventos);
        Assert.Equal(18.40m, provento.ValorLiquido);
        Assert.Equal(new DateTime(2026, 3, 15), provento.DataPagamento);
        Assert.Equal(TipoProvento.Rendimento, provento.Tipo);
        var lancamento = Assert.Single(depois.Transacoes);
        Assert.Equal(18.40m, lancamento.Valor);
        Assert.Equal(provento.Id, lancamento.ProventoId);
        Assert.Null(lancamento.AtualizadoEm);
    }

    [Fact]
    public async Task HashDuplicado_ERejeitado_ENaoGravaOSegundo()
    {
        var primeiro = Provento(_petroId, TipoProvento.Rendimento, new DateTime(2026, 3, 15), 18.40m);
        primeiro.Origem = OrigemProvento.ExtratoB3;
        primeiro.HashImportacao = "hash-do-provento";
        await _servico.SalvarAsync(primeiro, _membroId);

        var repetido = Provento(_petroId, TipoProvento.Jcp, new DateTime(2026, 4, 2), 3m);
        repetido.Origem = OrigemProvento.ExtratoB3;
        repetido.HashImportacao = "hash-do-provento";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _servico.SalvarAsync(repetido, _membroId));
        Assert.Equal(ProventoService.MensagemHashRepetido, ex.Message);

        using var contexto = _factory.CreateDbContext();
        var provento = Assert.Single(contexto.Proventos);
        Assert.Equal(18.40m, provento.ValorLiquido);
        Assert.Equal(TipoProvento.Rendimento, provento.Tipo);
        Assert.Equal(provento.Id, Assert.Single(contexto.Transacoes).ProventoId);
    }

    [Fact]
    public async Task DoisManuaisIguais_Coexistem_EHashNuloNaoColide()
    {
        var manual = Provento(_petroId, TipoProvento.Rendimento, new DateTime(2026, 3, 15), 18.40m);
        await _servico.SalvarAsync(manual, _membroId);
        manual.HashImportacao = "   ";
        await _servico.SalvarAsync(manual, _membroId);

        var importado = Provento(_petroId, TipoProvento.Rendimento, new DateTime(2026, 3, 15), 18.40m);
        importado.Origem = OrigemProvento.ExtratoB3;
        importado.HashImportacao = new string('A', 64);
        var gravado = await _servico.SalvarAsync(importado, _membroId);

        await _servico.SalvarAsync(
            Provento(_petroId, TipoProvento.Rendimento, new DateTime(2026, 3, 20), 1m, gravado.Id), _membroId);

        using var contexto = _factory.CreateDbContext();
        Assert.Equal(3, contexto.Proventos.Count());
        Assert.Equal(2, contexto.Proventos.Count(p => p.HashImportacao == null && p.Origem == OrigemProvento.Manual));
        var importadoAtual = contexto.Proventos.Single(p => p.Origem == OrigemProvento.ExtratoB3);
        Assert.Equal(new string('A', 64), importadoAtual.HashImportacao);
        Assert.Equal(1m, importadoAtual.ValorLiquido);
        Assert.Equal(3, contexto.Transacoes.Count());
        Assert.Equal(3, contexto.Transacoes.Count(t => t.Tipo == TipoTransacao.Receita && t.ProventoId != null));
    }

    [Fact]
    public async Task ValorLiquido_ArredondaParaDuasCasas()
    {
        await _servico.SalvarAsync(
            Provento(_petroId, TipoProvento.Rendimento, new DateTime(2026, 3, 15), 1.005m), _membroId);

        using var contexto = _factory.CreateDbContext();
        var provento = Assert.Single(contexto.Proventos);
        Assert.Equal(1.00m, provento.ValorLiquido);
        Assert.Equal(1.00m, Assert.Single(contexto.Transacoes).Valor);
    }

    [Fact]
    public async Task ExcluirEmMesAberto_RemoveProventoELancamento()
    {
        var gravado = await _servico.SalvarAsync(
            Provento(_petroId, TipoProvento.Rendimento, new DateTime(2026, 3, 15), 18.40m), _membroId);
        await _servico.ExcluirAsync(gravado.Id, _membroId);

        using var contexto = _factory.CreateDbContext();
        Assert.Empty(contexto.Proventos);
        Assert.Empty(contexto.Transacoes);
    }

    [Fact]
    public void LancamentoGeradoPeloProvento_NaoSeEditaPelaTela()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ProtecaoLancamentoOperacao.GarantirEditavelNaTela(null, null, 4));
        Assert.Equal(ProtecaoLancamentoOperacao.MensagemProvento, ex.Message);
        ProtecaoLancamentoOperacao.GarantirEditavelNaTela(null, null, null);
    }

    [Fact]
    public void ServicoNaoRecalculaOResultadoDaVenda()
    {
        var raiz = EncontrarRaiz();
        var servico = File.ReadAllText(Path.Combine(raiz, "OrcaFacil.Core", "Services", "ProventoService.cs"));
        var resultado = File.ReadAllText(Path.Combine(raiz, "OrcaFacil.Core", "Services", "ResultadoRealizadoService.cs"));
        Assert.Contains("LancamentoResultadoAtivo", servico, StringComparison.Ordinal);
        Assert.Contains("LancamentoResultadoAtivo", resultado, StringComparison.Ordinal);
        Assert.DoesNotContain("CalculadoraResultadoRealizado", servico, StringComparison.Ordinal);
        Assert.DoesNotContain("new Categoria", servico, StringComparison.Ordinal);
        Assert.DoesNotContain("new Categoria", resultado, StringComparison.Ordinal);
    }

    public void Dispose() => _factory.Dispose();

    private static ProventoFormModel Provento(
        int ativoId,
        TipoProvento tipo,
        DateTime data,
        decimal valor,
        int? id = null) => new()
    {
        Id = id,
        AtivoId = ativoId,
        Tipo = tipo,
        DataPagamento = data,
        ValorLiquido = valor
    };

    private static OperacaoAtivoFormModel Operacao(
        int ativoId,
        TipoOperacao tipo,
        DateTime data,
        decimal quantidade,
        decimal preco) => new()
    {
        AtivoId = ativoId,
        Tipo = tipo,
        Data = data,
        Quantidade = quantidade,
        PrecoUnitario = preco
    };

    private static string EncontrarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OrcaFacil.Core", "Services", "ProventoService.cs")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar o serviço de proventos.");
    }
}
