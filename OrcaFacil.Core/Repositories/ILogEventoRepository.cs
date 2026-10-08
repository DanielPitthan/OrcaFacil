using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Repositories;

public interface ILogEventoRepository : IGenericRepository<LogEvento>
{
    Task RegistrarAsync(TipoLogEvento tipo, string mensagem);

    /// <summary>Lista os eventos mais recentes primeiro, opcionalmente filtrando por tipo.</summary>
    Task<List<LogEvento>> ListarAsync(TipoLogEvento? tipo = null);

    Task<int> LimparAsync();
}
