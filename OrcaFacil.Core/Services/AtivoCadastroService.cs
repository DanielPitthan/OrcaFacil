using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Repositories;

namespace OrcaFacil.Core.Services;

public class AtivoCadastroService
{
    private readonly IAtivoFinanceiroRepository _ativos;
    private readonly IOperacaoAtivoRepository _operacoes;

    public AtivoCadastroService(IAtivoFinanceiroRepository ativos, IOperacaoAtivoRepository operacoes)
    {
        _ativos = ativos;
        _operacoes = operacoes;
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

        if (modelo.Id is null)
        {
            var novo = new AtivoFinanceiro { Ticker = ticker, Nome = nome, Tipo = modelo.Tipo };
            await _ativos.AddAsync(novo);
            return novo;
        }

        var ativo = await _ativos.GetByIdAsync(modelo.Id.Value)
            ?? throw new InvalidOperationException("Ativo não encontrado.");

        ativo.Ticker = ticker;
        ativo.Nome = nome;
        ativo.Tipo = modelo.Tipo;
        await _ativos.UpdateAsync(ativo);
        return ativo;
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
