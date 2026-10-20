namespace OrcaFacil.Core.Ativos;

/// <summary>
/// Lançamento gerado pela venda não se edita nem se apaga pela tela de transações.
/// O caminho é a operação, que recalcula o resultado.
/// </summary>
public static class ProtecaoLancamentoAtivo
{
    public const string Mensagem =
        "Lançamento gerado pela venda de um ativo. Altere ou exclua a operação, não o lançamento.";

    public static void GarantirEditavelNaTela(int? operacaoAtivoId)
    {
        if (operacaoAtivoId is not null)
            throw new InvalidOperationException(Mensagem);
    }
}
