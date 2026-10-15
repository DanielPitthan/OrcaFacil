using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Lancamentos;
using OrcaFacil.Core.Repositories;

namespace OrcaFacil.Core.Services;

public class CopiaDespesasMesService
{
    private readonly ITransacaoRepository _transacoes;

    public CopiaDespesasMesService(ITransacaoRepository transacoes)
    {
        _transacoes = transacoes;
    }

    public async Task<List<Transacao>> CopiarAsync(int mes, int ano)
    {
        var elegiveis = await ObterElegiveisAsync(mes, ano);
        var diasNoMesAlvo = DateTime.DaysInMonth(ano, mes);
        var criadas = new List<Transacao>();

        foreach (var origem in elegiveis)
        {
            var itens = ItensFaturaRegras.SelecionarParaCopia(origem.Itens);
            var dia = Math.Min(origem.Data.Day, diasNoMesAlvo);
            var copia = new Transacao
            {
                Descricao = origem.Descricao,
                Valor = ItensFaturaRegras.ValorDaCopia(origem, itens),
                Data = new DateTime(ano, mes, dia),
                Tipo = TipoTransacao.Despesa,
                Recorrencia = origem.Recorrencia,
                Pago = false,
                Observacao = origem.Observacao,
                CategoriaId = origem.CategoriaId,
                CartaoId = origem.CartaoId,
                MembroFamiliaId = origem.MembroFamiliaId,
                TransacaoOrigemId = origem.Id,
                Itens = itens
            };

            await _transacoes.AddAsync(copia);
            criadas.Add(copia);
        }

        return criadas;
    }

    public async Task<bool> ExistemAsync(int mes, int ano)
    {
        var elegiveis = await ObterElegiveisAsync(mes, ano);
        return elegiveis.Count > 0;
    }

    private async Task<List<Transacao>> ObterElegiveisAsync(int mes, int ano)
    {
        var mesAnterior = new DateTime(ano, mes, 1).AddMonths(-1);
        var inicioAlvo = new DateTime(ano, mes, 1);
        var fimAlvo = inicioAlvo.AddMonths(1).AddTicks(-1);

        var candidatas = await _transacoes.GetDespesasParaCopiaAsync(mesAnterior, mesAnterior.AddMonths(1).AddTicks(-1));
        var jaCopiadas = await _transacoes.GetOrigemIdsComCopiaNoPeriodoAsync(inicioAlvo, fimAlvo);

        return candidatas
            .Where(t => !jaCopiadas.Contains(t.Id) && ItensFaturaRegras.DeveCopiar(t))
            .ToList();
    }
}
