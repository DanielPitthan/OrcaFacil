using System.Globalization;
using System.Text;
using OrcaFacil.Core.Llm;

namespace OrcaFacil.Core.Llm.Avaliacao;

public sealed record AvaliacaoComprovante(
    string Id,
    bool LeituraOk,
    bool TotalCorreto,
    bool DataCorreta,
    int ItensNoGabarito,
    int PrecosCorretos)
{
    public override string ToString() => "AvaliacaoComprovante";
}

public sealed record ResultadoAvaliacaoLeitura(
    IReadOnlyList<AvaliacaoComprovante> Casos,
    decimal TaxaTotal,
    decimal TaxaData,
    decimal TaxaPrecoItem,
    bool AtingeMetas)
{
    public override string ToString() => "ResultadoAvaliacaoLeitura";
}

/// <summary>
/// Compara <see cref="ComprovanteLido"/> com o gabarito. Não chama rede e não guarda segredo.
/// Total e preço: igualdade em centavos após <see cref="MidpointRounding.AwayFromZero"/>.
/// Com a mesma contagem e a descrição normalizada na mesma posição, o preço casa pela ordem.
/// Se a contagem diferir ou a ordem não bater, casa pela descrição
/// (<see cref="NormalizadorComprovante.TextoComparacao"/>). Cada item lido casa no máximo uma vez.
/// Item a mais na leitura não soma acerto.
/// Falha de leitura (tipo diferente de Ok, ou comprovante nulo) entra como erro nas três métricas.
/// <see cref="ComprovanteLido.Legivel"/> falso com tipo Ok continua lido: as métricas saem do conteúdo.
/// </summary>
public static class AvaliadorLeituraComprovante
{
    private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;

    public static AvaliacaoComprovante Avaliar(GabaritoComprovante gabarito, ResultadoMensagem resultado)
    {
        ArgumentNullException.ThrowIfNull(gabarito);
        ArgumentNullException.ThrowIfNull(gabarito.Itens);
        ArgumentNullException.ThrowIfNull(resultado);

        var itensNoGabarito = gabarito.Itens.Count;
        if (resultado.Tipo != TipoResultadoMensagem.Ok || resultado.Comprovante is null)
        {
            return new AvaliacaoComprovante(
                gabarito.Id,
                LeituraOk: false,
                TotalCorreto: false,
                DataCorreta: false,
                itensNoGabarito,
                PrecosCorretos: 0);
        }

        var lido = resultado.Comprovante;
        var totalCorreto = lido.TotalCupom is decimal total && PrecosIguais(total, gabarito.TotalCupom);
        var dataCorreta = lido.Data == gabarito.Data;
        return new AvaliacaoComprovante(
            gabarito.Id,
            LeituraOk: true,
            totalCorreto,
            dataCorreta,
            itensNoGabarito,
            ContarPrecosCorretos(gabarito, lido));
    }

    public static ResultadoAvaliacaoLeitura Consolidar(
        IReadOnlyList<AvaliacaoComprovante> casos,
        MetasLeituraComprovante metas)
    {
        ArgumentNullException.ThrowIfNull(casos);
        ArgumentNullException.ThrowIfNull(metas);

        var totaisCertos = 0;
        var datasCertas = 0;
        var precosCertos = 0;
        var itens = 0;
        foreach (var caso in casos)
        {
            if (caso.TotalCorreto)
                totaisCertos++;
            if (caso.DataCorreta)
                datasCertas++;
            precosCertos += caso.PrecosCorretos;
            itens += caso.ItensNoGabarito;
        }

        var taxaTotal = Taxa(totaisCertos, casos.Count);
        var taxaData = Taxa(datasCertas, casos.Count);
        var taxaPreco = Taxa(precosCertos, itens);
        var atinge = Atinge(taxaTotal, metas.TotalMinimo)
            && Atinge(taxaData, metas.DataMinima)
            && Atinge(taxaPreco, metas.PrecoItemMinimo);

        return new ResultadoAvaliacaoLeitura(casos.ToArray(), taxaTotal, taxaData, taxaPreco, atinge);
    }

