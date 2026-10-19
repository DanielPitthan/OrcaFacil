namespace OrcaFacil.Core.Exportacao;

/// <summary>
/// Texto de erro da exportação. A mensagem do teto de linhas do Excel passa;
/// caminho, URI, senha e rastreio não.
/// </summary>
public static class MensagemExportacaoPlanilha
{
    public const string Generica = "Não foi possível exportar a planilha. Tente de novo.";
    public const string Compartilhamento = "Não foi possível compartilhar a planilha. Tente de novo.";

    private static readonly string[] Prefixos =
    [
        "A aba ",
        "A exportação parou:",
        "Prazo de aviso desconhecido:",
        "Tema desconhecido:",
        "Papel desconhecido:",
        "Tipo de lançamento desconhecido:",
        "Recorrência desconhecida:",
        "Natureza desconhecida:",
        "Tipo de ativo desconhecido:",
        "Operação desconhecida:",
        "Tipo de cartão desconhecido:",
        "Tipo de benefício desconhecido:"
    ];

    private static readonly string[] Segredos =
    [
        "://",
        "securestorage",
        "senha",
        "password",
        "chave",
        "token",
        "bearer",
        "content:",
        "file:",
        "\\",
        "/",
        "uri",
        "api_key",
        "apikey"
    ];

    /// <summary>
    /// <c>null</c> quando a pessoa cancelou e não há o que avisar.
    /// Caso contrário, a frase do exportador, se for uma das conhecidas, ou o texto genérico.
    /// </summary>
    public static string? Para(Exception? excecao)
    {
        if (excecao is null)
            return null;

        for (var atual = excecao; atual is not null; atual = atual.InnerException)
        {
            if (atual is OperationCanceledException)
                return null;

            if (atual is InvalidOperationException && PodeMostrar(atual.Message))
                return atual.Message;
        }

        return Generica;
    }

    public static bool PodeMostrar(string? mensagem)
    {
        if (string.IsNullOrWhiteSpace(mensagem) || mensagem.Length > 240)
            return false;

        foreach (var caractere in mensagem)
        {
            if (caractere is '\r' or '\n' or '\t' or < ' ')
                return false;
        }

        var conhecido = false;
        foreach (var prefixo in Prefixos)
        {
            if (mensagem.StartsWith(prefixo, StringComparison.Ordinal))
            {
                conhecido = true;
                break;
            }
        }

        if (!conhecido)
            return false;

        foreach (var segredo in Segredos)
        {
            if (mensagem.Contains(segredo, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}
