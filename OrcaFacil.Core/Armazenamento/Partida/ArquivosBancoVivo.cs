namespace OrcaFacil.Core.Armazenamento.Partida;

/// <summary>O arquivo principal e os laterais que o SQLite pode ter aberto ao lado.</summary>
internal static class ArquivosBancoVivo
{
    public static readonly string[] Sufixos = ["", "-wal", "-shm", "-journal"];

    public static IEnumerable<string> Existentes(string caminhoBanco)
    {
        foreach (var sufixo in Sufixos)
        {
            var caminho = caminhoBanco + sufixo;
            if (ExisteArquivo(caminho))
                yield return caminho;
        }
    }

    public static bool ExisteArquivo(string caminho)
    {
        try
        {
            return File.Exists(caminho);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
