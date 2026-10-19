using OrcaFacil.Core.Dtos;

namespace OrcaFacil.Core.Cesta;

public enum ModoPagamentoCesta
{
    Dinheiro,
    Cartao,
    Dividido
}

public enum SituacaoCompraCesta
{
    Coberta,
    APagar
}

/// <summary>
/// Regras de rascunho da compra da cesta. A persistência continua em <see cref="CestaDespesaRegras"/>.
/// </summary>
public static class CompraCestaEditor
{
    public const int ProdutosEmDestaque = 6;

    public static decimal Saldo(decimal total, decimal beneficioInformado)
        => Math.Max(0, CestaDespesaRegras.Dinheiro(total) - CestaDespesaRegras.Dinheiro(beneficioInformado));

    public static (decimal Dinheiro, decimal Cartao) Pagamento(ModoPagamentoCesta modo, decimal saldo, decimal dinheiroInformado)
    {
        var dinheiro = modo switch
        {
            ModoPagamentoCesta.Cartao => 0,
            ModoPagamentoCesta.Dividido => CestaDespesaRegras.Dinheiro(dinheiroInformado),
            _ => saldo
        };
        return (dinheiro, saldo - dinheiro);
    }

    public static decimal Aproveitamento(decimal total, decimal beneficio)
        => total <= 0 ? 0 : beneficio / total * 100m;

    public static double PercentualBeneficio(decimal total, decimal beneficio)
        => total <= 0 ? 0 : (double)Math.Min(100m, beneficio / total * 100m);

    public static SituacaoCompraCesta? Situacao(decimal total, decimal beneficioInformado, decimal saldo)
    {
        if (total <= 0)
            return null;

        return saldo <= 0 && beneficioInformado >= total
            ? SituacaoCompraCesta.Coberta
            : SituacaoCompraCesta.APagar;
    }

    public static string RotuloSituacao(SituacaoCompraCesta situacao) => situacao switch
    {
        SituacaoCompraCesta.Coberta => "Coberta",
        _ => "A pagar"
    };

    public static string TextoApoioBeneficio(bool usar)
        => usar
            ? "O valor sai do saldo do cartão de benefício."
            : "A compra fica para dinheiro ou cartão de crédito.";

    public static string Contagem(int quantidade) => quantidade switch
    {
        0 => "Nenhum item",
        1 => "1 item",
        _ => $"{quantidade} itens"
    };

    public static string Subtitulo(string? categoria)
        => string.IsNullOrWhiteSpace(categoria) ? "Sem categoria" : categoria.Trim();

    public static string RotuloModo(ModoPagamentoCesta modo) => modo switch
    {
        ModoPagamentoCesta.Cartao => "Cartão de crédito",
        ModoPagamentoCesta.Dividido => "Dividir",
        _ => "Dinheiro"
    };

    public static ModoPagamentoCesta DefinirModo(decimal dinheiro, decimal cartao)
    {
        if (dinheiro > 0 && cartao > 0)
            return ModoPagamentoCesta.Dividido;
        if (cartao > 0)
            return ModoPagamentoCesta.Cartao;
        return ModoPagamentoCesta.Dinheiro;
    }

    public static string? ValidarItem(int produtoId, decimal preco)
    {
        if (produtoId <= 0)
            return "Selecione o produto.";

        if (preco <= 0)
            return "Informe o preço do item.";

        return null;
    }

    public static void AcrescentarOuAtualizar(IList<ItemCompraCestaFormModel> itens, int produtoId, decimal preco)
    {
        var erro = ValidarItem(produtoId, preco);
        if (erro is not null)
            throw new InvalidOperationException(erro);

        var existente = itens.FirstOrDefault(i => i.ProdutoCestaId == produtoId && string.IsNullOrEmpty(i.ProdutoNovoChave));
        if (existente is null)
        {
            itens.Add(new ItemCompraCestaFormModel { ProdutoCestaId = produtoId, Preco = preco });
            return;
        }

        existente.Preco = preco;
    }

    public static void AplicarEdicao(
        IList<ItemCompraCestaFormModel> itens,
        ItemCompraCestaFormModel atual,
        int produtoId,
        decimal preco)
    {
        var erro = ValidarItem(produtoId, preco);
        if (erro is not null)
            throw new InvalidOperationException(erro);

        var outro = itens.FirstOrDefault(i =>
            !ReferenceEquals(i, atual) && i.ProdutoCestaId == produtoId && string.IsNullOrEmpty(i.ProdutoNovoChave));
        if (outro is not null)
        {
            outro.Preco = preco;
            itens.Remove(atual);
            return;
        }

        atual.ProdutoCestaId = produtoId;
        atual.ProdutoNovoChave = null;
        atual.NomePendente = null;
        atual.CategoriaPendente = null;
        atual.Preco = preco;
    }

    public static string? Validar(
        int localId,
        DateTime? data,
        IReadOnlyList<ItemCompraCestaFormModel> itens,
        bool usarBeneficio,
        int cartaoBeneficioId,
        decimal beneficio,
        decimal saldoBeneficio,
        decimal dinheiroEfetivo,
        decimal cartaoEfetivo,
        decimal saldo,
        decimal total,
        int cartaoCreditoId)
    {
        if (localId <= 0)
            return "Selecione o local da compra.";
        if (data is null)
            return "Informe a data da compra.";
        if (itens.Count == 0)
            return "Inclua ao menos um produto.";
        if (itens.Any(l => l.Preco <= 0))
            return "Todo item precisa de um preço maior que zero.";
        if (usarBeneficio && cartaoBeneficioId <= 0)
            return "Selecione o cartão de benefício.";
        if (usarBeneficio && beneficio <= 0)
            return "Informe o valor usado do cartão de benefício.";
        if (usarBeneficio && CestaDespesaRegras.Dinheiro(beneficio) > CestaDespesaRegras.Dinheiro(total))
            return "O benefício não pode ser maior que o total da cesta.";
        if (usarBeneficio && CestaDespesaRegras.Dinheiro(beneficio) > saldoBeneficio)
            return $"Saldo insuficiente no cartão de benefício (disponível: {saldoBeneficio:C}). Recarregue antes de usar.";
        if (dinheiroEfetivo < 0 || cartaoEfetivo < 0)
            return "O dinheiro não pode passar do saldo.";
        if (dinheiroEfetivo + cartaoEfetivo != saldo)
            return "Dinheiro e cartão de crédito precisam somar o saldo da compra.";
        if (cartaoEfetivo > 0 && cartaoCreditoId <= 0)
            return "Selecione o cartão de crédito.";
        return null;
    }

    public static IReadOnlyList<T> EmDestaque<T>(IReadOnlyList<T> todas, Func<T, bool> selecionada, int limite = ProdutosEmDestaque)
        where T : class
    {
        if (limite < 1)
            limite = 1;

        var destaque = todas.Take(limite).ToList();
        var escolhida = todas.FirstOrDefault(selecionada);
        if (escolhida is not null && !destaque.Any(selecionada))
            destaque.Add(escolhida);

        return destaque;
    }
}
