using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OrcaFacil.Core.Cesta;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Llm;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Core.Services;

namespace OrcaFacil.Tests.Cesta;

public class ProdutoNovoNaCompraTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly CestaRepository _cestas;
    private readonly CestaCadastroService _service;
    private readonly int _membroId;
    private readonly Categoria _alimentacao;

    public ProdutoNovoNaCompraTests()
    {
        _cestas = new CestaRepository(_factory);
        _service = new CestaCadastroService(_cestas);
        var membros = new MembroFamiliaRepository(_factory);
        var membro = new MembroFamilia { Nome = "Usuário" };
        membros.AddAsync(membro).GetAwaiter().GetResult();
        _membroId = membro.Id;

        using var contexto = _factory.CreateDbContext();
        _alimentacao = new Categoria { Nome = "Alimentação", Icone = "🛒", CorHex = "#B2CFE6" };
        contexto.Categorias.Add(_alimentacao);
        contexto.SaveChanges();
    }

    [Fact]
    public async Task MesAberto_GravaProdutoECompra_ELigaALinha()
    {
        var local = await _service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        var compra = await _service.SalvarCompraAsync(Modelo(local.Id, "biscoito", "Biscoito", 4.5m), _membroId);

        await using var contexto = _factory.CreateDbContext();
        var produto = Assert.Single(await contexto.ProdutosCesta.ToListAsync());
        var item = Assert.Single(compra.Itens);
        Assert.Equal("Biscoito", produto.Nome);
        Assert.Equal(_alimentacao.Id, produto.CategoriaId);
        Assert.Equal(produto.Id, item.ProdutoCestaId);
        Assert.Equal(4.5m, item.Preco);
        Assert.Equal(compra.Id, Assert.Single(await contexto.ComprasCesta.ToListAsync()).Id);
    }

    [Fact]
    public async Task MesFechado_NaoDeixaProduto()
    {
        var local = await _service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        await using (var contexto = _factory.CreateDbContext())
        {
            contexto.FechamentosPeriodo.Add(new FechamentoPeriodo { Mes = 10, Ano = 2026, Fechado = true });
            await contexto.SaveChangesAsync();
        }

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SalvarCompraAsync(Modelo(local.Id, "biscoito", "Biscoito", 4.5m), _membroId));

        Assert.Contains("fechado", erro.Message, StringComparison.Ordinal);
        await using var depois = _factory.CreateDbContext();
        Assert.Empty(await depois.ProdutosCesta.ToListAsync());
        Assert.Empty(await depois.ComprasCesta.ToListAsync());
    }

    [Fact]
    public async Task FalhaNoMeioDaCompra_DesfazOProduto()
    {
        var compra = new CompraCesta
        {
            LocalCompraId = 987654,
            Data = new DateTime(2026, 10, 18),
            Mes = 10,
            Ano = 2026,
            Itens = [new CompraCestaItem { Preco = 4.5m, Ordem = 0 }]
        };

        await Assert.ThrowsAnyAsync<Exception>(() => _cestas.SalvarCompraAsync(
            compra,
            null,
            null,
            [new ProdutoNovoCompra
            {
                Chave = "biscoito",
                Nome = "Biscoito",
                CategoriaId = _alimentacao.Id,
                IndiceItem = 0
            }]));

        await using var contexto = _factory.CreateDbContext();
        Assert.Empty(await contexto.ProdutosCesta.ToListAsync());
        Assert.Empty(await contexto.ComprasCesta.ToListAsync());
    }

    [Fact]
    public async Task CancelarPrevia_NaoDeixaProdutoNoBanco()
    {
        var previa = PreviaComprovante.Criar(
            NormalizadorComprovante.Normalizar(
                new ComprovanteLido(null, null, null, true, [new ItemComprovanteLido("BISC", "", false, 4m, null, null, null, null)]),
                []),
            [],
            [],
            new DateTime(2026, 10, 1));
        var formulario = previa.IniciarCadastro(Assert.Single(previa.Linhas));
        Assert.NotNull(formulario);
        formulario.Nome = "Biscoito";
        formulario.CategoriaId = _alimentacao.Id;
        previa.ConfirmarCadastro(formulario, 4m, "Alimentação");
        Assert.True(Assert.Single(previa.Linhas).NovoSeraCriado);

        previa.CancelarCadastro();

        await using var contexto = _factory.CreateDbContext();
        Assert.Empty(await contexto.ProdutosCesta.ToListAsync());
        Assert.Empty(await contexto.ComprasCesta.ToListAsync());
    }

    [Fact]
    public async Task CadastrarTudo_MesFechado_NaoDeixaProduto()
    {
        var local = await _service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        var previa = PreviaComprovante.Criar(
            NormalizadorComprovante.Normalizar(
                new ComprovanteLido(
                    local.Nome,
                    new DateOnly(2026, 10, 18),
                    null,
                    true,
                    [
                        new ItemComprovanteLido("Biscoito", "Doce", true, 4m, null, null, null, null),
                        new ItemComprovanteLido("Sabonete", "Outro", true, 3m, null, null, null, null)
                    ]),
                []),
            [local],
            [],
            new DateTime(2026, 10, 1));
        Assert.True(previa.PodeCadastrarTudo);
        previa.CadastrarTudo(_alimentacao.Id, _alimentacao.Nome);
        Assert.False(previa.PodeCadastrarTudo);
        Assert.True(previa.PodeConfirmar);

        await using (var contexto = _factory.CreateDbContext())
        {
            contexto.FechamentosPeriodo.Add(new FechamentoPeriodo { Mes = 10, Ano = 2026, Fechado = true });
            await contexto.SaveChangesAsync();
        }

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SalvarCompraAsync(previa.ParaCompra(), _membroId));

        Assert.Contains("fechado", erro.Message, StringComparison.Ordinal);
        await using var depois = _factory.CreateDbContext();
        Assert.Empty(await depois.ProdutosCesta.ToListAsync());
        Assert.Empty(await depois.ComprasCesta.ToListAsync());
    }

    [Fact]
    public async Task NomeNormalizadoRepetido_NaoCriaDuplicata()
    {
        await _service.SalvarProdutoAsync(new ProdutoCestaFormModel { Nome = "Açúcar", CategoriaId = _alimentacao.Id });
        var local = await _service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        var compra = await _service.SalvarCompraAsync(Modelo(local.Id, "acucar", "acucar", 3m), _membroId);

        await using var contexto = _factory.CreateDbContext();
        var produto = Assert.Single(await contexto.ProdutosCesta.ToListAsync());
        Assert.Equal("Açúcar", produto.Nome);
        Assert.Equal(produto.Id, Assert.Single(compra.Itens).ProdutoCestaId);
    }

    [Fact]
    public async Task DoisItensComOMesmoNomeNormalizado_GravaUmProdutoComTotalExato()
    {
        var local = await _service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        var modelo = Modelo(local.Id, "acucar", "Açúcar", 2.10m);
        modelo.Itens.Add(new ItemCompraCestaFormModel { ProdutoNovoChave = "acucar", NomePendente = "Açúcar", Preco = 3.25m });

        var compra = await _service.SalvarCompraAsync(modelo, _membroId);

        await using var contexto = _factory.CreateDbContext();
        var produto = Assert.Single(await contexto.ProdutosCesta.ToListAsync());
        var item = Assert.Single(compra.Itens);
        Assert.Equal("Açúcar", produto.Nome);
        Assert.Equal(produto.Id, item.ProdutoCestaId);
        Assert.Equal(5.35m, item.Preco);
        Assert.Equal(5.35m, compra.Itens.Sum(i => i.Preco));
        Assert.Equal(5.35m, compra.Transacao!.Valor);
        Assert.Equal(compra.Id, Assert.Single(await contexto.ComprasCesta.ToListAsync()).Id);
    }

    [Fact]
    public async Task Previa_CopiaDoDialogo_SalvarCompra_GravaProdutoECompra()
    {
        var local = await _service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        var previa = PreviaDuasLinhas(local, "açúcar", "Doce", 2.10m, "2", "acucar", "Outro", 3.25m, "1");
        var primeira = previa.IniciarCadastro(previa.Linhas[0]);
        Assert.NotNull(primeira);
        primeira.Nome = "Açúcar";
        primeira.CategoriaId = _alimentacao.Id;
        previa.ConfirmarCadastro(primeira, previa.Linhas[0].Preco, _alimentacao.Nome);
        var segunda = previa.IniciarCadastro(previa.Linhas[1]);
        Assert.NotNull(segunda);
        segunda.Nome = "acucar";
        segunda.CategoriaId = _alimentacao.Id;
        previa.ConfirmarCadastro(segunda, previa.Linhas[1].Preco, _alimentacao.Nome);

        Assert.Equal(2, previa.Linhas.Count);
        Assert.Equal("2", previa.Linhas[0].Quantidade);
        Assert.Equal("1", previa.Linhas[1].Quantidade);
        Assert.True(previa.PodeConfirmar);

        var vindaDaPrevia = previa.ParaCompra();
        var copiada = new CompraCestaFormModel
        {
            LocalCompraId = vindaDaPrevia.LocalCompraId,
            Data = vindaDaPrevia.Data,
            ProdutosNovos = vindaDaPrevia.ProdutosNovos,
            Itens = CompraCestaItens.CopiarItens(vindaDaPrevia)
        };
        var produtos = CompraCestaItens.FiltrarProdutos([], copiada);
        Assert.Equal(2, copiada.Itens.Count);
        Assert.All(copiada.Itens, item =>
        {
            Assert.False(string.IsNullOrEmpty(item.ProdutoNovoChave));
            Assert.Equal("Açúcar", CompraCestaItens.Titulo(item, produtos));
            Assert.NotEqual("Produto", CompraCestaItens.Titulo(item, produtos));
        });
        Assert.Contains(produtos, produto => produto.Nome == "Açúcar");
        Assert.DoesNotContain(produtos, produto => produto.Id > 0);

        var compra = await _service.SalvarCompraAsync(copiada, _membroId);

        await using var contexto = _factory.CreateDbContext();
        var produto = Assert.Single(await contexto.ProdutosCesta.ToListAsync());
        var item = Assert.Single(compra.Itens);
        Assert.Equal("Açúcar", produto.Nome);
        Assert.Equal(produto.Id, item.ProdutoCestaId);
        Assert.Equal(5.35m, item.Preco);
        Assert.Equal(5.35m, compra.Transacao!.Valor);
        Assert.Equal(2, previa.Linhas.Count);
    }

    [Fact]
    public async Task CadastrarTudo_MesmoProdutoNovo_GravaUmETotalExato()
    {
        var local = await _service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        var previa = PreviaDuasLinhas(local, "Açúcar", "Doce", 2.10m, "2", "açúcar", "Outro", 3.25m, "1");
        Assert.True(previa.PodeCadastrarTudo);
        previa.CadastrarTudo(_alimentacao.Id, _alimentacao.Nome);

        Assert.Equal(2, previa.Linhas.Count);
        Assert.Equal("2", previa.Linhas[0].Quantidade);
        Assert.Equal("1", previa.Linhas[1].Quantidade);
        Assert.Equal(2.10m, previa.Linhas[0].Preco);
        Assert.Equal(3.25m, previa.Linhas[1].Preco);
        Assert.True(previa.Linhas[0].NovoSeraCriado);
        Assert.True(previa.Linhas[1].NovoSeraCriado);
        Assert.Equal(previa.Linhas[0].ProdutoNovoChave, previa.Linhas[1].ProdutoNovoChave);
        Assert.True(previa.PodeConfirmar);

        var vindaDaPrevia = previa.ParaCompra();
        var copiada = new CompraCestaFormModel
        {
            LocalCompraId = vindaDaPrevia.LocalCompraId,
            Data = vindaDaPrevia.Data,
            ProdutosNovos = vindaDaPrevia.ProdutosNovos,
            Itens = CompraCestaItens.CopiarItens(vindaDaPrevia)
        };
        var compra = await _service.SalvarCompraAsync(copiada, _membroId);

        await using var contexto = _factory.CreateDbContext();
        var produto = Assert.Single(await contexto.ProdutosCesta.ToListAsync());
        Assert.Equal("Açúcar", produto.Nome);
        Assert.Equal(5.35m, Assert.Single(compra.Itens).Preco);
        Assert.Equal(previa.Linhas.Sum(linha => linha.Preco), compra.Itens.Sum(item => item.Preco));
        Assert.Equal(previa.Linhas.Sum(linha => linha.Preco), compra.Transacao!.Valor);
        Assert.All(previa.Linhas, linha => Assert.Contains(PreviaComprovante.EtiquetaSeraSomado, linha.Titulo, StringComparison.Ordinal));
        Assert.Equal(2, previa.Linhas.Count);
    }

    [Fact]
    public async Task CadastrarTudo_NoCatalogo_SomaExataENaoCriaOutroProduto()
    {
        var acucar = await _service.SalvarProdutoAsync(new ProdutoCestaFormModel
        {
            Nome = "Açúcar",
            CategoriaId = _alimentacao.Id
        });
        acucar.Categoria = _alimentacao;
        var local = await _service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        var previa = PreviaNoCatalogo(
            local,
            acucar,
            ("Não identificado: Açúcar", "Doce", 2.10m, "2"),
            ("Não identificado: acucar", "Outro", 3.25m, "1"));

        Assert.True(previa.PodeCadastrarTudo);
        previa.CadastrarTudo(_alimentacao.Id, _alimentacao.Nome);

        Assert.Equal(2, previa.Linhas.Count);
        Assert.True(previa.PodeConfirmar);
        Assert.All(previa.Linhas, linha =>
        {
            Assert.Equal(acucar.Id, linha.ProdutoCestaId);
            Assert.False(linha.NovoSeraCriado);
            Assert.Contains(PreviaComprovante.EtiquetaSeraSomado, linha.Titulo, StringComparison.Ordinal);
        });
        Assert.Equal("2", previa.Linhas[0].Quantidade);
        Assert.Equal("1", previa.Linhas[1].Quantidade);

        var soma = previa.Linhas.Sum(linha => linha.Preco);
        previa.ConfirmarCasamentos();
        var compra = await _service.SalvarCompraAsync(Copiar(previa.ParaCompra()), _membroId);

        await using var contexto = _factory.CreateDbContext();
        Assert.Equal("Açúcar", Assert.Single(await contexto.ProdutosCesta.ToListAsync()).Nome);
        Assert.Equal(soma, Assert.Single(compra.Itens).Preco);
        Assert.Equal(acucar.Id, compra.Itens.Single().ProdutoCestaId);
        Assert.Equal(soma, compra.Transacao!.Valor);
    }

    [Fact]
    public async Task CasamentoNoCatalogo_SomaExata()
    {
        var acucar = await _service.SalvarProdutoAsync(new ProdutoCestaFormModel
        {
            Nome = "Açúcar",
            CategoriaId = _alimentacao.Id
        });
        acucar.Categoria = _alimentacao;
        var local = await _service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        var previa = PreviaNoCatalogo(
            local,
            acucar,
            ("pacote doce", "Doce", 1.10m, "1"),
            ("outro pacote", "Outro", 2.25m, "3"));
        var primeiro = previa.IniciarCadastro(previa.Linhas[0]);
        Assert.NotNull(primeiro);
        primeiro.Nome = "acucar";
        primeiro.CategoriaId = 9;
        previa.ConfirmarCadastro(primeiro, previa.Linhas[0].Preco, "Outra");
        var segundo = previa.IniciarCadastro(previa.Linhas[1]);
        Assert.NotNull(segundo);
        segundo.Nome = "Açúcar";
        segundo.CategoriaId = 9;
        previa.ConfirmarCadastro(segundo, previa.Linhas[1].Preco, "Outra");

        Assert.Equal(2, previa.Linhas.Count);
        Assert.True(previa.PodeConfirmar);
        Assert.All(previa.Linhas, linha =>
        {
            Assert.Equal(acucar.Id, linha.ProdutoCestaId);
            Assert.Equal("Açúcar", linha.NomeGravado);
            Assert.Contains(PreviaComprovante.EtiquetaSeraSomado, linha.Titulo, StringComparison.Ordinal);
        });

        var soma = previa.Linhas.Sum(linha => linha.Preco);
        previa.ConfirmarCasamentos();
        var compra = await _service.SalvarCompraAsync(Copiar(previa.ParaCompra()), _membroId);

        await using var contexto = _factory.CreateDbContext();
        Assert.Equal("Açúcar", Assert.Single(await contexto.ProdutosCesta.ToListAsync()).Nome);
        Assert.Equal(soma, Assert.Single(compra.Itens).Preco);
        Assert.Equal(soma, compra.Transacao!.Valor);
        Assert.Equal(3.35m, soma);
    }

    [Fact]
    public void NomeDeProdutoRepetido_SoReconheceOIndiceDoNome()
    {
        var nome = new DbUpdateException("falha", new Exception("UNIQUE constraint failed: ProdutosCesta.Nome"));
        var outro = new DbUpdateException("falha", new Exception("UNIQUE constraint failed: ItensCompraCesta.CompraCestaId"));

        Assert.True(CestaCadastroService.NomeDeProdutoRepetido(nome));
        Assert.False(CestaCadastroService.NomeDeProdutoRepetido(outro));
    }

    [Fact]
    public async Task DbUpdateException_ForaDoNome_MensagemGenericaENaoGrava()
    {
        using var fabrica = new FabricaComFalhaAoGravar("Biscoito");
        var cestas = new CestaRepository(fabrica);
        var service = new CestaCadastroService(cestas);
        var membros = new MembroFamiliaRepository(fabrica);
        var membro = new MembroFamilia { Nome = "Usuário" };
        await membros.AddAsync(membro);
        int alimentacaoId;
        await using (var contexto = fabrica.CreateDbContext())
        {
            var alimentacao = new Categoria { Nome = "Alimentação", Icone = "🛒", CorHex = "#B2CFE6" };
            contexto.Categorias.Add(alimentacao);
            await contexto.SaveChangesAsync();
            alimentacaoId = alimentacao.Id;
        }

        var local = await service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        await service.SalvarCompraAsync(ModeloNovo(local.Id, alimentacaoId, "arroz", "Arroz", 4m), membro.Id);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SalvarCompraAsync(ModeloNovo(local.Id, alimentacaoId, "biscoito", "Biscoito", 3m), membro.Id));

        Assert.Equal(CestaCadastroService.MensagemFalhaAoGravar, erro.Message);
        Assert.DoesNotContain("UNIQUE", erro.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Sqlite", erro.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ProdutosCesta", erro.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(CestaCadastroService.MensagemNomeProdutoRepetido, erro.Message, StringComparison.Ordinal);

        await using var depois = fabrica.CreateDbContext();
        Assert.Equal("Arroz", Assert.Single(await depois.ProdutosCesta.ToListAsync()).Nome);
        Assert.Single(await depois.ComprasCesta.ToListAsync());
    }

    [Fact]
    public async Task SegundoConfirmar_NomeRepetido_MensagemEmPortugues_SemDeixarProduto()
    {
        using var fabrica = new FabricaComColisaoNoNome("Biscoito");
        var cestas = new CestaRepository(fabrica);
        var service = new CestaCadastroService(cestas);
        var membros = new MembroFamiliaRepository(fabrica);
        var membro = new MembroFamilia { Nome = "Usuário" };
        await membros.AddAsync(membro);
        int alimentacaoId;
        await using (var contexto = fabrica.CreateDbContext())
        {
            var alimentacao = new Categoria { Nome = "Alimentação", Icone = "🛒", CorHex = "#B2CFE6" };
            contexto.Categorias.Add(alimentacao);
            await contexto.SaveChangesAsync();
            alimentacaoId = alimentacao.Id;
        }

        var local = await service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        await service.SalvarCompraAsync(ModeloNovo(local.Id, alimentacaoId, "arroz", "Arroz", 4m), membro.Id);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SalvarCompraAsync(ModeloNovo(local.Id, alimentacaoId, "biscoito", "Biscoito", 3m), membro.Id));

        Assert.Equal(CestaCadastroService.MensagemNomeProdutoRepetido, erro.Message);
        Assert.DoesNotContain("UNIQUE", erro.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Sqlite", erro.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("at ", erro.Message, StringComparison.Ordinal);

        await using var depois = fabrica.CreateDbContext();
        var produtos = await depois.ProdutosCesta.ToListAsync();
        Assert.Equal("Arroz", Assert.Single(produtos).Nome);
        Assert.Single(await depois.ComprasCesta.ToListAsync());
    }

    public void Dispose() => _factory.Dispose();

    private static CompraCestaFormModel Copiar(CompraCestaFormModel modelo) => new()
    {
        LocalCompraId = modelo.LocalCompraId,
        Data = modelo.Data,
        ProdutosNovos = modelo.ProdutosNovos,
        Itens = CompraCestaItens.CopiarItens(modelo)
    };

    private static PreviaComprovante PreviaNoCatalogo(
        LocalCompra local,
        ProdutoCesta produto,
        params (string Descricao, string NomeLido, decimal Preco, string Quantidade)[] linhas) =>
        PreviaComprovante.Criar(
            NormalizadorComprovante.Normalizar(
                new ComprovanteLido(
                    local.Nome,
                    new DateOnly(2026, 10, 18),
                    null,
                    true,
                    linhas.Select(linha => new ItemComprovanteLido(
                        linha.Descricao, linha.NomeLido, true, linha.Preco, linha.Quantidade, "un", null, null)).ToList()),
                [produto]),
            [local],
            [produto],
            new DateTime(2026, 10, 1));

    private static PreviaComprovante PreviaDuasLinhas(
        LocalCompra local,
        string descricao1,
        string produto1,
        decimal preco1,
        string quantidade1,
        string descricao2,
        string produto2,
        decimal preco2,
        string quantidade2) =>
        PreviaComprovante.Criar(
            NormalizadorComprovante.Normalizar(
                new ComprovanteLido(
                    local.Nome,
                    new DateOnly(2026, 10, 18),
                    null,
                    true,
                    [
                        new ItemComprovanteLido(descricao1, produto1, true, preco1, quantidade1, "un", null, null),
                        new ItemComprovanteLido(descricao2, produto2, true, preco2, quantidade2, "un", null, null)
                    ]),
                []),
            [local],
            [],
            new DateTime(2026, 10, 1));

    private CompraCestaFormModel Modelo(int localId, string chave, string nome, decimal preco) =>
        ModeloNovo(localId, _alimentacao.Id, chave, nome, preco);

    private static CompraCestaFormModel ModeloNovo(int localId, int categoriaId, string chave, string nome, decimal preco) => new()
    {
        LocalCompraId = localId,
        Data = new DateTime(2026, 10, 18),
        ProdutosNovos =
        [
            new ProdutoNovoCompra
            {
                Chave = chave,
                Nome = nome,
                CategoriaId = categoriaId
            }
        ],
        Itens = [new ItemCompraCestaFormModel { ProdutoNovoChave = chave, Preco = preco }]
    };
}

