using System.ComponentModel.DataAnnotations;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Entities;

/// <summary>Compra ou venda. A posição em carteira é calculada a partir destas linhas, nunca digitada.</summary>
public class OperacaoAtivo
{
    public int Id { get; set; }

    public int AtivoId { get; set; }
    public AtivoFinanceiro? Ativo { get; set; }

    public int MembroFamiliaId { get; set; }
    public MembroFamilia? MembroFamilia { get; set; }

    public TipoOperacao Tipo { get; set; }

    public DateTime Data { get; set; }

    public decimal Quantidade { get; set; }

    public decimal PrecoUnitario { get; set; }

    /// <summary>
    /// Valor total da operação. O resultado realizado usa este valor.
    /// Operação antiga e a tela manual recebem quantidade × preço.
    /// </summary>
    public decimal ValorOperacao { get; set; }

    /// <summary>Corretagem e emolumentos. Entram no preço médio apenas na compra.</summary>
    public decimal Custos { get; set; }

    [MaxLength(40)]
    public string? Corretora { get; set; }

    public OrigemOperacaoAtivo Origem { get; set; } = OrigemOperacaoAtivo.Manual;

    /// <summary>
    /// Identidade da linha importada. Operação manual fica sem hash;
    /// o índice único ignora nulos, então manual não colide com importada.
    /// </summary>
    [MaxLength(64)]
    public string? HashImportacao { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? AtualizadoEm { get; set; }
}
