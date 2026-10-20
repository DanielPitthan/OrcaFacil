namespace OrcaFacil.Core.RendaFixa;

/// <summary>
/// Lançamento gerado pelo resgate não se edita nem se apaga pela tela de transações.
/// O caminho é a operação, que recalcula o resultado.
/// </summary>
public static class ProtecaoLancamentoRendaFixa
{
    public const string Mensagem =
        "Lançamento gerado por resgate de renda fixa. Altere ou exclua a operação, não o lançamento.";

    public static void GarantirEditavelNaTela(int? operacaoRendaFixaId)
    {
        if (operacaoRendaFixaId is not null)
            throw new InvalidOperationException(Mensagem);
    }
}
