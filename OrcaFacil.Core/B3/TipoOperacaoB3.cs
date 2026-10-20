namespace OrcaFacil.Core.B3;

/// <summary>
/// Tipo mapeado a partir do texto de Movimentação e, quando a tabela manda, da direção.
/// Não é o cálculo de posição: só classifica a linha.
/// </summary>
public enum TipoOperacaoB3
{
    Compra,
    Venda,
    TransferenciaLiquidacao,
    Rendimento,
    Jcp,
    ResgateAntecipado,
    Atualizacao,
    Aplicacao,
    Ignorada
}
