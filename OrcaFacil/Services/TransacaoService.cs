using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Services.Interfaces;

namespace OrcaFacil.Services;

public class TransacaoService : ITransacaoService
{
    private readonly ITransacaoRepository _transacaoRepository;
    private readonly ICategoriaRepository _categoriaRepository;
    private readonly IPerfilContext _perfilContext;
    private readonly INotificationSchedulerService _notificationScheduler;
    private readonly IFechamentoService _fechamentoService;

    public TransacaoService(
        ITransacaoRepository transacaoRepository,
        ICategoriaRepository categoriaRepository,
        IPerfilContext perfilContext,
        INotificationSchedulerService notificationScheduler,
        IFechamentoService fechamentoService)
    {
        _transacaoRepository = transacaoRepository;
        _categoriaRepository = categoriaRepository;
        _perfilContext = perfilContext;
        _notificationScheduler = notificationScheduler;
        _fechamentoService = fechamentoService;
    }

    public Task<List<Transacao>> ListarAsync(FiltroLancamentoDto filtro)
    {
        var inicio = filtro.Inicio ?? DateTime.MinValue;
        var fim = filtro.Fim ?? DateTime.MaxValue;
        return _transacaoRepository.GetByPeriodoAsync(inicio, fim, filtro.CategoriaId, filtro.Tipo, filtro.MembroFamiliaId);
    }

    public Task<Transacao?> ObterAsync(int id) => _transacaoRepository.GetByIdAsync(id);

    public async Task<Transacao> CriarAsync(TransacaoFormModel dto)
    {
        await _fechamentoService.GarantirPeriodoAbertoAsync(dto.Data);

        if (await _categoriaRepository.GetByIdAsync(dto.CategoriaId) is null)
            throw new InvalidOperationException("Categoria inválida.");

        var membroId = _perfilContext.Atual?.Id
            ?? throw new InvalidOperationException("Nenhum perfil ativo.");

        var transacao = new Transacao
        {
            Descricao = dto.Descricao,
            Valor = dto.Valor,
            Data = dto.Data,
            Tipo = dto.Tipo,
            Recorrencia = dto.Recorrencia,
            Pago = dto.Tipo == TipoTransacao.Despesa ? dto.Pago : true,
            Observacao = dto.Observacao,
            CategoriaId = dto.CategoriaId,
            MembroFamiliaId = membroId
        };

        await _transacaoRepository.AddAsync(transacao);
        await _notificationScheduler.AgendarLembreteAsync(transacao);

        return transacao;
    }

    public async Task AtualizarAsync(int id, TransacaoFormModel dto)
    {
        if (await _categoriaRepository.GetByIdAsync(dto.CategoriaId) is null)
            throw new InvalidOperationException("Categoria inválida.");

        var transacao = await _transacaoRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException("Lançamento não encontrado.");

        await _fechamentoService.GarantirPeriodoAbertoAsync(transacao.Data);
        await _fechamentoService.GarantirPeriodoAbertoAsync(dto.Data);

        transacao.Descricao = dto.Descricao;
        transacao.Valor = dto.Valor;
        transacao.Data = dto.Data;
        transacao.Tipo = dto.Tipo;
        transacao.Recorrencia = dto.Recorrencia;
        transacao.Pago = dto.Tipo == TipoTransacao.Despesa ? dto.Pago : true;
        transacao.Observacao = dto.Observacao;
        transacao.CategoriaId = dto.CategoriaId;
        transacao.AtualizadoEm = DateTime.UtcNow;

        await _transacaoRepository.UpdateAsync(transacao);

        await _notificationScheduler.CancelarLembreteAsync(id);
        await _notificationScheduler.AgendarLembreteAsync(transacao);
    }

    public async Task ExcluirAsync(int id)
    {
        var transacao = await _transacaoRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException("Lançamento não encontrado.");

        await _fechamentoService.GarantirPeriodoAbertoAsync(transacao.Data);

        await _transacaoRepository.DeleteAsync(transacao);
        await _notificationScheduler.CancelarLembreteAsync(id);
    }

    public async Task MarcarComoPagoAsync(int id, bool pago)
    {
        var transacao = await _transacaoRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException("Lançamento não encontrado.");

        await _fechamentoService.GarantirPeriodoAbertoAsync(transacao.Data);

        transacao.Pago = pago;
        transacao.AtualizadoEm = DateTime.UtcNow;
        await _transacaoRepository.UpdateAsync(transacao);

        await _notificationScheduler.CancelarLembreteAsync(id);
        if (!pago)
            await _notificationScheduler.AgendarLembreteAsync(transacao);
    }

    public async Task<int> TrazerDespesasDoMesAnteriorAsync(int mes, int ano)
    {
        await _fechamentoService.GarantirPeriodoAbertoAsync(new DateTime(ano, mes, 1));

        var elegiveis = await ObterDespesasElegiveisAsync(mes, ano);
        var diasNoMesAlvo = DateTime.DaysInMonth(ano, mes);

        foreach (var origem in elegiveis)
        {
            var dia = Math.Min(origem.Data.Day, diasNoMesAlvo);
            var copia = new Transacao
            {
                Descricao = origem.Descricao,
                Valor = origem.Valor,
                Data = new DateTime(ano, mes, dia),
                Tipo = TipoTransacao.Despesa,
                Recorrencia = origem.Recorrencia,
                Pago = false,
                Observacao = origem.Observacao,
                CategoriaId = origem.CategoriaId,
                MembroFamiliaId = origem.MembroFamiliaId,
                TransacaoOrigemId = origem.Id
            };

            await _transacaoRepository.AddAsync(copia);
            await _notificationScheduler.AgendarLembreteAsync(copia);
        }

        return elegiveis.Count;
    }

