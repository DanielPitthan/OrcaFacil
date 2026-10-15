using OrcaFacil.Core.Cesta;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Core.Services;

namespace OrcaFacil.Tests.Cesta;

public class ProdutoCestaCsvTests
{
    [Fact]
    public void Ler_ComCabecalhoEAcentos_SeparaNomeECategoria()
    {
        var leitura = ProdutoCestaCsv.Ler("Produto;Categoria\nAçúcar;Alimentação\nDetergente;Limpeza\n");

        Assert.Empty(leitura.Erros);
        Assert.Equal(new[] { "Açúcar", "Detergente" }, leitura.Linhas.Select(l => l.Nome));
        Assert.Equal("Alimentação", leitura.Linhas[0].Categoria);
    }

    [Fact]
    public void Ler_SoNome_DeixaCategoriaVazia()
    {
        var leitura = ProdutoCestaCsv.Ler("Arroz\nFeijão\n");

        Assert.Equal(2, leitura.Linhas.Count);
        Assert.All(leitura.Linhas, l => Assert.Null(l.Categoria));
    }

    [Fact]
    public void Ler_VirgulaEAspas_NaoParteONome()
    {
        var leitura = ProdutoCestaCsv.Ler("Nome,Categoria\n\"Feijão, preto\",Alimentação\n");

        Assert.Equal("Feijão, preto", leitura.Linhas[0].Nome);
        Assert.Equal("Alimentação", leitura.Linhas[0].Categoria);
    }
}

public class ImportacaoProdutosTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly CestaCadastroService _service;
    private readonly Categoria _alimentacao;

    public ImportacaoProdutosTests()
    {
        _service = new CestaCadastroService(new CestaRepository(_factory));
        using var contexto = _factory.CreateDbContext();
        _alimentacao = new Categoria { Nome = "Alimentação", Icone = "🛒", CorHex = "#B2CFE6" };
        contexto.Categorias.Add(_alimentacao);
        contexto.SaveChanges();
    }

    [Fact]
    public async Task Importar_CriaNovosEIgnoraRepetido()
    {
        await _service.SalvarProdutoAsync(new ProdutoCestaFormModel { Nome = "Arroz", CategoriaId = _alimentacao.Id });
        var leitura = ProdutoCestaCsv.Ler("Arroz;Alimentação\nAçúcar;Alimentação\nSabão;Inexistente\n");

        var resultado = await _service.ImportarProdutosAsync(leitura.Linhas, new[] { _alimentacao }, null);

        Assert.Equal(1, resultado.Importados);
        Assert.Equal(1, resultado.Ignorados);
        Assert.Contains("Inexistente", resultado.Erros[0]);

        var produtos = await _service.ListarProdutosAsync(false);
        Assert.Contains(produtos, p => p.Nome == "Açúcar");
    }

    [Fact]
    public async Task Importar_SemCategoria_UsaAPadrao()
    {
        var leitura = ProdutoCestaCsv.Ler("Feijão\n");

        var resultado = await _service.ImportarProdutosAsync(leitura.Linhas, new[] { _alimentacao }, _alimentacao.Id);

        Assert.Equal(1, resultado.Importados);
        var produto = Assert.Single(await _service.ListarProdutosAsync(false));
        Assert.Equal(_alimentacao.Id, produto.CategoriaId);
    }

    public void Dispose() => _factory.Dispose();
}
