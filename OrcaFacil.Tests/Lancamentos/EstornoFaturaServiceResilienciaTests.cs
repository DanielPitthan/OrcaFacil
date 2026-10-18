using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Services;

namespace OrcaFacil.Tests.Lancamentos;

public class EstornoFaturaServiceResilienciaTests
{
    [Fact]
    public async Task LancarAsync_FalhaNoMeioDoSaveChanges_DesfazTotalEItem()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var opcoes = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var falha = new FalhaNoMeioDoSave();
        var opcoesComFalha = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(falha)
            .Options;

        int faturaId;
        int categoriaId;
        await using (var contexto = new AppDbContext(opcoes))
        {
            contexto.Database.EnsureCreated();
            var cartao = new Categoria { Nome = "Cartão de Crédito", Icone = "💳", CorHex = "#B2CFE6", PermiteItensFatura = true };
            var alimentacao = new Categoria { Nome = "Alimentação", Icone = "🛒", CorHex = "#B2CFE6" };
            var membro = new MembroFamilia { Nome = "Usuário" };
            contexto.AddRange(cartao, alimentacao, membro);
            await contexto.SaveChangesAsync();
            var fatura = new Transacao
            {
                Descricao = "Fatura Nubank",
                Valor = 200m,
                Data = new DateTime(2026, 10, 18),
                Tipo = TipoTransacao.Despesa,
                CategoriaId = cartao.Id,
                MembroFamiliaId = membro.Id
            };
            contexto.Transacoes.Add(fatura);
            await contexto.SaveChangesAsync();
            faturaId = fatura.Id;
            categoriaId = alimentacao.Id;
        }

        var service = new EstornoFaturaService(new Fabrica(opcoesComFalha));
        var ex = await Assert.ThrowsAsync<IOException>(() =>
            service.LancarAsync(faturaId, 80m, "Estorno", categoriaId));

        Assert.Contains("meio do SaveChanges", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, falha.Gravacoes);

        await using var conferido = new AppDbContext(opcoes);
        var gravada = await conferido.Transacoes.Include(t => t.Itens).SingleAsync();
        Assert.Equal(200m, gravada.Valor);
        Assert.Empty(gravada.Itens);
    }

    private sealed class FalhaNoMeioDoSave : SaveChangesInterceptor
    {
        public int Gravacoes { get; private set; }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            Gravacoes++;
            throw new IOException("falha no meio do SaveChanges");
        }
    }

    private sealed class Fabrica(DbContextOptions<AppDbContext> opcoes) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(opcoes);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
