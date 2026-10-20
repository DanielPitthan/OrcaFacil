using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Repositories;

namespace OrcaFacil.Core.Services;

/// <summary>
/// Posição em carteira. A gravação, o resultado e o lançamento ficam em <see cref="ResultadoRealizadoService"/>.
/// </summary>
public class OperacaoCadastroService
{
    private readonly IOperacaoAtivoRepository _operacoes;

    public OperacaoCadastroService(IOperacaoAtivoRepository operacoes)
    {
        _operacoes = operacoes;
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

    private static MovimentoPosicao ParaMovimento(Entities.OperacaoAtivo operacao) => new()
    {
        Id = operacao.Id,
        Data = operacao.Data,
        Tipo = operacao.Tipo,
        Quantidade = operacao.Quantidade,
        PrecoUnitario = operacao.PrecoUnitario,
        Custos = operacao.Custos
    };
}
