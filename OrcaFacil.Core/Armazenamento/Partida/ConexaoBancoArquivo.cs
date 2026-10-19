using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;

namespace OrcaFacil.Core.Armazenamento.Partida;

/// <summary>
/// Abre o SQLite do banco vivo. A leitura usa <c>Mode=ReadOnly</c> e não cria o arquivo.
/// O pool fica desligado para a troca do arquivo não esbarrar numa conexão antiga.
/// </summary>
public static class ConexaoBancoArquivo
{
    public static AppDbContext AbrirLeitura(string caminho)
        => new(Opcoes(caminho, SqliteOpenMode.ReadOnly));

    public static AppDbContext AbrirEscrita(string caminho)
        => new(Opcoes(caminho, SqliteOpenMode.ReadWriteCreate));

    private static DbContextOptions<AppDbContext> Opcoes(string caminho, SqliteOpenMode modo)
    {
        var texto = new SqliteConnectionStringBuilder
        {
            DataSource = caminho,
            Mode = modo,
            Pooling = false
        }.ConnectionString;

        return new DbContextOptionsBuilder<AppDbContext>().UseSqlite(texto).Options;
    }
}
