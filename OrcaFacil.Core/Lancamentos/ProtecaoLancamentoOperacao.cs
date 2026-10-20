namespace OrcaFacil.Core.Lancamentos;

/// <summary>
/// Lançamento gerado por operação de renda variável ou renda fixa não se edita
/// nem se apaga pela tela de transações. O caminho é a operação.
/// </summary>
public static class ProtecaoLancamentoOperacao
{
    public const string MensagemAtivo =
        "Lançamento gerado pela venda de um ativo. Altere ou exclua a operação, não o lançamento.";

    public const string MensagemRendaFixa =
        "Lançamento gerado por resgate de renda fixa. Altere ou exclua a operação, não o lançamento.";

    public static void GarantirEditavelNaTela(int? operacaoAtivoId, int? operacaoRendaFixaId)
    {
        if (operacaoAtivoId is not null)
            throw new InvalidOperationException(MensagemAtivo);
        if (operacaoRendaFixaId is not null)
            throw new InvalidOperationException(MensagemRendaFixa);
    }
}
