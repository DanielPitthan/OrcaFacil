using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OrcaFacil.Core.B3;

/// <summary>
/// SHA-256 da linha, em hex minúsculo.
/// A chave é Data(yyyy-MM-dd)|Tipo|Ticker|Quantidade|PrecoEfetivo|ValorOperacao|Instituicao,
/// mais o índice de ocorrência dessa chave no arquivo (0, 1, …), na ordem em que aparece.
/// Data é a liquidação do arquivo, a mesma de <c>OperacaoAtivo.Data</c>.
/// O pregão derivado na #104 não entra: reimportar o extrato (#94) precisa achar o mesmo hash.
/// O preço efetivo é ValorOperacao / Quantidade, arredondado a 4 casas para longe de zero.
/// Sem quantidade ou valor, o preço fica vazio. O preço lido no arquivo não entra.
/// Decimais saem sem zeros à direita, em <see cref="CultureInfo.InvariantCulture"/>. Nulo vira vazio.
/// O número da linha não entra: o mesmo período em outro extrato gera o mesmo hash.
/// </summary>
public static class HashLinhaExtratoB3
{
    public static string Chave(LinhaExtratoB3 linha)
    {
        ArgumentNullException.ThrowIfNull(linha);
        return string.Join('|',
            linha.Data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            linha.Tipo.ToString(),
            linha.Ticker,
            Formatar(linha.Quantidade),
            Formatar(PrecoEfetivo(linha)),
            Formatar(linha.ValorOperacao),
            linha.Instituicao);
    }

    public static string Texto(LinhaExtratoB3 linha, int ocorrencia)
    {
        if (ocorrencia < 0)
            throw new ArgumentOutOfRangeException(nameof(ocorrencia));

        return Chave(linha) + "|" + ocorrencia.ToString(CultureInfo.InvariantCulture);
    }

    public static string Calcular(LinhaExtratoB3 linha, int ocorrencia)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(Texto(linha, ocorrencia)));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// A primeira linha de cada chave vale 0. A seguinte igual vale 1, e assim por diante.
    /// A ordem é a da lista, que é a ordem do arquivo.
    /// </summary>
    public static int[] Ocorrencias(IReadOnlyList<LinhaExtratoB3> linhas)
    {
        ArgumentNullException.ThrowIfNull(linhas);
        var vistas = new Dictionary<string, int>(StringComparer.Ordinal);
        var ocorrencias = new int[linhas.Count];
        for (var i = 0; i < linhas.Count; i++)
        {
            var chave = Chave(linhas[i]);
            vistas.TryGetValue(chave, out var ocorrencia);
            ocorrencias[i] = ocorrencia;
            vistas[chave] = ocorrencia + 1;
        }

        return ocorrencias;
    }

    /// <summary>
    /// ValorOperacao / Quantidade, em 4 casas para longe de zero.
    /// Sem quantidade ou sem valor, não há preço efetivo.
    /// Valores extremos lançam <see cref="OverflowException"/>. A prévia captura por linha.
    /// </summary>
    private static decimal? PrecoEfetivo(LinhaExtratoB3 linha)
    {
        if (linha.Quantidade is not > 0 || linha.ValorOperacao is not > 0)
            return null;

        return Math.Round(
            linha.ValorOperacao.Value / linha.Quantidade.Value,
            4,
            MidpointRounding.AwayFromZero);
    }

    public static string Formatar(decimal? valor)
    {
        if (valor is null)
            return "";

        return valor.Value.ToString("0.#############################", CultureInfo.InvariantCulture);
    }
}
