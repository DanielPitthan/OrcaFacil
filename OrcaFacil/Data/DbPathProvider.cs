namespace OrcaFacil.Data;

public static class DbPathProvider
{
    private const string NomeArquivo = "orcafacil.db";

    public static string GetDbPath() => Path.Combine(FileSystem.AppDataDirectory, NomeArquivo);

    public static string GetConnectionString() => $"Data Source={GetDbPath()}";
}
