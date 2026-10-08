using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrcaFacil.Core.Data;

/// <summary>
/// Usada apenas em tempo de design pelo `dotnet ef` (migrations) — em runtime o app usa
/// IDbContextFactory&lt;AppDbContext&gt; configurado em MauiProgram.cs com o caminho real do SQLite.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=design_time.db")
            .Options;

        return new AppDbContext(options);
    }
}
