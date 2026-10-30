using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Core.Services;
using Xunit;

namespace OrcaFacil.Tests.Ativos;

public class CadastroAtivoServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly AtivoFinanceiroRepository _ativos;
    private readonly OperacaoAtivoRepository _operacoes;
    private readonly AtivoCadastroService _ativosService;
    private readonly ResultadoRealizadoService _operacoesService;
    private readonly int _membroId;

    public CadastroAtivoServiceTests()
    {
        _ativos = new AtivoFinanceiroRepository(_factory);
        _operacoes = new OperacaoAtivoRepository(_factory);
        _ativosService = new AtivoCadastroService(_ativos, _operacoes);
        _operacoesService = new ResultadoRealizadoService(_factory);

        var membros = new MembroFamiliaRepository(_factory);
        var membro = new MembroFamilia { Nome = "Usuário" };
        membros.AddAsync(membro).GetAwaiter().GetResult();
        _membroId = membro.Id;
    }

    [Fact]
    public async Task Excluir_ComOperacao_BloqueiaEInativarPermanece()
    {
        var ativo = await _ativosService.SalvarAsync(new AtivoFormModel { Ticker = "petr4", Nome = "Petrobras", Tipo = TipoAtivo.Acao });
        await _operacoesService.SalvarAsync(Compra(ativo.Id, 100), _membroId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _ativosService.ExcluirAsync(ativo.Id));
        Assert.Contains("não pode ser excluído", ex.Message);
        Assert.NotNull(await _ativos.GetByIdAsync(ativo.Id));

        await _ativosService.InativarAsync(ativo.Id);
        Assert.False((await _ativos.GetByIdAsync(ativo.Id))!.Ativo);
    }

    [Fact]
    public async Task CorrigirCompra_QueEstouraVendaPosterior_NaoGrava()
    {
        var ativo = await _ativosService.SalvarAsync(new AtivoFormModel { Ticker = "VALE3", Nome = "Vale", Tipo = TipoAtivo.Acao });
        await _operacoesService.SalvarAsync(Compra(ativo.Id, 100, new DateTime(2026, 1, 10)), _membroId);
        await _operacoesService.SalvarAsync(new OperacaoAtivoFormModel
        {
            AtivoId = ativo.Id,
            Tipo = TipoOperacao.Venda,
            Data = new DateTime(2026, 2, 10),
            Quantidade = 80,
            PrecoUnitario = 12
        }, _membroId);

        var compra = (await _operacoes.ListarPorAtivoAsync(ativo.Id, _membroId)).Single(o => o.Tipo == TipoOperacao.Compra);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _operacoesService.SalvarAsync(new OperacaoAtivoFormModel
        {
            Id = compra.Id,
            AtivoId = ativo.Id,
            Tipo = TipoOperacao.Compra,
            Data = compra.Data,
            Quantidade = 50,
            PrecoUnitario = compra.PrecoUnitario
        }, _membroId));

        var gravada = await _operacoes.GetByIdAsync(compra.Id);
        Assert.Equal(100m, gravada!.Quantidade);
    }

    [Fact]
    public async Task EtfConhecido_GravaComoEtf_MesmoInformadoComoAcao()
    {
        var ativo = await _ativosService.SalvarAsync(new AtivoFormModel
        {
            Ticker = "bova11",
            Nome = "Ibovespa",
            Tipo = TipoAtivo.Acao
        });

        Assert.Equal("BOVA11", ativo.Ticker);
        Assert.Equal(TipoAtivo.Etf, ativo.Tipo);

        ativo = await _ativosService.SalvarAsync(new AtivoFormModel
        {
            Id = ativo.Id,
            Ticker = "BOVA11",
            Nome = "iShares Ibovespa",
            Tipo = TipoAtivo.Acao
        });
        Assert.Equal(TipoAtivo.Etf, ativo.Tipo);
        Assert.Equal("iShares Ibovespa", ativo.Nome);
    }

    [Fact]
    public async Task TickerInvalido_ERecusado()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _ativosService.SalvarAsync(new AtivoFormModel { Ticker = "PETRO4", Nome = "Inválido" }));
    }

    private static OperacaoAtivoFormModel Compra(int ativoId, decimal quantidade, DateTime? data = null) => new()
    {
        AtivoId = ativoId,
        Tipo = TipoOperacao.Compra,
        Data = data ?? new DateTime(2026, 1, 10),
        Quantidade = quantidade,
        PrecoUnitario = 10
    };

    public void Dispose() => _factory.Dispose();
}
