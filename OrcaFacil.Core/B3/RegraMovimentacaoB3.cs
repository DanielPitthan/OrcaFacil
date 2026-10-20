namespace OrcaFacil.Core.B3;

/// <summary>
/// Uma entrada da tabela texto → tipo. O tipo é fixo, depende de Crédito/Débito,
/// ou, na renda variável, depende da direção e nas outras classes usa um tipo reservado.
/// </summary>
public sealed class RegraMovimentacaoB3
{
    private RegraMovimentacaoB3(
        TipoOperacaoB3? fixo,
        TipoOperacaoB3? credito,
        TipoOperacaoB3? debito,
        TipoOperacaoB3? foraDaRendaVariavel)
    {
        TipoFixo = fixo;
        TipoCredito = credito;
        TipoDebito = debito;
        TipoForaDaRendaVariavel = foraDaRendaVariavel;
    }

    public TipoOperacaoB3? TipoFixo { get; }

    public TipoOperacaoB3? TipoCredito { get; }

    public TipoOperacaoB3? TipoDebito { get; }

    /// <summary>
    /// Usado quando a regra de direção vale só para renda variável
    /// (ação e FII; fundo de índice entra como ação).
    /// </summary>
    public TipoOperacaoB3? TipoForaDaRendaVariavel { get; }

    public static RegraMovimentacaoB3 Fixa(TipoOperacaoB3 tipo) => new(tipo, null, null, null);

    public static RegraMovimentacaoB3 PorDirecao(TipoOperacaoB3 credito, TipoOperacaoB3 debito)
        => new(null, credito, debito, null);

    /// <summary>
    /// Crédito e débito mapeiam a renda variável. Renda fixa e o restante usam <paramref name="foraDaRendaVariavel"/>.
    /// A direção da linha não muda.
    /// </summary>
    public static RegraMovimentacaoB3 PorDirecaoNaRendaVariavel(
        TipoOperacaoB3 credito,
        TipoOperacaoB3 debito,
        TipoOperacaoB3 foraDaRendaVariavel)
        => new(null, credito, debito, foraDaRendaVariavel);

    public bool TentarResolver(DirecaoB3 direcao, ClasseAtivoB3 classe, out TipoOperacaoB3 tipo)
    {
        if (TipoForaDaRendaVariavel is { } reservado && classe == ClasseAtivoB3.RendaFixa)
        {
            tipo = reservado;
            return true;
        }

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
