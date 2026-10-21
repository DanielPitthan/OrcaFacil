using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Entities;

/// <summary>
/// IRRF informado para a planilha de DARFs. Uma linha por perfil, ano, mês e cesta.
/// O zero gravado substitui a estimativa. Sem linha, a apuração usa zero.
/// </summary>
public class IrrfInformado
{
    public int Id { get; set; }

    public int MembroFamiliaId { get; set; }
    public MembroFamilia? MembroFamilia { get; set; }

    public int Ano { get; set; }

    public int Mes { get; set; }

    public CestaApuracaoIr Cesta { get; set; }

    public decimal Valor { get; set; }
}
