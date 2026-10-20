namespace OrcaFacil.Core.B3;

/// <summary>
/// Uma linha aceita do extrato. <see cref="Data"/> é a data de liquidação do arquivo
/// e vale como data da operação. <see cref="ValorOperacao"/> é o valor de referência;
/// quantidade × preço não é recalculado. Os campos estáveis para quem for hashear
/// a linha são Data, Tipo, Ticker, Quantidade, ValorOperacao, Instituicao e Linha.
/// </summary>
public sealed record LinhaExtratoB3(
    int Linha,
    DirecaoB3 Direcao,
    DateOnly Data,
    string MovimentacaoBruta,
    TipoOperacaoB3 Tipo,
    string Ticker,
    string ProdutoDescricao,
    ClasseAtivoB3 Classe,
    string Instituicao,
    decimal? Quantidade,
    decimal? PrecoUnitario,
    decimal? ValorOperacao);
