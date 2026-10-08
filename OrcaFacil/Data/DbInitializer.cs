using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;

namespace OrcaFacil.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IDbContextFactory<AppDbContext> factory)
    {
        await using var context = await factory.CreateDbContextAsync();
        await context.Database.MigrateAsync();

        if (!await context.MembrosFamilia.AnyAsync())
        {
            await SeedData.PopularAsync(context);
        }
    }
}
