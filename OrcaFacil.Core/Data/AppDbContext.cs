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
    public DbSet<ItemLancamento> ItensLancamento => Set<ItemLancamento>();
    public DbSet<Orcamento> Orcamentos => Set<Orcamento>();
    public DbSet<Meta> Metas => Set<Meta>();
    public DbSet<FechamentoPeriodo> FechamentosPeriodo => Set<FechamentoPeriodo>();
    public DbSet<LogEvento> LogsEvento => Set<LogEvento>();
    public DbSet<AtivoFinanceiro> AtivosFinanceiros => Set<AtivoFinanceiro>();
    public DbSet<OperacaoAtivo> OperacoesAtivo => Set<OperacaoAtivo>();
    public DbSet<ProdutoCesta> ProdutosCesta => Set<ProdutoCesta>();
    public DbSet<LocalCompra> LocaisCompra => Set<LocalCompra>();
    public DbSet<CompraCesta> ComprasCesta => Set<CompraCesta>();
    public DbSet<CompraCestaItem> ItensCompraCesta => Set<CompraCestaItem>();
    public DbSet<Cartao> Cartoes => Set<Cartao>();
    public DbSet<RecargaCartao> RecargasCartao => Set<RecargaCartao>();
    public DbSet<TituloRendaFixa> TitulosRendaFixa => Set<TituloRendaFixa>();
    public DbSet<OperacaoRendaFixa> OperacoesRendaFixa => Set<OperacaoRendaFixa>();
    public DbSet<RegraCatalogoComprovante> RegrasCatalogoComprovante => Set<RegraCatalogoComprovante>();

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

            entity.HasOne(t => t.Cartao)
                  .WithMany(c => c.Transacoes)
                  .HasForeignKey(t => t.CartaoId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.OperacaoRendaFixa)
                  .WithMany()
                  .HasForeignKey(t => t.OperacaoRendaFixaId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(t => t.OperacaoRendaFixaId)
                  .IsUnique()
                  .HasDatabaseName("IX_Transacoes_OperacaoRendaFixaId");

            entity.HasMany(t => t.Itens)
                  .WithOne(i => i.Transacao)
                  .HasForeignKey(i => i.TransacaoId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ItemLancamento>(entity =>
        {
            entity.HasOne(i => i.Categoria)
                  .WithMany()
                  .HasForeignKey(i => i.CategoriaId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Vários itens sem hash (lançados na mão) continuam válidos: o único ignora nulos.
            entity.HasIndex(i => new { i.TransacaoId, i.HashImportacao })
                  .IsUnique()
                  .HasFilter("\"HashImportacao\" IS NOT NULL")
                  .HasDatabaseName("IX_ItensLancamento_TransacaoId_HashImportacao");
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

        modelBuilder.Entity<AtivoFinanceiro>(entity =>
        {
            entity.Property(a => a.Ticker).UseCollation("NOCASE");
            entity.HasIndex(a => a.Ticker).IsUnique();
        });

        modelBuilder.Entity<OperacaoAtivo>(entity =>
        {
            entity.HasIndex(o => new { o.AtivoId, o.MembroFamiliaId, o.Data });

            entity.HasOne(o => o.Ativo)
                  .WithMany(a => a.Operacoes)
                  .HasForeignKey(o => o.AtivoId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(o => o.MembroFamilia)
                  .WithMany()
                  .HasForeignKey(o => o.MembroFamiliaId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProdutoCesta>(entity =>
        {
            entity.Property(p => p.Nome).UseCollation("NOCASE");
            entity.HasIndex(p => p.Nome).IsUnique();
            entity.HasOne(p => p.Categoria)
                  .WithMany()
                  .HasForeignKey(p => p.CategoriaId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LocalCompra>(entity =>
        {
            entity.Property(l => l.Nome).UseCollation("NOCASE");
            entity.HasIndex(l => l.Nome).IsUnique();
        });

        modelBuilder.Entity<Cartao>(entity =>
        {
            entity.Property(c => c.Nome).UseCollation("NOCASE");
            entity.HasIndex(c => c.Nome).IsUnique();
        });

        modelBuilder.Entity<RecargaCartao>(entity =>
        {
            entity.HasIndex(r => new { r.CartaoId, r.Data });
            entity.HasOne(r => r.Cartao)
                  .WithMany(c => c.Recargas)
                  .HasForeignKey(r => r.CartaoId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CompraCesta>(entity =>
        {
            entity.HasIndex(c => new { c.Ano, c.Mes, c.LocalCompraId });

            entity.HasOne(c => c.LocalCompra)
                  .WithMany(l => l.Compras)
                  .HasForeignKey(c => c.LocalCompraId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.CartaoBeneficio)
                  .WithMany()
                  .HasForeignKey(c => c.CartaoBeneficioId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.CartaoCredito)
                  .WithMany()
                  .HasForeignKey(c => c.CartaoCreditoId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.ItemFatura)
                  .WithMany()
                  .HasForeignKey(c => c.ItemFaturaId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(c => c.Transacao)
                  .WithMany()
                  .HasForeignKey(c => c.TransacaoId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(c => c.Itens)
                  .WithOne(i => i.CompraCesta)
                  .HasForeignKey(i => i.CompraCestaId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CompraCestaItem>(entity =>
        {
            entity.HasIndex(i => new { i.CompraCestaId, i.ProdutoCestaId }).IsUnique();
            entity.HasOne(i => i.Produto)
                  .WithMany(p => p.Itens)
                  .HasForeignKey(i => i.ProdutoCestaId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TituloRendaFixa>(entity =>
        {
            entity.Property(t => t.Codigo).UseCollation("NOCASE");
            entity.Property(t => t.Corretora).UseCollation("NOCASE");
            entity.HasIndex(t => new { t.Codigo, t.Corretora })
                  .IsUnique()
                  .HasDatabaseName("IX_TitulosRendaFixa_Codigo_Corretora");
        });

        modelBuilder.Entity<OperacaoRendaFixa>(entity =>
        {
            // SQLite guarda decimal como TEXT. A precisão fica no modelo para o cálculo.
            entity.Property(o => o.Quantidade).HasPrecision(18, 8);
            entity.Property(o => o.PrecoUnitario).HasPrecision(18, 8);
            entity.Property(o => o.Valor).HasPrecision(18, 2);
            entity.Property(o => o.Custos).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(o => o.IrRetido).HasPrecision(18, 2).HasDefaultValue(0m);

            entity.HasOne(o => o.Titulo)
                  .WithMany(t => t.Operacoes)
                  .HasForeignKey(o => o.TituloRendaFixaId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Várias operações manuais, sem hash, continuam válidas: o único ignora nulos.
            entity.HasIndex(o => o.HashImportacao)
                  .IsUnique()
                  .HasFilter("\"HashImportacao\" IS NOT NULL")
                  .HasDatabaseName("IX_OperacoesRendaFixa_HashImportacao");
        });

        modelBuilder.Entity<RegraCatalogoComprovante>(entity =>
        {
            entity.HasData(new RegraCatalogoComprovante
            {
                Id = 1,
                LimiteProdutos = RegraCatalogoComprovante.LimitePadrao
            });
        });
    }
}
