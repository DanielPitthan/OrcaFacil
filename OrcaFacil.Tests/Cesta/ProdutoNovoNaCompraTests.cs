using Microsoft.EntityFrameworkCore;
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
    public async Task DoisItensComOMesmoNomeNormalizado_NaoGravamProduto()
    {
        var local = await _service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        var modelo = Modelo(local.Id, "acucar", "Açúcar", 2m);
        modelo.Itens.Add(new ItemCompraCestaFormModel { ProdutoNovoChave = "acucar", Preco = 3m });

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SalvarCompraAsync(modelo, _membroId));

        await using var contexto = _factory.CreateDbContext();
        Assert.Empty(await contexto.ProdutosCesta.ToListAsync());
        Assert.Empty(await contexto.ComprasCesta.ToListAsync());
    }

    public void Dispose() => _factory.Dispose();

    private CompraCestaFormModel Modelo(int localId, string chave, string nome, decimal preco) => new()
    {
        LocalCompraId = localId,
        Data = new DateTime(2026, 10, 18),
        ProdutosNovos =
        [
            new ProdutoNovoCompra
            {
                Chave = chave,
                Nome = nome,
                CategoriaId = _alimentacao.Id
            }
        ],
        Itens = [new ItemCompraCestaFormModel { ProdutoNovoChave = chave, Preco = preco }]
    };
}
