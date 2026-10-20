namespace OrcaFacil.Core.B3;

/// <summary>
/// Uma entrada da tabela texto → tipo. O tipo é fixo ou depende de Crédito/Débito,
/// como em COMPRA / VENDA.
/// </summary>
public sealed class RegraMovimentacaoB3
{
    private RegraMovimentacaoB3(TipoOperacaoB3? fixo, TipoOperacaoB3? credito, TipoOperacaoB3? debito)
    {
        TipoFixo = fixo;
        TipoCredito = credito;
        TipoDebito = debito;
    }

    public TipoOperacaoB3? TipoFixo { get; }

    public TipoOperacaoB3? TipoCredito { get; }

    public TipoOperacaoB3? TipoDebito { get; }

    public static RegraMovimentacaoB3 Fixa(TipoOperacaoB3 tipo) => new(tipo, null, null);

    public static RegraMovimentacaoB3 PorDirecao(TipoOperacaoB3 credito, TipoOperacaoB3 debito)
        => new(null, credito, debito);

    public bool TentarResolver(DirecaoB3 direcao, out TipoOperacaoB3 tipo)
    {
        if (TipoFixo is { } fixo)
        {
            tipo = fixo;
            return true;
        }

        if (direcao == DirecaoB3.Credito && TipoCredito is { } noCredito)
        {
            tipo = noCredito;
            return true;
        }

        if (direcao == DirecaoB3.Debito && TipoDebito is { } noDebito)
        {
            tipo = noDebito;
            return true;
        }

        tipo = default;
        return false;
    }
}
