using OrcaFacil.Core.Ativos;

namespace OrcaFacil.Core.B3;

/// <summary>
/// Heurística de classe, nesta ordem:
/// <list type="number">
/// <item>CDB, LCI ou LCA no ticker, ou a palavra Tesouro no produto → <see cref="ClasseAtivoB3.RendaFixa"/>.</item>
/// <item>Ticker terminando em 11 e nome com FII, Fiagro ou o radical imobiliário "imob"
/// (IMOB, IMOBI, Imobiliário) → <see cref="ClasseAtivoB3.FII"/>. Fiagro conta como FII.
/// O sufixo fracionário F não é tratado como 11.</item>
/// <item>Demais tickers válidos na B3 → <see cref="ClasseAtivoB3.Acao"/>. Fundo de índice
/// (BOVA11, GOLD11, SMAL11) cai aqui: o contrato não tem classe de ETF.</item>
/// <item><see cref="ClasseAtivoB3.Outro"/> quando o identificador não é renda fixa nem ticker da B3.
/// O parser não emite essa classe: a linha vai para erros por ticker inválido.</item>
/// </list>
/// </summary>
public static class ClassificadorAtivoB3
{
    public static ClasseAtivoB3 Classificar(string ticker, string produtoDescricao)
    {
        var codigo = TickerB3.Normalizar(ticker);
        var produto = TextoB3.NormalizarChave(produtoDescricao);
        if (EhRendaFixa(codigo, produto))
            return ClasseAtivoB3.RendaFixa;

        if (EhFii(codigo, produto))
            return ClasseAtivoB3.FII;

        if (TickerB3.EhValido(codigo))
            return ClasseAtivoB3.Acao;

        return ClasseAtivoB3.Outro;
    }

    public static string ExtrairTicker(string? produto)
    {
        var texto = produto?.Trim() ?? "";
        const string Separador = " - ";
        var indice = texto.IndexOf(Separador, StringComparison.Ordinal);
        var bruto = indice < 0 ? texto : texto[..indice];
        return bruto.Trim().ToUpperInvariant();
    }

    private static bool EhRendaFixa(string ticker, string produtoNormalizado)
    {
        if (ticker is "CDB" or "LCI" or "LCA")
            return true;

        if (produtoNormalizado is "cdb" or "lci" or "lca"
            || produtoNormalizado.StartsWith("cdb ", StringComparison.Ordinal)
            || produtoNormalizado.StartsWith("cdb-", StringComparison.Ordinal)
            || produtoNormalizado.StartsWith("lci ", StringComparison.Ordinal)
            || produtoNormalizado.StartsWith("lci-", StringComparison.Ordinal)
            || produtoNormalizado.StartsWith("lca ", StringComparison.Ordinal)
            || produtoNormalizado.StartsWith("lca-", StringComparison.Ordinal))
            return true;

        return ContemPalavra(produtoNormalizado, "tesouro");
    }

    private static bool EhFii(string ticker, string produtoNormalizado)
        => ticker.EndsWith("11", StringComparison.Ordinal)
           && (ContemPalavra(produtoNormalizado, "fii")
               || produtoNormalizado.Contains("fiagro", StringComparison.Ordinal)
               || produtoNormalizado.Contains("imob", StringComparison.Ordinal));

    private static bool ContemPalavra(string texto, string palavra)
    {
        var indice = 0;
        while ((indice = texto.IndexOf(palavra, indice, StringComparison.Ordinal)) >= 0)
        {
            var antesOk = indice == 0 || !char.IsLetterOrDigit(texto[indice - 1]);
            var fim = indice + palavra.Length;
            var depoisOk = fim == texto.Length || !char.IsLetterOrDigit(texto[fim]);
            if (antesOk && depoisOk)
                return true;

            indice = fim;
        }

        return false;
    }
}
