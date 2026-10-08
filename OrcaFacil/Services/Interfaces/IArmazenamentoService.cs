using OrcaFacil.Core.Enums;

namespace OrcaFacil.Services.Interfaces;

public class ResumoArmazenamentoDto
{
    public long BancoBytes { get; set; }
    public long LogsBytes { get; set; }
    public long FotoBytes { get; set; }
    public long TotalBytes => BancoBytes + LogsBytes + FotoBytes;
    public Dictionary<string, int> Registros { get; set; } = new();
}

/// <summary>Uso de armazenamento local e limpeza seletiva da base de dados.</summary>
public interface IArmazenamentoService
{
    Task<ResumoArmazenamentoDto> ObterResumoAsync();
    Task LimparAsync(TipoLimpezaDados tipo);
}
