using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Dtos;

public class FiltroLancamentoDto
{
    public DateTime? Inicio { get; set; }
    public DateTime? Fim { get; set; }
    public TipoTransacao? Tipo { get; set; }
    public int? CategoriaId { get; set; }
    public int? MembroFamiliaId { get; set; }
}

public class FiltroRelatorioDto
{
    public DateTime Inicio { get; set; } = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    public DateTime Fim { get; set; } = DateTime.Today;
    public TipoTransacao? Tipo { get; set; }
    public int? CategoriaId { get; set; }
    public int? MembroFamiliaId { get; set; }
}

public class RelatorioResultDto
{
    public List<Transacao> Transacoes { get; set; } = new();
    public decimal TotalReceitas { get; set; }
    public decimal TotalDespesas { get; set; }
    public decimal Saldo => TotalReceitas - TotalDespesas;
}
