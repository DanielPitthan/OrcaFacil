namespace OrcaFacil.Core.Armazenamento.Espelho.Pasta;

/// <summary>
/// Arquivos temporários num diretório do app. <see cref="Descartar"/> ignora caminho
/// fora dessa raiz, para não servir de atalho até a pasta do usuário.
/// </summary>
public sealed class AreaTemporariaEspelhoEmDisco : IAreaTemporariaEspelho
{
    private readonly string _raiz;

    public AreaTemporariaEspelhoEmDisco(string raiz)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raiz);
        _raiz = Path.GetFullPath(raiz);
        Directory.CreateDirectory(_raiz);
    }

    public string CriarArquivo()
    {
        var caminho = Path.Combine(_raiz, Guid.NewGuid().ToString("N") + ".bin");
        using var _ = new FileStream(caminho, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        return caminho;
    }

    public void Descartar(string caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho))
            return;

        string cheio;
        try
        {
            cheio = Path.GetFullPath(caminho);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return;
        }

        var prefixo = _raiz.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!cheio.StartsWith(prefixo, StringComparison.Ordinal))
            return;

        try
        {
            if (File.Exists(cheio))
                File.Delete(cheio);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
