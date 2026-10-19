using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Data;

/// <summary>
/// Popula categorias padrão, o perfil único e dados de demonstração (fictícios) no primeiro uso do app.
/// Os valores aqui NÃO representam dados financeiros reais de nenhum usuário.
/// As linhas saem de <see cref="CatalogoDemonstracao"/>, o mesmo catálogo que distingue
/// demonstração de dado do usuário na hora de substituir um espelho.
/// </summary>
public static class SeedData
{
    public static async Task PopularAsync(AppDbContext ctx)
    {
        var perfil = await PopularEssencialAsync(ctx);
        await PopularDemonstracaoAsync(ctx, perfil);
    }

    /// <summary>Mínimo para o app funcionar: categorias padrão e o perfil do usuário.</summary>
    public static async Task<MembroFamilia> PopularEssencialAsync(AppDbContext ctx)
    {
        var nomes = ctx.Categorias.Select(categoria => categoria.Nome).ToList();
        var novas = CatalogoDemonstracao.CriarCategorias()
            .Where(categoria => !nomes.Any(nome => string.Equals(nome, categoria.Nome, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (novas.Count > 0)
            ctx.Categorias.AddRange(novas);

        var perfil = CatalogoDemonstracao.CriarPerfil();
        ctx.MembrosFamilia.Add(perfil);

        await ctx.SaveChangesAsync();
        return perfil;
    }

    public static async Task PopularDemonstracaoAsync(AppDbContext ctx, MembroFamilia perfil)
    {
        // Today só escolhe o dia em que a semente é gravada. O backup não consulta o relógio de novo.
        CatalogoDemonstracao.AdicionarDemonstracao(ctx, perfil, DateTime.Today);
        await ctx.SaveChangesAsync();
    }
}
