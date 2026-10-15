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
}

public class ItemCompraCestaFormModel
{
    public int ProdutoCestaId { get; set; }

    public decimal Preco { get; set; }
}
