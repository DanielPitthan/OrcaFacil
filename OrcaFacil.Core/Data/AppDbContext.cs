using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<MembroFamilia> MembrosFamilia => Set<MembroFamilia>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Transacao> Transacoes => Set<Transacao>();
    public DbSet<Orcamento> Orcamentos => Set<Orcamento>();
    public DbSet<Meta> Metas => Set<Meta>();
    public DbSet<FechamentoPeriodo> FechamentosPeriodo => Set<FechamentoPeriodo>();
    public DbSet<LogEvento> LogsEvento => Set<LogEvento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Categoria>(entity =>
        {
            entity.Property(c => c.Nome).UseCollation("NOCASE");
            entity.HasIndex(c => c.Nome).IsUnique();
        });

        modelBuilder.Entity<Orcamento>(entity =>
        {
            entity.HasIndex(o => new { o.CategoriaId, o.Mes, o.Ano }).IsUnique();
            entity.HasOne(o => o.Categoria)
                  .WithMany(c => c.Orcamentos)
                  .HasForeignKey(o => o.CategoriaId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Transacao>(entity =>
        {
            entity.HasOne(t => t.Categoria)
                  .WithMany(c => c.Transacoes)
                  .HasForeignKey(t => t.CategoriaId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.MembroFamilia)
                  .WithMany(m => m.Transacoes)
                  .HasForeignKey(t => t.MembroFamiliaId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.TransacaoOrigem)
                  .WithMany()
                  .HasForeignKey(t => t.TransacaoOrigemId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Meta>(entity =>
        {
            entity.HasOne(m => m.MembroFamilia)
                  .WithMany(mf => mf.Metas)
                  .HasForeignKey(m => m.MembroFamiliaId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.Ignore(m => m.PercentualConcluido);
        });

        modelBuilder.Entity<FechamentoPeriodo>(entity =>
        {
            entity.HasIndex(f => new { f.Mes, f.Ano }).IsUnique();
        });

        modelBuilder.Entity<LogEvento>(entity =>
        {
            entity.HasIndex(l => l.CriadoEm);
        });
    }
}
