namespace OrcaFacil.Core.B3;

/// <summary>
/// Uma linha aceita do extrato. <see cref="Data"/> é a data de liquidação do arquivo
/// e vale como data da operação. <see cref="ValorOperacao"/> é o valor de referência;
/// quantidade × preço não é recalculado. Em renda fixa, <see cref="Ticker"/> é o código
/// do papel (ou o nome normalizado do Tesouro) e <see cref="ProdutoDescricao"/> guarda o texto completo.
/// Os campos estáveis para quem for hashear a linha são Data, Tipo, Ticker, Quantidade, ValorOperacao e Instituicao.
/// O preço na chave é o efetivo, ValorOperacao / Quantidade, e não o PrecoUnitario lido.
/// O número da linha não entra. Linhas iguais no mesmo arquivo se distinguem pela ocorrência, na ordem em que aparecem.
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
