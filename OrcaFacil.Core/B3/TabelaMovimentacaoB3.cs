namespace OrcaFacil.Core.B3;

/// <summary>
/// Tabela configurável das movimentações do extrato. A chave é o texto normalizado
/// (sem acento, caixa ignorada, espaços internos colapsados). Quem for integrar pode
/// passar outro dicionário ao <see cref="ParserExtratoB3"/>.
/// </summary>
public static class TabelaMovimentacaoB3
{
    /// <summary>
    /// Textos da amostra e os dois aliases citados no contrato (COMPRA/VENDA sem espaços e JCP).
    /// Transferência - Liquidação em renda variável segue a direção (Crédito compra, Débito venda).
    /// <see cref="TipoOperacaoB3.TransferenciaLiquidacao"/> fica para a mesma movimentação fora da renda variável.
    /// Atualização vai para <see cref="TipoOperacaoB3.Atualizacao"/>.
    /// </summary>
    public static IReadOnlyDictionary<string, RegraMovimentacaoB3> Padrao { get; } = CriarPadrao();

    private static Dictionary<string, RegraMovimentacaoB3> CriarPadrao()
    {
        var tabela = new Dictionary<string, RegraMovimentacaoB3>(StringComparer.Ordinal);
        Adicionar(
            tabela,
            "Transferência - Liquidação",
            RegraMovimentacaoB3.PorDirecaoNaRendaVariavel(
                TipoOperacaoB3.Compra,
                TipoOperacaoB3.Venda,
                TipoOperacaoB3.TransferenciaLiquidacao));
        var compraVenda = RegraMovimentacaoB3.PorDirecao(TipoOperacaoB3.Compra, TipoOperacaoB3.Venda);
        Adicionar(tabela, "COMPRA / VENDA", compraVenda);
        Adicionar(tabela, "COMPRA/VENDA", compraVenda);
        Adicionar(tabela, "Compra", RegraMovimentacaoB3.Fixa(TipoOperacaoB3.Compra));
        Adicionar(tabela, "Venda", RegraMovimentacaoB3.Fixa(TipoOperacaoB3.Venda));
        Adicionar(tabela, "Rendimento", RegraMovimentacaoB3.Fixa(TipoOperacaoB3.Rendimento));
        Adicionar(tabela, "Juros Sobre Capital Próprio", RegraMovimentacaoB3.Fixa(TipoOperacaoB3.Jcp));
        Adicionar(tabela, "JCP", RegraMovimentacaoB3.Fixa(TipoOperacaoB3.Jcp));
        Adicionar(tabela, "Resgate antecipado", RegraMovimentacaoB3.Fixa(TipoOperacaoB3.ResgateAntecipado));
        Adicionar(tabela, "Atualização", RegraMovimentacaoB3.Fixa(TipoOperacaoB3.Atualizacao));
        Adicionar(tabela, "Aplicação", RegraMovimentacaoB3.Fixa(TipoOperacaoB3.Aplicacao));
        return tabela;
    }

    private static void Adicionar(
        Dictionary<string, RegraMovimentacaoB3> tabela,
        string texto,
        RegraMovimentacaoB3 regra)
    {
        tabela.Add(TextoB3.NormalizarChave(texto), regra);
    }
}
