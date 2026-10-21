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

    /// <summary>
    /// Na importação B3, a data de liquidação do extrato. A #30 gravava só esta data.
    /// No lançamento manual, a data digitada, igual ao pregão.
    /// O lançamento de receita ou despesa e o hash da importação usam este campo.
    /// </summary>
    public DateTime Data { get; set; }

    /// <summary>
    /// Data do pregão. A apuração do IR (mês, teto de R$ 20 mil, day trade e vencimento do DARF) usa este campo.
    /// Na importação B3, é a liquidação recuada D+2 dias úteis pelo calendário bancário.
    /// No lançamento manual, é a data digitada.
    /// A #104 altera a regra da #30. Fonte: Perguntas e Respostas IRPF 2026, pergunta 745.
    /// </summary>
    public DateTime DataPregao { get; set; }

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
