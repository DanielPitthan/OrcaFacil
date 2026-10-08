using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Services.Interfaces;

namespace OrcaFacil.Services;

public class FechamentoService : IFechamentoService
{
    private readonly IFechamentoRepository _fechamentoRepository;
    private readonly ILogEventoRepository _logRepository;

    public FechamentoService(IFechamentoRepository fechamentoRepository, ILogEventoRepository logRepository)
    {
        _fechamentoRepository = fechamentoRepository;
        _logRepository = logRepository;
    }

    public Task<bool> EstaFechadoAsync(int mes, int ano) => _fechamentoRepository.EstaFechadoAsync(mes, ano);

    public async Task<bool> AlternarAsync(int mes, int ano)
    {
        var fechado = await _fechamentoRepository.AlternarAsync(mes, ano);
        var periodo = $"{mes:00}/{ano}";

        await _logRepository.RegistrarAsync(
            fechado ? TipoLogEvento.Fechamento : TipoLogEvento.Reabertura,
            fechado ? $"Mês {periodo} fechado" : $"Mês {periodo} reaberto");

        return fechado;
    }

    public async Task GarantirPeriodoAbertoAsync(DateTime data)
    {
        if (await _fechamentoRepository.EstaFechadoAsync(data.Month, data.Year))
            throw new InvalidOperationException($"O mês {data:MM/yyyy} está fechado. Reabra o período para alterar lançamentos.");
    }
}