    public async Task<bool> ExistemDespesasParaTrazerAsync(int mes, int ano)
    {
        var elegiveis = await ObterDespesasElegiveisAsync(mes, ano);
        return elegiveis.Count > 0;
    }

    private async Task<List<Transacao>> ObterDespesasElegiveisAsync(int mes, int ano)
    {
        var mesAnterior = new DateTime(ano, mes, 1).AddMonths(-1);
        var inicioAnterior = mesAnterior;
        var fimAnterior = mesAnterior.AddMonths(1).AddTicks(-1);

        var inicioAlvo = new DateTime(ano, mes, 1);
        var fimAlvo = inicioAlvo.AddMonths(1).AddTicks(-1);

        var recorrentes = await _transacaoRepository.GetDespesasRecorrentesPorPeriodoAsync(inicioAnterior, fimAnterior);
        var jaCopiadas = await _transacaoRepository.GetOrigemIdsComCopiaNoPeriodoAsync(inicioAlvo, fimAlvo);

        return recorrentes.Where(t => !jaCopiadas.Contains(t.Id)).ToList();
    }

    public async Task<ResumoMensalDto> ObterResumoMensalAsync(int mes, int ano)
    {
        var inicio = new DateTime(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddTicks(-1);

        var receitas = await _transacaoRepository.SomaPorTipoAsync(TipoTransacao.Receita, inicio, fim);
        var despesas = await _transacaoRepository.SomaPorTipoAsync(TipoTransacao.Despesa, inicio, fim);
        var despesasPagas = await _transacaoRepository.SomaPorTipoAsync(TipoTransacao.Despesa, inicio, fim, pago: true);

        return new ResumoMensalDto
        {
            Mes = mes,
            Ano = ano,
            TotalReceitas = receitas,
            TotalDespesas = despesas,
            TotalDespesasPagas = despesasPagas
        };
    }

    public async Task<EvolucaoMensalDto> ObterEvolucaoDiariaAsync(int mes, int ano)
    {
        var inicio = new DateTime(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddTicks(-1);
        var transacoes = await _transacaoRepository.GetByPeriodoAsync(inicio, fim);

        var porDia = transacoes.ToLookup(t => t.Data.Day);
        var dias = new List<EvolucaoDiariaDto>();
        decimal receitas = 0, despesas = 0, pagas = 0;

        for (var dia = 1; dia <= DateTime.DaysInMonth(ano, mes); dia++)
        {
            foreach (var t in porDia[dia])
            {
                if (t.Tipo == TipoTransacao.Receita)
                {
                    receitas += t.Valor;
                }
                else
                {
                    despesas += t.Valor;
                    if (t.Pago)
                        pagas += t.Valor;
                }
            }

            dias.Add(new EvolucaoDiariaDto
            {
                Dia = dia,
                ReceitasAcumuladas = receitas,
                DespesasAcumuladas = despesas,
                DespesasPagasAcumuladas = pagas
            });
        }

        return new EvolucaoMensalDto
        {
            Resumo = new ResumoMensalDto
            {
                Mes = mes,
                Ano = ano,
                TotalReceitas = receitas,
                TotalDespesas = despesas,
                TotalDespesasPagas = pagas
            },
            Dias = dias
        };
    }

    public async Task<List<GastoPorCategoriaDto>> ObterGastosPorCategoriaAsync(int mes, int ano)
    {
        var inicio = new DateTime(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddTicks(-1);

        var somas = await _transacaoRepository.SomaPorCategoriaAsync(inicio, fim, TipoTransacao.Despesa);
        var categorias = await _categoriaRepository.GetAllAsync();

        return somas
            .Select(kv =>
            {
                var categoria = categorias.FirstOrDefault(c => c.Id == kv.Key);
                return new GastoPorCategoriaDto
                {
                    CategoriaId = kv.Key,
                    CategoriaNome = categoria?.Nome ?? "Sem categoria",
                    CategoriaIcone = categoria?.Icone ?? "🏷️",
                    CategoriaCorHex = categoria?.CorHex ?? "#A2B94F",
                    Total = kv.Value
                };
            })
            .OrderByDescending(d => d.Total)
            .ToList();
    }

    public Task<List<Transacao>> ObterDespesasComAvisoAsync()
        => _transacaoRepository.GetDespesasPendentesParaAvisoAsync(DateTime.Today);

    public async Task<List<TendenciaMensalDto>> ObterTendenciaMensalAsync(int meses)
    {
        var resultado = new List<TendenciaMensalDto>();
        var referencia = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        for (var i = meses - 1; i >= 0; i--)
        {
            var mesRef = referencia.AddMonths(-i);
            var inicio = mesRef;
            var fim = mesRef.AddMonths(1).AddTicks(-1);

            var receitas = await _transacaoRepository.SomaPorTipoAsync(TipoTransacao.Receita, inicio, fim);
            var despesas = await _transacaoRepository.SomaPorTipoAsync(TipoTransacao.Despesa, inicio, fim);

            resultado.Add(new TendenciaMensalDto
            {
                Mes = mesRef.Month,
                Ano = mesRef.Year,
                Rotulo = mesRef.ToString("MMM/yy"),
                TotalReceitas = receitas,
                TotalDespesas = despesas
            });
        }

        return resultado;
    }
}
