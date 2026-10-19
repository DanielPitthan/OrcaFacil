namespace OrcaFacil.Core.Armazenamento;

/// <summary>
/// <see cref="Entities.MembroFamilia.FotoPath"/> persiste só o nome do arquivo
/// (por exemplo <c>foto-20261017120000.jpg</c>). Pasta absoluta quebra na
/// reinstalação, porque o diretório interno do app muda.
/// </summary>
/// <remarks>
/// A restauração do espelho grava só o nome, pela <see cref="ExtrairNomeArquivo"/>.
/// Valor que não vira nome permanece. A leitura só abre arquivo dentro da pasta
/// <c>perfil</c>, depois de normalizar o caminho. Null é "sem foto"; qualquer outro
/// valor persistido é só o nome.
/// </remarks>
public static class FotoPath
{
    /// <summary>Igual ao MaxLength da coluna FotoPath. Não encurtar: isso pediria migration.</summary>
    public const int TamanhoMaximo = 260;

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

    /// <summary>
    /// Caminho absoluto dentro de <paramref name="pastaFotos"/>, ou null.
    /// O prefixo da pasta, com separador no fim, é exigido antes de <see cref="File.Exists"/>.
    /// Nome sem diretório é lido nessa pasta, não na pasta corrente.
    /// </summary>
    public static string? CaminhoDentroDaPasta(string pastaFotos, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor) || string.IsNullOrWhiteSpace(pastaFotos))
            return null;

        var candidato = EhSomenteNomeArquivo(valor)
            ? Path.Combine(pastaFotos, valor)
            : valor;

        string cheio;
        string prefixo;
        try
        {
            cheio = Path.GetFullPath(candidato);
            var raiz = Path.GetFullPath(pastaFotos);
            prefixo = raiz.EndsWith(Path.DirectorySeparatorChar)
                ? raiz
                : raiz + Path.DirectorySeparatorChar;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            return null;
        }

        if (!cheio.StartsWith(prefixo, StringComparison.Ordinal))
            return null;

        try
        {
            return File.Exists(cheio) ? cheio : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
