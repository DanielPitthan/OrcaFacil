using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Llm;

namespace OrcaFacil.Core.Cesta;

/// <summary>
/// Confirma o par na própria linha da prévia. Confiança e pendente não entram na decisão.
/// </summary>
public static class ConfirmacaoParProduto
{
    public static bool ExigeConfirmacao(LinhaPreviaComprovante linha)
    {
        ArgumentNullException.ThrowIfNull(linha);
        return linha.ProdutoCestaId is > 0 && !linha.CasamentoConfirmado;
    }

    public static bool PodeGravar(IReadOnlyList<LinhaPreviaComprovante> linhas)
    {
        ArgumentNullException.ThrowIfNull(linhas);
        foreach (var linha in linhas)
        {
            if (linha is not null && linha.ProdutoCestaId is > 0 && !linha.CasamentoConfirmado)
                return false;
        }

        return true;
    }

    public static string? MensagemBloqueio(IReadOnlyList<LinhaPreviaComprovante> linhas) =>
        PodeGravar(linhas) ? null : CasamentoComprovante.MensagemConfirmacaoObrigatoria;

    public static void Confirmar(LinhaPreviaComprovante linha)
    {
        ArgumentNullException.ThrowIfNull(linha);
        if (linha.ProdutoCestaId is not > 0 || linha.CasamentoConfirmado)
            return;

        linha.CasamentoConfirmado = true;
    }

    /// <summary>Escolhe outro produto ativo do catálogo. A escolha confirma o casamento da linha.</summary>
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
        if (linha.ProdutoCestaId is not > 0 || linha.NaoIdentificado)
            return false;
        if (produto.Id <= 0 || !produto.Ativo)
            return false;
        if (!catalogo.Any(item => item is not null && item.Id == produto.Id && item.Ativo))
            return false;

        previa.Classificar(linha, produto);
        linha.CasamentoConfirmado = true;
        return true;
    }

    public static void Desfazer(LinhaPreviaComprovante linha)
    {
        ArgumentNullException.ThrowIfNull(linha);
        if (linha.ProdutoCestaId is not > 0 || linha.NaoIdentificado)
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
        linha.Nome = NormalizadorComprovante.Cortar(linha.DescricaoLida);
    }
}
