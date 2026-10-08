namespace OrcaFacil.Core.Enums;

/// <summary>Momentos em que uma despesa não paga gera notificação, relativos à data de vencimento.</summary>
[Flags]
public enum PrazoAviso
{
    Nenhum = 0,
    TresDiasAntes = 1,
    UmDiaAntes = 2,
    NoDia = 4,
    UmDiaDepois = 8,

    /// <summary>Aviso diário enquanto a despesa continuar vencida (do 2º ao 7º dia após o vencimento).</summary>
    Vencidas = 16
}
