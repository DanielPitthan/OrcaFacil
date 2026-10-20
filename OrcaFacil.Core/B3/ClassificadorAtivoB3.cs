using System.Text;
using OrcaFacil.Core.Ativos;

namespace OrcaFacil.Core.B3;

/// <summary>
/// Heurística de classe, nesta ordem:
/// <list type="number">
/// <item>CDB, LCI ou LCA no início do produto, ou a palavra Tesouro no produto → <see cref="ClasseAtivoB3.RendaFixa"/>.</item>
/// <item>Ticker terminando em 11 e nome com FII, Fiagro ou o radical imobiliário "imob"
/// (IMOB, IMOBI, Imobiliário) → <see cref="ClasseAtivoB3.FII"/>. Fiagro conta como FII.
/// O sufixo fracionário F não é tratado como 11.</item>
/// <item>Demais tickers válidos na B3 → <see cref="ClasseAtivoB3.Acao"/>. Fundo de índice
/// (BOVA11, GOLD11, SMAL11) cai aqui: o contrato não tem classe de ETF.</item>
/// <item><see cref="ClasseAtivoB3.Outro"/> quando o identificador não é renda fixa nem ticker da B3.
/// O parser não emite essa classe: a linha vai para erros por ticker inválido.</item>
/// </list>
/// Na renda fixa o ticker é o código do papel, não o prefixo CDB/LCI/LCA.
/// CDB, LCI e LCA usam o segmento entre os separadores " - " (ex.: CDB9268YCA7).
/// Tesouro usa o nome do título, em maiúsculas e com espaços simples (ex.: TESOURO IPCA+ 2029).
/// Sem código válido a linha não entra no resultado.
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
        var indice = texto.IndexOf(Separador, StringComparison.Ordinal);
        var bruto = indice < 0 ? texto : texto[..indice];
        return bruto.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Código do papel de renda fixa. CDB/LCI/LCA exigem um código alfanumérico
    /// depois do prefixo, maior que o próprio prefixo. Tesouro exige um nome além da palavra.
    /// </summary>
    public static bool TentarCodigoRendaFixa(string? produto, out string codigo)
    {
        codigo = "";
        var texto = produto?.Trim() ?? "";
        if (texto.Length == 0)
            return false;

        if (TentarPrefixoBancario(texto, out _))
        {
            var partes = texto.Split(Separador);
            if (partes.Length < 2)
                return false;

            var candidato = partes[1].Trim().ToUpperInvariant();
            if (!CodigoBancarioValido(candidato))
                return false;

            codigo = candidato;
            return true;
        }

        var titulo = NormalizarTitulo(texto);
        if (!TituloTesouroValido(titulo))
            return false;

        codigo = titulo;
        return true;
    }

    private const string Separador = " - ";

    private static bool TentarPrefixoBancario(string texto, out string prefixo)
    {
        var indice = texto.IndexOf(Separador, StringComparison.Ordinal);
        var primeiro = (indice < 0 ? texto : texto[..indice]).Trim().ToUpperInvariant();
        if (primeiro is "CDB" or "LCI" or "LCA")
        {
            prefixo = primeiro;
            return true;
        }

        prefixo = "";
        return false;
    }

    private static bool CodigoBancarioValido(string codigo)
    {
        if (codigo.Length < 4)
            return false;

        var prefixo = codigo.StartsWith("CDB", StringComparison.Ordinal) ? "CDB"
            : codigo.StartsWith("LCI", StringComparison.Ordinal) ? "LCI"
            : codigo.StartsWith("LCA", StringComparison.Ordinal) ? "LCA"
            : null;
        if (prefixo is null || codigo.Length == prefixo.Length)
            return false;

        foreach (var caractere in codigo)
        {
            var letra = caractere is >= 'A' and <= 'Z';
            var digito = caractere is >= '0' and <= '9';
            if (!letra && !digito)
                return false;
        }

        return true;
    }

    private static bool TituloTesouroValido(string titulo)
    {
        const string Palavra = "TESOURO";
        if (!titulo.StartsWith(Palavra, StringComparison.Ordinal))
            return false;

        return titulo.Length > Palavra.Length && titulo[Palavra.Length] == ' ' && titulo[(Palavra.Length + 1)..].Length > 0;
    }

    private static string NormalizarTitulo(string texto)
    {
        var sb = new StringBuilder(texto.Length);
        var espacoPendente = false;
        foreach (var caractere in texto.Trim().ToUpperInvariant())
        {
            if (char.IsWhiteSpace(caractere))
            {
                if (sb.Length > 0)
                    espacoPendente = true;
                continue;
            }

            if (espacoPendente)
            {
                sb.Append(' ');
                espacoPendente = false;
            }

            sb.Append(caractere);
        }

        return sb.ToString();
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
