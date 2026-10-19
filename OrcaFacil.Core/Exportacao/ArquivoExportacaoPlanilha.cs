using System.Globalization;

namespace OrcaFacil.Core.Exportacao;

/// <summary>
/// Nome e limpeza do xlsx no cache do app. Só entra o padrão
/// <c>orcafacil-AAAAMMDD-HHmmss-hhhhhhhh.xlsx</c> (8 hex minúsculos) no topo da pasta.
/// Subpastas, CSV, espelho e qualquer outro arquivo ficam de fora.
/// Um arquivo reservado pela gravação em andamento também fica.
/// </summary>
public static class ArquivoExportacaoPlanilha
{
    public const string Prefixo = "orcafacil-";
    public const string Sufixo = ".xlsx";
    public const int TamanhoSufixo = 8;

    private static readonly object PortaReserva = new();
    private static readonly HashSet<string> Reservados = new(StringComparer.Ordinal);

    /// <summary>
    /// MIME do .xlsx. No Android, <c>ShareFile(caminho)</c> obtém este valor
    /// pela extensão, via MimeTypeMap. O FileProvider do MAUI já publica o cache.
    /// </summary>
    public const string Mime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>8 caracteres de <c>Guid</c> no formato <c>N</c>, minúsculos.</summary>
    public static string NovoSufixo() => Guid.NewGuid().ToString("N")[..TamanhoSufixo];

    public static bool SufixoValido(string? sufixo)
    {
        if (sufixo is null || sufixo.Length != TamanhoSufixo)
            return false;

        foreach (var caractere in sufixo)
        {
            if (caractere is not (>= '0' and <= '9' or >= 'a' and <= 'f'))
                return false;
        }

        return true;
    }

    public static string Nome(DateTime instante, string sufixo)
    {
        if (!SufixoValido(sufixo))
            throw new ArgumentException("O sufixo da planilha precisa ter 8 caracteres hexadecimais.", nameof(sufixo));

        var texto = instante.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return Prefixo + texto + "-" + sufixo + Sufixo;
    }

    public static bool NomeEhDesteApp(string? nome)
    {
        if (string.IsNullOrEmpty(nome))
            return false;

        if (!nome.StartsWith(Prefixo, StringComparison.Ordinal) || !nome.EndsWith(Sufixo, StringComparison.Ordinal))
            return false;

        var meio = nome[Prefixo.Length..^Sufixo.Length];
        if (meio.Length != 24 || meio[8] != '-' || meio[15] != '-')
            return false;

        for (var i = 0; i < meio.Length; i++)
        {
            if (i is 8 or 15)
                continue;

            var caractere = meio[i];
            if (i < 15)
            {
                if (caractere is < '0' or > '9')
                    return false;
            }
            else if (caractere is not (>= '0' and <= '9' or >= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }

    public static void Reservar(string caminho)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminho);
        lock (PortaReserva)
            Reservados.Add(Path.GetFullPath(caminho));
    }

    public static void Liberar(string? caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho))
            return;

        try
        {
            var cheio = Path.GetFullPath(caminho);
            lock (PortaReserva)
                Reservados.Remove(cheio);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
        }
    }

    public static bool EstaReservado(string? caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho))
            return false;

        try
        {
            var cheio = Path.GetFullPath(caminho);
            lock (PortaReserva)
                return Reservados.Contains(cheio);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            return false;
        }
    }

    public static string Caminho(string diretorioCache, DateTime instante, string sufixo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diretorioCache);
        var nome = Nome(instante, sufixo);
        if (!NomeEhDesteApp(nome))
            throw new InvalidOperationException("Nome de arquivo da planilha recusado.");

        var raiz = NormalizarDiretorio(diretorioCache);
        if (EhRaizDeVolume(raiz))
            throw new InvalidOperationException("A pasta de cache da planilha foi recusada.");

        var caminho = Path.GetFullPath(Path.Combine(raiz, nome));
        if (!EstaDiretoNaPasta(caminho, raiz))
            throw new InvalidOperationException("O arquivo da planilha sairia da pasta de cache.");

        return caminho;
    }

    /// <summary>
    /// Apaga os xlsx anteriores deste app. Não desce para subpastas e não
    /// aceita a raiz do volume.
    /// </summary>
    public static int ApagarAnteriores(string? diretorioCache)
    {
        if (!TryNormalizarPasta(diretorioCache, out var raiz) || EhRaizDeVolume(raiz))
            return 0;

        if (!Directory.Exists(raiz))
            return 0;

        var apagados = 0;
        foreach (var caminho in Directory.EnumerateFiles(raiz))
        {
            if (EstaReservado(caminho))
                continue;

            if (ApagarSeForDesteApp(caminho, raiz))
                apagados++;
        }

        return apagados;
    }

    public static bool ApagarSeForDesteApp(string? caminho, string? diretorioCache)
    {
        if (!TryNormalizarPasta(diretorioCache, out var raiz) || EhRaizDeVolume(raiz))
            return false;

        if (string.IsNullOrWhiteSpace(caminho))
            return false;

        string cheio;
        try
        {
            cheio = Path.GetFullPath(caminho);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            return false;
        }

        if (!NomeEhDesteApp(Path.GetFileName(cheio)) || !EstaDiretoNaPasta(cheio, raiz))
            return false;

        try
        {
            if (Directory.Exists(cheio))
                return false;

            var atributos = File.GetAttributes(cheio);
            if (atributos.HasFlag(FileAttributes.Directory) || atributos.HasFlag(FileAttributes.ReparsePoint))
                return false;

            File.Delete(cheio);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryNormalizarPasta(string? diretorio, out string raiz)
    {
        raiz = "";
        if (string.IsNullOrWhiteSpace(diretorio))
            return false;

        try
        {
            raiz = NormalizarDiretorio(diretorio);
            return raiz.Length > 0 && !EhRaizDeVolume(raiz);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            return false;
        }
    }

    private static string NormalizarDiretorio(string diretorio)
    {
        var cheio = Path.GetFullPath(diretorio);
        return cheio.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool EstaDiretoNaPasta(string caminhoCompleto, string raiz)
    {
        var pasta = Path.GetDirectoryName(caminhoCompleto);
        if (string.IsNullOrEmpty(pasta))
            return false;

        pasta = pasta.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(pasta, raiz, StringComparison.Ordinal);
    }

    private static bool EhRaizDeVolume(string caminho)
    {
        if (string.IsNullOrEmpty(caminho))
            return true;

        var volume = Path.GetPathRoot(caminho);
        if (string.IsNullOrEmpty(volume))
            return false;

        var normalizado = volume.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(normalizado, caminho, StringComparison.Ordinal);
    }
}
