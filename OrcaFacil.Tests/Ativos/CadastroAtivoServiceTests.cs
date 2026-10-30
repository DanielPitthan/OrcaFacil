using Microsoft.EntityFrameworkCore;
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
    public async Task EtfConhecido_InformadoComoFiiOuBdr_MantemOTipoNoFormulario()
    {
        var fii = await _ativosService.SalvarAsync(new AtivoFormModel
        {
            Ticker = "BOVA11",
            Nome = "Ibovespa",
            Tipo = TipoAtivo.Fii
        });
        Assert.Equal(TipoAtivo.Fii, fii.Tipo);

        fii = await _ativosService.SalvarAsync(new AtivoFormModel
        {
            Id = fii.Id,
            Ticker = "BOVA11",
            Nome = "Ibovespa FII",
            Tipo = TipoAtivo.Fii
        });
        Assert.Equal(TipoAtivo.Fii, fii.Tipo);
        Assert.Equal("Ibovespa FII", fii.Nome);

        var bdr = await _ativosService.SalvarAsync(new AtivoFormModel
        {
            Ticker = "IVVB11",
            Nome = "S&P",
            Tipo = TipoAtivo.Bdr
        });
        Assert.Equal(TipoAtivo.Bdr, bdr.Tipo);

        var desconhecido = await _ativosService.SalvarAsync(new AtivoFormModel
        {
            Ticker = "PETR4",
            Nome = "Petrobras",
            Tipo = TipoAtivo.Acao
        });
        Assert.Equal(TipoAtivo.Acao, desconhecido.Tipo);
    }

    [Fact]
    public async Task Formulario_ReclassificaAcaoConhecida_EAvisaMesFechado_SemRepetir()
    {
        int id;
        await using (var ctx = _factory.CreateDbContext())
        {
            var membro = await ctx.MembrosFamilia.SingleAsync();
            var ativo = new AtivoFinanceiro { Ticker = "IMAB11", Nome = "IMA-B", Tipo = TipoAtivo.Acao };
            ctx.AtivosFinanceiros.Add(ativo);
            ctx.FechamentosPeriodo.Add(new FechamentoPeriodo { Mes = 3, Ano = 2024, Fechado = true });
            await ctx.SaveChangesAsync();
            id = ativo.Id;
            ctx.OperacoesAtivo.AddRange(
                new OperacaoAtivo
                {
                    AtivoId = id,
                    MembroFamiliaId = membro.Id,
                    Tipo = TipoOperacao.Compra,
                    Data = new DateTime(2024, 3, 4),
                    DataPregao = new DateTime(2024, 3, 4),
                    Quantidade = 10m,
                    PrecoUnitario = 100m,
                    ValorOperacao = 1000m
                },
                new OperacaoAtivo
                {
                    AtivoId = id,
                    MembroFamiliaId = membro.Id,
                    Tipo = TipoOperacao.Venda,
                    Data = new DateTime(2024, 3, 18),
                    DataPregao = new DateTime(2024, 3, 18),
                    Quantidade = 10m,
                    PrecoUnitario = 110m,
                    ValorOperacao = 1100m
                });
            await ctx.SaveChangesAsync();
        }

        var service = new AtivoCadastroService(_ativos, _operacoes, _factory);
        var salvo = await service.SalvarAsync(new AtivoFormModel
        {
            Id = id,
            Ticker = "IMAB11",
            Nome = "IMA-B",
            Tipo = TipoAtivo.Acao
        });
        Assert.Equal(TipoAtivo.Etf, salvo.Tipo);

        await using (var ctx = _factory.CreateDbContext())
        {
            var aviso = await ctx.LogsEvento.SingleAsync();
            Assert.Contains("03/2024", aviso.Mensagem);
            Assert.Contains("DARF complementar", aviso.Mensagem);
        }

        await service.SalvarAsync(new AtivoFormModel
        {
            Id = id,
            Ticker = "IMAB11",
            Nome = "IMA-B",
            Tipo = TipoAtivo.Etf
        });
        await using (var ctx = _factory.CreateDbContext())
            Assert.Equal(1, await ctx.LogsEvento.CountAsync());

        var comoFii = await service.SalvarAsync(new AtivoFormModel
        {
            Id = id,
            Ticker = "IMAB11",
            Nome = "IMA-B",
            Tipo = TipoAtivo.Fii
        });
        Assert.Equal(TipoAtivo.Fii, comoFii.Tipo);
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
