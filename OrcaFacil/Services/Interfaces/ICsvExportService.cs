using OrcaFacil.Core.Entities;

namespace OrcaFacil.Services.Interfaces;

public interface ICsvExportService
{
    Task<string> GerarCsvAsync(IEnumerable<Transacao> transacoes);
    Task CompartilharAsync(string caminhoArquivo);
}