/// <summary>
/// No instante em que o produto novo vai ao banco, outra gravação já inseriu o mesmo nome.
/// </summary>
file sealed class FabricaComColisaoNoNome : IDbContextFactory<AppDbContext>, IDisposable
{
    private readonly SqliteConnection _conexao;
    private readonly DbContextOptions<AppDbContext> _opcoes;

    public FabricaComColisaoNoNome(string nomeQueColide)
    {
        _conexao = new SqliteConnection("DataSource=:memory:");
        _conexao.Open();
        _opcoes = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_conexao)
            .AddInterceptors(new ColisaoNoNome(nomeQueColide))
            .Options;

        using var contexto = new AppDbContext(_opcoes);
        contexto.Database.EnsureCreated();
    }

    public AppDbContext CreateDbContext() => new(_opcoes);

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());

    public void Dispose() => _conexao.Dispose();

    private sealed class ColisaoNoNome : SaveChangesInterceptor
    {
        private readonly string _nome;
        private bool _inseriu;

        public ColisaoNoNome(string nome) => _nome = nome;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var contexto = eventData.Context;
            var entrada = contexto?.ChangeTracker.Entries<ProdutoCesta>()
                .FirstOrDefault(item => item.State == EntityState.Added && item.Entity.Nome == _nome);
            if (contexto is not null && entrada is not null && !_inseriu)
            {
                _inseriu = true;
                contexto.Database.ExecuteSqlInterpolated(
                    $"INSERT INTO ProdutosCesta (Nome, CategoriaId, Ativo, CriadoEm) VALUES ({entrada.Entity.Nome}, {entrada.Entity.CategoriaId}, {true}, {DateTime.UtcNow})");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}

/// <summary>Uma falha de banco que não é o índice do nome, no meio da transação.</summary>
file sealed class FabricaComFalhaAoGravar : IDbContextFactory<AppDbContext>, IDisposable
{
    private readonly SqliteConnection _conexao;
    private readonly DbContextOptions<AppDbContext> _opcoes;

    public FabricaComFalhaAoGravar(string nome)
    {
        _conexao = new SqliteConnection("DataSource=:memory:");
        _conexao.Open();
        _opcoes = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_conexao)
            .AddInterceptors(new Falha(nome))
            .Options;

        using var contexto = new AppDbContext(_opcoes);
        contexto.Database.EnsureCreated();
    }

    public AppDbContext CreateDbContext() => new(_opcoes);

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());

    public void Dispose() => _conexao.Dispose();

    private sealed class Falha : SaveChangesInterceptor
    {
        private readonly string _nome;

        public Falha(string nome) => _nome = nome;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var contexto = eventData.Context;
            var entrada = contexto?.ChangeTracker.Entries<ProdutoCesta>()
                .FirstOrDefault(item => item.State == EntityState.Added && item.Entity.Nome == _nome);
            if (contexto is not null && entrada is not null)
            {
                contexto.Database.ExecuteSqlInterpolated(
                    $"INSERT INTO ProdutosCesta (Nome, CategoriaId, Ativo, CriadoEm) VALUES ({entrada.Entity.Nome}, {entrada.Entity.CategoriaId}, {true}, {DateTime.UtcNow})");
                throw new DbUpdateException(
                    "Falha ao gravar.",
                    new InvalidOperationException("UNIQUE constraint failed: ItensCompraCesta.CompraCestaId"));
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}

