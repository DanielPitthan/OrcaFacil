using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Lancamentos;

/// <summary>
/// Inclusão de linhas na fatura. Cada chamada acrescenta um item novo:
/// categoria, valor e período podem se repetir na mesma fatura.
/// </summary>
public static class ItensFaturaInclusao
{
    public const int DescritivoMaximo = 120;

    public static string? NormalizarDescritivo(string? descritivo)
    {
        if (string.IsNullOrWhiteSpace(descritivo))
            return null;

        var texto = descritivo.Trim();
        if (texto.Length > DescritivoMaximo)
            throw new InvalidOperationException($"O descritivo pode ter no máximo {DescritivoMaximo} caracteres.");

        return texto;
    }

    public static bool TemDescritivo(string? descritivo) => !string.IsNullOrWhiteSpace(descritivo);

    /// <summary>Texto principal da linha: o descritivo, quando houver; senão o nome da categoria.</summary>
    public static string Titulo(string? descritivo, string fallback)
        => TemDescritivo(descritivo) ? descritivo!.Trim() : fallback;

    public static ItemLancamentoFormModel Acrescentar(
        ICollection<ItemLancamentoFormModel> itens,
        int categoriaId,
        string nomeCategoria,
        decimal valor,
        NaturezaItemLancamento natureza,
        int? parcelaAtual,
        int? totalParcelas,
        string? descritivo)
    {
        var descricao = string.IsNullOrWhiteSpace(nomeCategoria) ? "Item" : nomeCategoria.Trim();
        if (descricao.Length > 120)
            descricao = descricao[..120];

        var item = new ItemLancamentoFormModel
        {
            Descricao = descricao,
            Descritivo = NormalizarDescritivo(descritivo),
            Valor = valor,
            Natureza = natureza,
            ParcelaAtual = natureza == NaturezaItemLancamento.Parcelada ? parcelaAtual : null,
            TotalParcelas = natureza == NaturezaItemLancamento.Parcelada ? totalParcelas : null,
            CategoriaId = categoriaId
        };

        itens.Add(item);
        return item;
    }
}
