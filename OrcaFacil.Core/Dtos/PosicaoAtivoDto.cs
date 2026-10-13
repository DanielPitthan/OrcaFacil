using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Dtos;

public class PosicaoAtivoDto
{
    public int AtivoId { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public TipoAtivo Tipo { get; set; }
    public bool CadastroAtivo { get; set; }
    public decimal Quantidade { get; set; }
    public decimal PrecoMedio { get; set; }
    public decimal CustoTotal { get; set; }
}
