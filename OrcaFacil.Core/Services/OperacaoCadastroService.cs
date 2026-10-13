using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Repositories;

namespace OrcaFacil.Core.Services;

public class OperacaoCadastroService
{
    private readonly IOperacaoAtivoRepository _operacoes;
    private readonly IAtivoFinanceiroRepository _ativos;

    public OperacaoCadastroService(IOperacaoAtivoRepository operacoes, IAtivoFinanceiroRepository ativos)
    {
        _operacoes = operacoes;
        _ativos = ativos;
    }

    public async Task<List<PosicaoAtivoDto>> ListarPosicaoAsync(int membroFamiliaId)
    {
        var operacoes = await _operacoes.ListarPorMembroAsync(membroFamiliaId);

        return operacoes
            .GroupBy(o => o.AtivoId)
            .Select(grupo =>
            {
                var ativo = grupo.First().Ativo;
                var posicao = PosicaoCalculator.Calcular(grupo.Select(ParaMovimento));
                return new PosicaoAtivoDto
                {
                    AtivoId = grupo.Key,
                    Ticker = ativo?.Ticker ?? string.Empty,
                    Nome = ativo?.Nome ?? string.Empty,
                    Tipo = ativo?.Tipo ?? default,
                    CadastroAtivo = ativo?.Ativo ?? false,
                    Quantidade = posicao.Quantidade,
                    PrecoMedio = posicao.PrecoMedio,
                    CustoTotal = posicao.CustoTotal
                };
            })
            .Where(p => p.Quantidade > 0)
            .OrderBy(p => p.Ticker)
            .ToList();
    }

    public async Task SalvarAsync(OperacaoAtivoFormModel modelo, int membroFamiliaId)
    {
        ValidarCampos(modelo);

        var ativo = await _ativos.GetByIdAsync(modelo.AtivoId)
            ?? throw new InvalidOperationException("Ativo não encontrado.");

        OperacaoAtivo? existente = null;
        if (modelo.Id is int id)
        {
            existente = await _operacoes.GetByIdAsync(id)
                ?? throw new InvalidOperationException("Operação não encontrada.");

            if (existente.MembroFamiliaId != membroFamiliaId)
                throw new InvalidOperationException("Operação não encontrada.");

            if (existente.AtivoId != modelo.AtivoId)
                await ValidarLinhaAsync(existente.AtivoId, membroFamiliaId, ignorarId: id, inclusao: null);
        }
        else if (!ativo.Ativo)
        {
            throw new InvalidOperationException("Este ativo está inativo e não aceita novas operações.");
        }

        if (existente is not null && existente.AtivoId != modelo.AtivoId && !ativo.Ativo)
            throw new InvalidOperationException("Este ativo está inativo e não aceita novas operações.");

        await ValidarLinhaAsync(modelo.AtivoId, membroFamiliaId, ignorarId: modelo.Id, inclusao: modelo);

        if (existente is null)
        {
            await _operacoes.AddAsync(new OperacaoAtivo
            {
                AtivoId = modelo.AtivoId,
                MembroFamiliaId = membroFamiliaId,
                Tipo = modelo.Tipo,
                Data = modelo.Data.Date,
                Quantidade = modelo.Quantidade,
                PrecoUnitario = modelo.PrecoUnitario,
                Custos = modelo.Custos,
                Corretora = NormalizarCorretora(modelo.Corretora)
            });
            return;
        }

        existente.AtivoId = modelo.AtivoId;
        existente.Tipo = modelo.Tipo;
        existente.Data = modelo.Data.Date;
        existente.Quantidade = modelo.Quantidade;
        existente.PrecoUnitario = modelo.PrecoUnitario;
        existente.Custos = modelo.Custos;
        existente.Corretora = NormalizarCorretora(modelo.Corretora);
        existente.AtualizadoEm = DateTime.UtcNow;
        await _operacoes.UpdateAsync(existente);
    }

    public async Task ExcluirAsync(int id, int membroFamiliaId)
    {
        var operacao = await _operacoes.GetByIdAsync(id)
            ?? throw new InvalidOperationException("Operação não encontrada.");

        if (operacao.MembroFamiliaId != membroFamiliaId)
            throw new InvalidOperationException("Operação não encontrada.");

        await ValidarLinhaAsync(operacao.AtivoId, membroFamiliaId, ignorarId: id, inclusao: null);
        await _operacoes.DeleteAsync(operacao);
    }

    private async Task ValidarLinhaAsync(int ativoId, int membroFamiliaId, int? ignorarId, OperacaoAtivoFormModel? inclusao)
    {
        var atuais = await _operacoes.ListarPorAtivoAsync(ativoId, membroFamiliaId);
        var movimentos = atuais
            .Where(o => o.Id != ignorarId)
            .Select(ParaMovimento)
            .ToList();

        if (inclusao is not null)
        {
            movimentos.Add(new MovimentoPosicao
            {
                Id = inclusao.Id ?? 0,
                Data = inclusao.Data,
                Tipo = inclusao.Tipo,
                Quantidade = inclusao.Quantidade,
                PrecoUnitario = inclusao.PrecoUnitario,
                Custos = inclusao.Custos
            });
        }

        PosicaoCalculator.Calcular(movimentos);
    }

    private static void ValidarCampos(OperacaoAtivoFormModel modelo)
    {
        if (modelo.AtivoId <= 0)
            throw new InvalidOperationException("Selecione um ativo.");
        if (modelo.Quantidade <= 0)
            throw new InvalidOperationException("A quantidade deve ser maior que zero.");
        if (modelo.PrecoUnitario <= 0)
            throw new InvalidOperationException("O preço deve ser maior que zero.");
        if (modelo.Custos < 0)
            throw new InvalidOperationException("Os custos não podem ser negativos.");
        if (modelo.Corretora?.Trim().Length > 40)
            throw new InvalidOperationException("A corretora deve ter no máximo 40 caracteres.");
    }

    private static MovimentoPosicao ParaMovimento(OperacaoAtivo operacao) => new()
    {
        Id = operacao.Id,
        Data = operacao.Data,
        Tipo = operacao.Tipo,
        Quantidade = operacao.Quantidade,
        PrecoUnitario = operacao.PrecoUnitario,
        Custos = operacao.Custos
    };

    private static string? NormalizarCorretora(string? corretora)
        => string.IsNullOrWhiteSpace(corretora) ? null : corretora.Trim();
}
