namespace OrcaFacil.Core.Armazenamento;

/// <summary>
/// O que a tela e o registro dizem depois de limpar.
/// A frase é fixa: não carrega a chave nem o texto da exceção do cofre.
/// </summary>
public readonly record struct ResultadoLimpeza
{
    public const string TextoAplicacaoZerada = "Aplicação zerada";

    public const string TextoChaveNaoApagada =
        "A chave da API não pôde ser apagada. Tente de novo ou remova o app.";

    public const string TextoDadosRemovidos = "Dados removidos";

    public const string TextoRegistroZerada = "Aplicação zerada (todos os dados removidos)";

    public bool ZerouAplicacao { get; }

    public bool ChaveApagada { get; }

    public string Mensagem { get; }

    public string TextoRegistro { get; }

    /// <summary>Limpeza total em que a chave pode ter ficado no cofre.</summary>
    public bool ChavePermaneceu => ZerouAplicacao && !ChaveApagada;

    private ResultadoLimpeza(bool zerouAplicacao, bool chaveApagada, string mensagem, string textoRegistro)
    {
        ZerouAplicacao = zerouAplicacao;
        ChaveApagada = chaveApagada;
        Mensagem = mensagem;
        TextoRegistro = textoRegistro;
    }

    public static ResultadoLimpeza Total(bool chaveApagada) => chaveApagada
        ? new(true, true, TextoAplicacaoZerada, TextoRegistroZerada)
        : new(true, false, TextoChaveNaoApagada, TextoChaveNaoApagada);

    public static ResultadoLimpeza Parcial(string descricao) =>
        new(false, true, TextoDadosRemovidos, $"Dados removidos: {descricao}");

    public static ResultadoLimpeza Nada() => new(false, true, "", "");

    public override string ToString() => ChavePermaneceu
        ? "ResultadoLimpeza(falha)"
        : ZerouAplicacao ? "ResultadoLimpeza(zerada)" : "ResultadoLimpeza(parcial)";
}
