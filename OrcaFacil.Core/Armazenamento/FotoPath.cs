namespace OrcaFacil.Core.Armazenamento;

/// <summary>
/// <see cref="Entities.MembroFamilia.FotoPath"/> persiste só o nome do arquivo
/// (por exemplo <c>foto-20261017120000.jpg</c>). Pasta absoluta quebra na
/// reinstalação, porque o diretório interno do app muda.
/// </summary>
/// <remarks>
/// A restauração do espelho grava só o nome, pela <see cref="ExtrairNomeArquivo"/>.
/// Valor que não vira nome permanece. A leitura só abre arquivo cujo caminho final
/// está dentro da pasta <c>perfil</c>. Caminho absoluto de outro aparelho tenta o
/// mesmo nome nessa pasta. Link simbólico que aponta para fora é recusado.
/// Null é "sem foto"; qualquer outro valor persistido é só o nome.
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
    /// Caminho final dentro de <paramref name="pastaFotos"/>, ou null.
    /// O prefixo da pasta, com separador no fim, é exigido antes de <see cref="File.Exists"/>.
    /// Nome sem diretório é lido nessa pasta, não na pasta corrente.
    /// Caminho absoluto de outro aparelho, fora da pasta atual, cai no nome do arquivo
    /// dentro dela (caso do Auto Backup). Link simbólico é resolvido com
    /// <see cref="FileSystemInfo.ResolveLinkTarget(bool)"/>; o alvo final também
    /// precisa ficar sob o prefixo. Pasta irmã (<c>perfil2</c>) e outra caixa não entram.
    /// </summary>
    public static string? CaminhoDentroDaPasta(string pastaFotos, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor) || string.IsNullOrWhiteSpace(pastaFotos))
            return null;

        if (EhSomenteNomeArquivo(valor))
            return AbrirSeDentro(pastaFotos, Path.Combine(pastaFotos, valor));

        if (AbrirSeDentro(pastaFotos, valor) is string direto)
            return direto;

        try
        {
            var nome = ExtrairNomeArquivo(valor);
            return AbrirSeDentro(pastaFotos, Path.Combine(pastaFotos, nome));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? AbrirSeDentro(string pastaFotos, string candidato)
    {
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

        var final = ResolverSobPrefixo(prefixo, cheio);
        if (final is null)
            return null;

        try
        {
            return File.Exists(final) ? final : null;
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

    /// <summary>
    /// <see cref="Path.GetFullPath"/> não segue link. Cada segmento é conferido;
    /// se for link, o alvo final precisa continuar sob o prefixo.
    /// </summary>
    private static string? ResolverSobPrefixo(string prefixo, string cheio)
    {
        var raiz = prefixo[..^1];
        var relativo = cheio[prefixo.Length..];
        if (relativo.Length == 0)
            return null;

        var atual = raiz;
        foreach (var parte in relativo.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            if (parte is "." or "..")
                return null;

            atual = Path.Combine(atual, parte);
            atual = ResolverLink(atual, prefixo) ?? "";
            if (atual.Length == 0)
                return null;
        }

        return atual.StartsWith(prefixo, StringComparison.Ordinal) ? atual : null;
    }

    private static string? ResolverLink(string caminho, string prefixo)
    {
        try
        {
            var info = new FileInfo(caminho);
            if (info.LinkTarget is null)
            {
                var direto = Path.GetFullPath(caminho);
                return direto.StartsWith(prefixo, StringComparison.Ordinal) ? direto : null;
            }

            var alvo = info.ResolveLinkTarget(returnFinalTarget: true);
            if (alvo is null)
                return null;

            var final = Path.GetFullPath(alvo.FullName);
            return final.StartsWith(prefixo, StringComparison.Ordinal) ? final : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
