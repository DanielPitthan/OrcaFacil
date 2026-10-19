namespace OrcaFacil.Core.Armazenamento.Partida;

internal readonly record struct ArquivoDoBanco(string Caminho, string Sufixo);

internal readonly record struct InventarioBanco(IReadOnlyList<ArquivoDoBanco> Arquivos)
{
    public bool TemPrincipal => Arquivos.Any(arquivo => arquivo.Sufixo.Length == 0);
}

/// <summary>O arquivo principal e os laterais que o SQLite pode ter aberto ao lado.</summary>
internal static class ArquivosBancoVivo
{
    public static readonly string[] Sufixos = ["", "-wal", "-shm", "-journal"];

    /// <summary>
    /// Lista o que existe de fato. Se a consulta de um arquivo lança, a lista não segue:
    /// tratar a falha como "ausente" copiaria zero laterais e ainda assim pareceria sucesso.
    /// </summary>
    public static InventarioBanco Inventariar(string caminhoBanco, Func<string, bool>? sonda = null)
    {
        var existe = sonda ?? File.Exists;
        var lista = new List<ArquivoDoBanco>();
        foreach (var sufixo in Sufixos)
        {
            var caminho = caminhoBanco + sufixo;
            bool presente;
            try
            {
                presente = existe(caminho);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new IOException("Não foi possível confirmar se o banco vivo existe. Nada foi apagado.", ex);
            }

            if (presente)
                lista.Add(new ArquivoDoBanco(caminho, sufixo));
        }

        return new InventarioBanco(lista);
    }

    /// <summary>
    /// Consulta frouxa, só para um passo que já não decide se a cópia valeu.
    /// Quem vai substituir o banco usa <see cref="Inventariar"/>.
    /// </summary>
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
