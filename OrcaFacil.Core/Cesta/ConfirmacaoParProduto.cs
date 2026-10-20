using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Llm;

namespace OrcaFacil.Core.Cesta;

/// <summary>
/// Trocar e Desfazer na prévia. Os dois desmarcam a linha.
/// A marcação e o Confirmar ficam em <see cref="PreviaComprovante"/>.
/// </summary>
public static class ConfirmacaoParProduto
{
    public static bool PodeAjustar(LinhaPreviaComprovante linha)
    {
        ArgumentNullException.ThrowIfNull(linha);
        return linha.ProdutoCestaId is > 0 && !linha.NaoIdentificado;
    }

    /// <summary>Escolhe outro produto ativo do catálogo e desmarca a linha.</summary>
    public static bool Trocar(
        PreviaComprovante previa,
        LinhaPreviaComprovante linha,
        ProdutoCesta produto,
        IReadOnlyList<ProdutoCesta> catalogo)
    {
        ArgumentNullException.ThrowIfNull(previa);
        ArgumentNullException.ThrowIfNull(linha);
        ArgumentNullException.ThrowIfNull(produto);
        ArgumentNullException.ThrowIfNull(catalogo);
        if (!PodeAjustar(linha))
            return false;
        if (produto.Id <= 0 || !produto.Ativo)
            return false;
        if (!catalogo.Any(item => item is not null && item.Id == produto.Id && item.Ativo))
            return false;

        previa.Classificar(linha, produto);
        linha.Marcado = false;
        linha.CasamentoConfirmado = false;
        return true;
    }

    public static void Desfazer(LinhaPreviaComprovante linha)
    {
        ArgumentNullException.ThrowIfNull(linha);
        if (!PodeAjustar(linha))
            return;

        AplicarNaoIdentificado(linha);
    }

    public static void AplicarNaoIdentificado(LinhaPreviaComprovante linha)
    {
        ArgumentNullException.ThrowIfNull(linha);
        linha.ProdutoCestaId = null;
        linha.CategoriaId = null;
        linha.CategoriaNome = null;
        linha.CategoriaFaltando = true;
        linha.ProdutoInativo = false;
        linha.NaoIdentificado = true;
        linha.NomeNovo = false;
        linha.Ambiguo = false;
        linha.ItemAusente = false;
        linha.CasadoPelaDescricao = false;
        linha.NovoSeraCriado = false;
        linha.ProdutoNovoChave = null;
        linha.PodeDesfazerNovo = false;
        linha.CasamentoConfirmado = false;
        linha.Marcado = false;
        linha.Nome = NormalizadorComprovante.Cortar(linha.DescricaoLida);
    }
}
