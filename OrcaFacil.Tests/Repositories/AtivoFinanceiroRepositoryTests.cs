using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Repositories;
using Xunit;

namespace OrcaFacil.Tests.Repositories;

public class AtivoFinanceiroRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly AtivoFinanceiroRepository _repository;

    public AtivoFinanceiroRepositoryTests()
    {
        _repository = new AtivoFinanceiroRepository(_factory);
    }

    [Fact]
    public async Task TickerDuplicado_DeveSerRecusado()
    {
        await _repository.AddAsync(new AtivoFinanceiro { Ticker = "PETR4", Nome = "Petrobras", Tipo = TipoAtivo.Acao });

        Assert.True(await _repository.ExisteTickerAsync("petr4"));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            _repository.AddAsync(new AtivoFinanceiro { Ticker = "PETR4", Nome = "Outra", Tipo = TipoAtivo.Acao }));
    }

    [Fact]
    public async Task Operacao_ExigeAtivoEMembroExistentes()
    {
        var operacoes = new OperacaoAtivoRepository(_factory);
        var membros = new MembroFamiliaRepository(_factory);
        var membro = new MembroFamilia { Nome = "Usuário" };
        await membros.AddAsync(membro);

        await Assert.ThrowsAsync<DbUpdateException>(() => operacoes.AddAsync(new OperacaoAtivo
        {
            AtivoId = 999,
            MembroFamiliaId = membro.Id,
            Tipo = TipoOperacao.Compra,
            Data = DateTime.Today,
            Quantidade = 1,
            PrecoUnitario = 10
        }));

        var ativo = new AtivoFinanceiro { Ticker = "MXRF11", Nome = "Maxi Renda", Tipo = TipoAtivo.Fii };
        await _repository.AddAsync(ativo);

        await Assert.ThrowsAsync<DbUpdateException>(() => operacoes.AddAsync(new OperacaoAtivo
        {
            AtivoId = ativo.Id,
            MembroFamiliaId = 999,
            Tipo = TipoOperacao.Compra,
            Data = DateTime.Today,
            Quantidade = 1,
            PrecoUnitario = 10
        }));
    }

    public void Dispose() => _factory.Dispose();
}
