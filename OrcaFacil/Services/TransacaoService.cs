using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Lancamentos;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Core.Services;
using OrcaFacil.Services.Interfaces;

namespace OrcaFacil.Services;

public class TransacaoService : ITransacaoService
{
    private readonly ITransacaoRepository _transacaoRepository;
    private readonly ICategoriaRepository _categoriaRepository;
    private readonly IPerfilContext _perfilContext;
    private readonly INotificationSchedulerService _notificationScheduler;
    private readonly IFechamentoService _fechamentoService;
    private readonly CopiaDespesasMesService _copiaDespesas;

    public TransacaoService(
        ITransacaoRepository transacaoRepository,
        ICategoriaRepository categoriaRepository,
        IPerfilContext perfilContext,
        INotificationSchedulerService notificationScheduler,
        IFechamentoService fechamentoService,
        CopiaDespesasMesService copiaDespesas)
    {
        _transacaoRepository = transacaoRepository;
        _categoriaRepository = categoriaRepository;
        _perfilContext = perfilContext;
        _notificationScheduler = notificationScheduler;
        _fechamentoService = fechamentoService;
        _copiaDespesas = copiaDespesas;
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

        var categoria = await _categoriaRepository.GetByIdAsync(dto.CategoriaId)
            ?? throw new InvalidOperationException("Categoria inválida.");

        var itens = await MapearItensAsync(dto, categoria);

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
            CartaoId = dto.CartaoId,
            MembroFamiliaId = membroId,
            Itens = itens
        };

        await _transacaoRepository.AddAsync(transacao);
        await _notificationScheduler.AgendarLembreteAsync(transacao);

        return transacao;
    }

    public async Task AtualizarAsync(int id, TransacaoFormModel dto)
    {
        var categoria = await _categoriaRepository.GetByIdAsync(dto.CategoriaId)
            ?? throw new InvalidOperationException("Categoria inválida.");

        var transacao = await _transacaoRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException("Lançamento não encontrado.");

        ProtecaoLancamentoOperacao.GarantirEditavelNaTela(
            transacao.OperacaoAtivoId, transacao.OperacaoRendaFixaId, transacao.ProventoId);
        await _fechamentoService.GarantirPeriodoAbertoAsync(transacao.Data);
        await _fechamentoService.GarantirPeriodoAbertoAsync(dto.Data);

        var itens = await MapearItensAsync(dto, categoria);

        transacao.Descricao = dto.Descricao;
        transacao.Valor = dto.Valor;
        transacao.Data = dto.Data;
        transacao.Tipo = dto.Tipo;
        transacao.Recorrencia = dto.Recorrencia;
        transacao.Pago = dto.Tipo == TipoTransacao.Despesa ? dto.Pago : true;
        transacao.Observacao = dto.Observacao;
        transacao.CategoriaId = dto.CategoriaId;
        transacao.CartaoId = dto.CartaoId;
        transacao.AtualizadoEm = DateTime.UtcNow;

        await _transacaoRepository.UpdateAsync(transacao);
        await _transacaoRepository.SubstituirItensAsync(id, itens);

        await _notificationScheduler.CancelarLembreteAsync(id);
        await _notificationScheduler.AgendarLembreteAsync(transacao);
    }

    public async Task ExcluirAsync(int id)
    {
        var transacao = await _transacaoRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException("Lançamento não encontrado.");

        ProtecaoLancamentoOperacao.GarantirEditavelNaTela(
            transacao.OperacaoAtivoId, transacao.OperacaoRendaFixaId, transacao.ProventoId);
        await _fechamentoService.GarantirPeriodoAbertoAsync(transacao.Data);

        await _transacaoRepository.DeleteAsync(transacao);
        await _notificationScheduler.CancelarLembreteAsync(id);
    }

    public async Task MarcarComoPagoAsync(int id, bool pago)
    {
        var transacao = await _transacaoRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException("Lançamento não encontrado.");

        ProtecaoLancamentoOperacao.GarantirEditavelNaTela(
            transacao.OperacaoAtivoId, transacao.OperacaoRendaFixaId, transacao.ProventoId);
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

        var copias = await _copiaDespesas.CopiarAsync(mes, ano);
        foreach (var copia in copias)
            await _notificationScheduler.AgendarLembreteAsync(copia);

        return copias.Count;
    }

    public Task<bool> ExistemDespesasParaTrazerAsync(int mes, int ano)
        => _copiaDespesas.ExistemAsync(mes, ano);

    private async Task<List<ItemLancamento>> MapearItensAsync(TransacaoFormModel dto, Categoria categoria)
    {
        var itens = FaturaCartaoEditor.MapearItens(dto.Itens);

        ItensFaturaRegras.Validar(dto.Valor, dto.CategoriaId, categoria.PermiteItensFatura, dto.Tipo, itens);

        foreach (var item in itens)
        {
            if (await _categoriaRepository.GetByIdAsync(item.CategoriaId) is null)
                throw new InvalidOperationException("Categoria do item inválida.");
        }

        return itens;
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

    public async Task<ResumoAcumuladoDto> ObterResumoAcumuladoAsync()
    {
        var receitas = await _transacaoRepository.SomaTotalPorTipoAsync(TipoTransacao.Receita);
        var despesas = await _transacaoRepository.SomaTotalPorTipoAsync(TipoTransacao.Despesa);

        return new ResumoAcumuladoDto
        {
            TotalReceitas = receitas,
            TotalDespesas = despesas
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