    /// <summary>
    /// Markdown só com id e taxas. Não inclui chave, cabeçalho, imagem, base64 nem caminho.
    /// </summary>
    public static string Relatorio(ResultadoAvaliacaoLeitura resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(resultado.Casos);

        var texto = new StringBuilder();
        texto.AppendLine("# Avaliação da leitura do comprovante");
        texto.AppendLine();
        LinhaTaxa(texto, "Taxa do total", resultado.TaxaTotal);
        LinhaTaxa(texto, "Taxa da data", resultado.TaxaData);
        LinhaTaxa(texto, "Taxa do preço por item", resultado.TaxaPrecoItem);
        texto.Append("Metas: ").AppendLine(resultado.AtingeMetas ? "atingidas" : "não atingidas");
        texto.AppendLine();
        texto.AppendLine("| Id | Leitura | Total | Data | Preços |");
        texto.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var caso in resultado.Casos)
        {
            texto.Append("| ").Append(caso.Id);
            texto.Append(" | ").Append(caso.LeituraOk ? "ok" : "erro");
            texto.Append(" | ").Append(caso.TotalCorreto ? "certo" : "errado");
            texto.Append(" | ").Append(caso.DataCorreta ? "certo" : "errado");
            texto.Append(" | ").Append(caso.PrecosCorretos.ToString(Invariante));
            texto.Append('/').Append(caso.ItensNoGabarito.ToString(Invariante));
            texto.AppendLine(" |");
        }

        return texto.ToString();
    }

    private static int ContarPrecosCorretos(GabaritoComprovante gabarito, ComprovanteLido lido)
    {
        var itens = lido.Itens;
        if (itens.Count == gabarito.Itens.Count && OrdemAlinhada(gabarito, itens))
            return ContarPorOrdem(gabarito, itens);

        return ContarPorDescricao(gabarito, itens);
    }

    private static bool OrdemAlinhada(GabaritoComprovante gabarito, IReadOnlyList<ItemComprovanteLido> itens)
    {
        for (var i = 0; i < gabarito.Itens.Count; i++)
        {
            if (!MesmaDescricao(gabarito.Itens[i].Descricao, itens[i].DescricaoLida))
                return false;
        }

        return true;
    }

    private static int ContarPorOrdem(GabaritoComprovante gabarito, IReadOnlyList<ItemComprovanteLido> itens)
    {
        var acertos = 0;
        for (var i = 0; i < gabarito.Itens.Count; i++)
        {
            if (PrecosIguais(itens[i].Preco, gabarito.Itens[i].Preco))
                acertos++;
        }

        return acertos;
    }

    private static int ContarPorDescricao(GabaritoComprovante gabarito, IReadOnlyList<ItemComprovanteLido> itens)
    {
        var usados = new bool[itens.Count];
        var acertos = 0;
        foreach (var esperado in gabarito.Itens)
        {
            for (var i = 0; i < itens.Count; i++)
            {
                if (usados[i] || !MesmaDescricao(esperado.Descricao, itens[i].DescricaoLida))
                    continue;

                usados[i] = true;
                if (PrecosIguais(itens[i].Preco, esperado.Preco))
                    acertos++;
                break;
            }
        }

        return acertos;
    }

    private static bool MesmaDescricao(string esperado, string? lido) =>
        string.Equals(
            NormalizadorComprovante.TextoComparacao(esperado),
            NormalizadorComprovante.TextoComparacao(lido),
            StringComparison.Ordinal);

    private static bool PrecosIguais(decimal lido, decimal esperado) =>
        Centavos(lido) == Centavos(esperado);

    private static decimal Centavos(decimal valor) =>
        Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    private static decimal Taxa(int acertos, int total)
    {
        if (total <= 0)
            return 0m;

        return Math.Round(acertos * 100m / total, 2, MidpointRounding.AwayFromZero);
    }

    private static bool Atinge(decimal taxa, decimal minimo) => taxa >= minimo;

    private static void LinhaTaxa(StringBuilder texto, string rotulo, decimal taxa)
    {
        texto.Append(rotulo).Append(": ").Append(taxa.ToString("0.00", Invariante)).AppendLine("%");
    }
}
