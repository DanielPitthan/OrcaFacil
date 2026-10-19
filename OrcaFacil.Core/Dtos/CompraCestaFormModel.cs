using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Dtos;

public class CompraCestaFormModel
{
    public int? Id { get; set; }

    public int LocalCompraId { get; set; }

    public DateTime Data { get; set; } = DateTime.Today;

    public decimal ValorBeneficio { get; set; }

    public TipoCartaoBeneficio? TipoCartaoBeneficio { get; set; }

    public int? CartaoBeneficioId { get; set; }

    public decimal ValorDinheiro { get; set; }

    public decimal ValorCartaoCredito { get; set; }

    public int? CartaoCreditoId { get; set; }

    /// <summary>Fatura existente escolhida pelo usuário para lançar o crédito; nulo cria uma nova fatura.</summary>
    public int? FaturaTransacaoId { get; set; }

    public List<ItemCompraCestaFormModel> Itens { get; set; } = new();

    /// <summary>Produtos confirmados na prévia e ainda não gravados. Entram na mesma transação da compra.</summary>
    public List<ProdutoNovoCompra> ProdutosNovos { get; set; } = new();
}

public class ItemCompraCestaFormModel
{
    public int ProdutoCestaId { get; set; }

    /// <summary>Chave do produto novo guardado na prévia. A gravação da compra resolve o id.</summary>
    public string? ProdutoNovoChave { get; set; }

    public decimal Preco { get; set; }
}

public class ProdutoNovoCompra
{
    public string Chave { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    public int CategoriaId { get; set; }

    /// <summary>Posição do item na compra. A prévia deixa em zero; o serviço preenche ao gravar.</summary>
    public int IndiceItem { get; set; }
}
