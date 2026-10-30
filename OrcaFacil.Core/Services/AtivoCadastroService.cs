using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Repositories;

namespace OrcaFacil.Core.Services;

public class AtivoCadastroService
{
    private readonly IAtivoFinanceiroRepository _ativos;
    private readonly IOperacaoAtivoRepository _operacoes;
    private readonly IDbContextFactory<AppDbContext>? _contexto;

    public AtivoCadastroService(
        IAtivoFinanceiroRepository ativos,
        IOperacaoAtivoRepository operacoes,
        IDbContextFactory<AppDbContext>? contexto = null)
    {
        _ativos = ativos;
        _operacoes = operacoes;
        _contexto = contexto;
    }

    public async Task<AtivoFinanceiro> SalvarAsync(AtivoFormModel modelo)
    {
        var ticker = TickerB3.Normalizar(modelo.Ticker);
        if (!TickerB3.EhValido(ticker))
            throw new InvalidOperationException(TickerB3.MensagemInvalido);

        var nome = modelo.Nome.Trim();
        if (string.IsNullOrWhiteSpace(nome))
            throw new InvalidOperationException("Informe o nome do ativo.");

        if (await _ativos.ExisteTickerAsync(ticker, modelo.Id))
            throw new InvalidOperationException($"Já existe um ativo com o ticker {ticker}.");

        var tipo = CatalogoEtfConhecido.TipoNoCadastro(ticker, modelo.Tipo);
        if (modelo.Id is null)
        {
            var novo = new AtivoFinanceiro { Ticker = ticker, Nome = nome, Tipo = tipo };
            await _ativos.AddAsync(novo);
            return novo;
        }

        if (_contexto is null)
        {
            var ativo = await _ativos.GetByIdAsync(modelo.Id.Value)
                ?? throw new InvalidOperationException("Ativo não encontrado.");

            ativo.Ticker = ticker;
            ativo.Nome = nome;
            ativo.Tipo = tipo;
            await _ativos.UpdateAsync(ativo);
            return ativo;
        }

        await using var contexto = await _contexto.CreateDbContextAsync();
        var gravado = await contexto.AtivosFinanceiros.FirstOrDefaultAsync(item => item.Id == modelo.Id.Value)
            ?? throw new InvalidOperationException("Ativo não encontrado.");
        await CatalogoEtfConhecido.AvisarAposMudancaAsync(contexto, () =>
        {
            gravado.Ticker = ticker;
            gravado.Nome = nome;
            gravado.Tipo = tipo;
        });

        await contexto.SaveChangesAsync();
        return gravado;
    }

    public async Task ExcluirAsync(int id)
    {
        var ativo = await _ativos.GetByIdAsync(id)
            ?? throw new InvalidOperationException("Ativo não encontrado.");

        if (await _operacoes.ExistePorAtivoAsync(id))
            throw new InvalidOperationException("Este ativo possui operações e não pode ser excluído. Inative-o para ocultá-lo de novas operações.");

        await _ativos.DeleteAsync(ativo);
    }

    public async Task InativarAsync(int id)
    {
        var ativo = await _ativos.GetByIdAsync(id)
            ?? throw new InvalidOperationException("Ativo não encontrado.");

        ativo.Ativo = false;
        await _ativos.UpdateAsync(ativo);
    }
}
