namespace OrcaFacil.Core.Llm;

public enum TipoResultadoMensagem
{
    Ok,
    NaoAutorizado,
    Limite,
    Sobrecarga,
    Timeout,
    PedidoInvalido,
    RespostaInvalida,
    SemRede,
    Falha
}

public enum ConfiancaLeitura
{
    Alta,
    Media,
    Baixa
}

/// <summary>Leitura de um comprovante. Quantidade e unidade são só apoio.</summary>
public sealed record ComprovanteLido(
    string? Loja,
    DateOnly? Data,
    decimal? TotalCupom,
    bool Legivel,
    IReadOnlyList<ItemComprovanteLido> Itens)
{
    public override string ToString() => "ComprovanteLido";
}

public sealed record ItemComprovanteLido(
    string DescricaoLida,
    string Produto,
    bool Reconhecido,
    decimal Preco,
    string? Quantidade,
    string? Unidade,
    decimal? Desconto,
    ConfiancaLeitura? Confianca)
{
    public override string ToString() => "ItemComprovanteLido";
}

/// <summary>
/// Resultado explícito de uma chamada. A mensagem é fixa: não carrega chave, corpo, imagem nem caminho.
/// </summary>
public sealed record ResultadoMensagem(
    TipoResultadoMensagem Tipo,
    string Mensagem,
    ComprovanteLido? Comprovante = null,
    TimeSpan? RetryAfter = null)
{
    public const string TextoNaoAutorizado = "A chave da API foi recusada.";
    public const string TextoLimite = "O provedor limitou o uso agora.";
    public const string TextoSobrecarga = "O provedor está sobrecarregado agora.";
    public const string TextoTimeout = "A leitura demorou demais e foi interrompida.";
    public const string TextoPedidoInvalido = "O pedido foi recusado.";
    public const string TextoRespostaInvalida = "A resposta veio fora do formato esperado.";
    public const string TextoSemRede = "Não foi possível conectar ao provedor.";
    public const string TextoFalha = "O provedor não concluiu a leitura.";

    public bool Sucesso => Tipo == TipoResultadoMensagem.Ok;

    public static ResultadoMensagem Ok(ComprovanteLido comprovante) =>
        new(TipoResultadoMensagem.Ok, string.Empty, comprovante);

    public static ResultadoMensagem Falha(
        TipoResultadoMensagem tipo,
        string mensagem,
        TimeSpan? retryAfter = null) =>
        new(tipo, mensagem, null, retryAfter);

    public override string ToString() => $"ResultadoMensagem({Tipo})";
}
