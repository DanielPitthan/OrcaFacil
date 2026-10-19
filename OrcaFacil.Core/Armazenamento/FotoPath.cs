namespace OrcaFacil.Core.Armazenamento;

/// <summary>
/// <see cref="Entities.MembroFamilia.FotoPath"/> persiste só o nome do arquivo
/// (por exemplo <c>foto-20261017120000.jpg</c>). Pasta absoluta quebra na
/// reinstalação, porque o diretório interno do app muda.
/// </summary>
/// <remarks>
/// A restauração do espelho grava só o nome, pela <see cref="ExtrairNomeArquivo"/>.
/// Valor que não vira nome permanece. A leitura no app ainda abre o caminho antigo
/// quando o arquivo existe. Null é "sem foto"; qualquer outro valor persistido é só o nome.
/// </remarks>
public static class FotoPath
{
    public const int TamanhoMaximo = 255;

    private static readonly char[] Proibidos = ['/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    /// <summary>Null significa sem foto. Qualquer outro valor tem de ser só o nome do arquivo.</summary>
    public static bool AceitaValorPersistido(string? valor)
        => valor is null || EhSomenteNomeArquivo(valor);

    public static bool EhSomenteNomeArquivo(string? valor)
        => TentarValidar(valor, out _);

    public static string ExigirNomeArquivo(string valor)
    {
        if (!TentarValidar(valor, out var erro))
            throw new ArgumentException(erro, nameof(valor));

        return valor;
    }

    /// <summary>
    /// De um valor legado com pasta, devolve só o último segmento já validado como nome.
    /// </summary>
    public static string ExtrairNomeArquivo(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new ArgumentException("FotoPath vazio.", nameof(valor));

        var texto = valor.Trim();
        var corte = Math.Max(texto.LastIndexOf('/'), texto.LastIndexOf('\\'));
        var nome = corte < 0 ? texto : texto[(corte + 1)..];
        return ExigirNomeArquivo(nome);
    }

    private static bool TentarValidar(string? valor, out string erro)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            erro = "FotoPath vazio.";
            return false;
        }

        if (valor != valor.Trim())
        {
            erro = "FotoPath não pode ter espaço nas pontas.";
            return false;
        }

        if (valor.Length > TamanhoMaximo)
        {
            erro = "FotoPath longo demais para um nome de arquivo.";
            return false;
        }

        if (valor is "." or "..")
        {
            erro = "FotoPath precisa ser um nome de arquivo.";
            return false;
        }

        if (valor.IndexOfAny(Proibidos) >= 0)
        {
            erro = "FotoPath guarda só o nome do arquivo, sem pasta.";
            return false;
        }

        foreach (var caractere in valor)
        {
            if (char.IsControl(caractere))
            {
                erro = "FotoPath tem caractere de controle.";
                return false;
            }
        }

        erro = "";
        return true;
    }
}
