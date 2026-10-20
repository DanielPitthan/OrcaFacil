using System.Text;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Cesta;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Llm;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Core.Services;

namespace OrcaFacil.Tests.Cesta;

/// <summary>
/// O Salvar da prévia grava o nome editado. Comentário no fonte não conta.
/// </summary>
public class ConfirmacaoCadastroProdutoTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly CestaCadastroService _service;
    private readonly int _membroId;
    private readonly Categoria _alimentacao;

    public ConfirmacaoCadastroProdutoTests()
    {
        _service = new CestaCadastroService(new CestaRepository(_factory));
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
    public async Task NomeEditado_BancoVazio_CriaOProdutoEVinculaALinha()
    {
        await AssertBancoVazioAsync();
        var local = await _service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        var previa = Previa(local, ("LEITE INTEGRAL LT", 4.5m), ("SABONETE", 3m));
        Assert.Equal(2, previa.NaoIdentificados);
        Assert.All(previa.Linhas, linha => Assert.True(linha.NaoIdentificado));
        Assert.DoesNotContain(previa.Linhas, linha => linha.NovoSeraCriado);

        var linha = previa.Linhas[0];
        var formulario = previa.IniciarCadastro(linha);
        Assert.NotNull(formulario);
        Assert.Equal("LEITE INTEGRAL LT", formulario.Nome);
        Assert.DoesNotContain(NormalizadorComprovante.PrefixoNaoIdentificado, formulario.Nome, StringComparison.Ordinal);
        const string editado = "Leite integral";
        Assert.NotEqual(editado, formulario.Nome);
        formulario.Nome = ConfirmacaoCadastroProduto.NomeParaSalvar(formulario.Nome, editado);
        formulario.CategoriaId = _alimentacao.Id;
        Assert.Null(ConfirmacaoCadastroProduto.Validar(formulario.Nome, formulario.CategoriaId, false));
        previa.ConfirmarCadastro(formulario, linha.Preco, _alimentacao.Nome);

        Assert.Equal(editado, linha.NomeGravado);
        Assert.Equal(editado, linha.Nome);
        Assert.True(linha.NovoSeraCriado);
        Assert.False(linha.NaoIdentificado);
        Assert.Equal(NormalizadorComprovante.TextoComparacao(editado), linha.ProdutoNovoChave);
        Assert.True(previa.Linhas[1].NaoIdentificado);
        Assert.Null(previa.Linhas[1].ProdutoCestaId);
        Assert.False(previa.Linhas[1].NovoSeraCriado);
        Assert.False(previa.PodeConfirmar);
        await AssertBancoVazioAsync();

        previa.Remover(previa.Linhas[1]);
        Assert.True(previa.PodeConfirmar);
        var pendente = Assert.Single(previa.ParaCompra().ProdutosNovos);
        Assert.Equal(editado, pendente.Nome);
        Assert.Equal(linha.ProdutoNovoChave, Assert.Single(previa.ParaCompra().Itens).ProdutoNovoChave);
        var compra = await _service.SalvarCompraAsync(Copiar(previa.ParaCompra()), _membroId);

        await using var depois = _factory.CreateDbContext();
        var produto = Assert.Single(await depois.ProdutosCesta.ToListAsync());
        var item = Assert.Single(compra.Itens);
        Assert.Equal(editado, produto.Nome);
        Assert.Equal(_alimentacao.Id, produto.CategoriaId);
        Assert.Equal(produto.Id, item.ProdutoCestaId);
        Assert.Equal(4.5m, item.Preco);
        Assert.NotEqual("LEITE INTEGRAL LT", produto.Nome);
    }

    [Fact]
    public async Task NomeOriginalSemEdicao_BancoVazio_GravaONomeCortadoSemPrefixo()
    {
        await AssertBancoVazioAsync();
        var local = await _service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        var descricao = NormalizadorComprovante.PrefixoNaoIdentificado + new string('A', 90) + "Z";
        var previa = Previa(local, (descricao, 6.2m));
        var linha = Assert.Single(previa.Linhas);
        Assert.True(linha.NaoIdentificado);
        Assert.StartsWith(NormalizadorComprovante.PrefixoNaoIdentificado, linha.Rotulo, StringComparison.Ordinal);

        var formulario = previa.IniciarCadastro(linha);
        Assert.NotNull(formulario);
        var original = new string('A', 80);
        Assert.Equal(original, formulario.Nome);
        Assert.Equal(80, formulario.Nome.Length);
        Assert.DoesNotContain(NormalizadorComprovante.PrefixoNaoIdentificado, formulario.Nome, StringComparison.Ordinal);
        Assert.DoesNotContain("Z", formulario.Nome, StringComparison.Ordinal);
        formulario.Nome = ConfirmacaoCadastroProduto.NomeParaSalvar(formulario.Nome, formulario.Nome);
        Assert.Equal(original, ConfirmacaoCadastroProduto.NomeParaSalvar(original, null));
        formulario.CategoriaId = _alimentacao.Id;
        Assert.Null(ConfirmacaoCadastroProduto.Validar(formulario.Nome, formulario.CategoriaId, false));
        previa.ConfirmarCadastro(formulario, linha.Preco, _alimentacao.Nome);

        Assert.Equal(original, linha.NomeGravado);
        Assert.True(linha.NovoSeraCriado);
        Assert.False(linha.NaoIdentificado);
        Assert.Equal(linha.ProdutoNovoChave, Assert.Single(previa.ParaCompra().ProdutosNovos).Chave);
        await AssertBancoVazioAsync();

        Assert.True(previa.PodeConfirmar);
        var compra = await _service.SalvarCompraAsync(Copiar(previa.ParaCompra()), _membroId);

        await using var depois = _factory.CreateDbContext();
        var produto = Assert.Single(await depois.ProdutosCesta.ToListAsync());
        Assert.Equal(original, produto.Nome);
        Assert.Equal(produto.Id, Assert.Single(compra.Itens).ProdutoCestaId);
        Assert.DoesNotContain(NormalizadorComprovante.PrefixoNaoIdentificado, produto.Nome, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BancoVazio_ValidacaoApareceENaoGrava()
    {
        await AssertBancoVazioAsync();
        Assert.Equal(
            ConfirmacaoCadastroProduto.MensagemNome,
            ConfirmacaoCadastroProduto.Validar("   ", _alimentacao.Id, false));
        Assert.Equal(
            ConfirmacaoCadastroProduto.MensagemCategoria,
            ConfirmacaoCadastroProduto.Validar("Leite integral", 0, false));
        Assert.Equal(
            ConfirmacaoCadastroProduto.MensagemNomeLongo,
            ConfirmacaoCadastroProduto.Validar(new string('B', 81), _alimentacao.Id, false));
        Assert.Null(ConfirmacaoCadastroProduto.Validar("Leite integral", _alimentacao.Id, false));
        Assert.False(string.IsNullOrWhiteSpace(ConfirmacaoCadastroProduto.MensagemFalha));

        var local = await _service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        var previa = Previa(local, ("LEITE", 2m));
        var linha = Assert.Single(previa.Linhas);
        var formulario = previa.IniciarCadastro(linha);
        Assert.NotNull(formulario);
        formulario.Nome = ConfirmacaoCadastroProduto.NomeParaSalvar(formulario.Nome, "   ");
        var erro = ConfirmacaoCadastroProduto.Validar(formulario.Nome, _alimentacao.Id, false);
        Assert.Equal(ConfirmacaoCadastroProduto.MensagemNome, erro);
        Assert.True(linha.NaoIdentificado);
        Assert.False(linha.NovoSeraCriado);
        await AssertBancoVazioAsync();
    }

    [Fact]
    public void NomeParaSalvar_PrefereOTextoEditado()
    {
        Assert.Equal("Leite integral", ConfirmacaoCadastroProduto.NomeParaSalvar("LEITE INTEGRAL LT", "Leite integral"));
        Assert.Equal("Leite integral", ConfirmacaoCadastroProduto.NomeParaSalvar("LEITE INTEGRAL LT", "  Leite integral  "));
        Assert.Equal("LEITE INTEGRAL LT", ConfirmacaoCadastroProduto.NomeParaSalvar("LEITE INTEGRAL LT", "LEITE INTEGRAL LT"));
        Assert.Equal("LEITE INTEGRAL LT", ConfirmacaoCadastroProduto.NomeParaSalvar("LEITE INTEGRAL LT", null));
        Assert.Equal(string.Empty, ConfirmacaoCadastroProduto.NomeParaSalvar("LEITE INTEGRAL LT", "   "));
    }

    [Fact]
    public void Dialogo_UsaOTextoEditado_EMostraErro_ComentarioNaoConta()
    {
        var dialogo = Ler("OrcaFacil", "Components", "Pages", "Cesta", "ProdutoCestaDialog.razor");
        var previa = Ler("OrcaFacil", "Components", "Pages", "Cesta", "PreviaComprovanteDialog.razor");

        Assert.True(DialogoComprometeOTexto(dialogo));
        Assert.True(PreviaMostraExcecao(previa));

        var mutado = dialogo
            .Replace("Immediate=\"true\"", "/* Immediate=\"true\" */", StringComparison.Ordinal)
            .Replace(
                "ConfirmacaoCadastroProduto.NomeParaSalvar(Modelo.Nome, _textoNome)",
                "/* ConfirmacaoCadastroProduto.NomeParaSalvar(Modelo.Nome, _textoNome) */",
                StringComparison.Ordinal)
            .Replace("role=\"alert\"", "<!-- role=\"alert\" -->", StringComparison.Ordinal)
            .Replace("catch (Exception ex)", "// catch (Exception ex)", StringComparison.Ordinal);
        Assert.False(DialogoComprometeOTexto(mutado));

        const string soComentario = """
            @* Immediate="true" *@
            @* @bind-Value="_textoNome" *@
            // ConfirmacaoCadastroProduto.NomeParaSalvar(Modelo.Nome, _textoNome)
            /* _textoNome = Modelo.Nome */
            <!-- role="alert" -->
            // catch (Exception ex)
            """;
        Assert.False(DialogoComprometeOTexto(soComentario));
        Assert.Contains("Immediate=\"true\"", soComentario, StringComparison.Ordinal);
        Assert.DoesNotContain("Immediate=\"true\"", SemComentarios(soComentario), StringComparison.Ordinal);
    }

    public void Dispose() => _factory.Dispose();

    private async Task AssertBancoVazioAsync()
    {
        await using var contexto = _factory.CreateDbContext();
        Assert.Empty(await contexto.ProdutosCesta.ToListAsync());
    }

    private static PreviaComprovante Previa(LocalCompra local, params (string Descricao, decimal Preco)[] linhas) =>
        PreviaComprovante.Criar(
            NormalizadorComprovante.Normalizar(
                new ComprovanteLido(
                    local.Nome,
                    new DateOnly(2026, 10, 18),
                    null,
                    true,
                    linhas.Select(linha => new ItemComprovanteLido(
                        linha.Descricao, "", false, linha.Preco, null, null, null, null)).ToList()),
                []),
            [local],
            [],
            new DateTime(2026, 10, 1));

    private static CompraCestaFormModel Copiar(CompraCestaFormModel modelo) => new()
    {
        LocalCompraId = modelo.LocalCompraId,
        Data = modelo.Data,
        ProdutosNovos = modelo.ProdutosNovos,
        Itens = CompraCestaItens.CopiarItens(modelo)
    };

    private static bool DialogoComprometeOTexto(string fonte)
    {
        var limpo = SemComentarios(fonte);
        var salvar = Metodo(limpo, "Task SalvarAsync()");
        var nome = salvar.IndexOf("ConfirmacaoCadastroProduto.NomeParaSalvar(Modelo.Nome, _textoNome)", StringComparison.Ordinal);
        var validar = salvar.IndexOf("ConfirmacaoCadastroProduto.Validar(", StringComparison.Ordinal);
        var erro = salvar.IndexOf("if (_erro is not null)", StringComparison.Ordinal);
        var fechar = salvar.IndexOf("MudDialog.Close", StringComparison.Ordinal);
        return limpo.Contains("Immediate=\"true\"", StringComparison.Ordinal)
            && limpo.Contains("@bind-Value=\"_textoNome\"", StringComparison.Ordinal)
            && limpo.Contains("_textoNome = Modelo.Nome", StringComparison.Ordinal)
            && limpo.Contains("class=\"of-previa-erro\"", StringComparison.Ordinal)
            && limpo.Contains("role=\"alert\"", StringComparison.Ordinal)
            && !limpo.Contains("Color.Error", StringComparison.Ordinal)
            && nome >= 0
            && validar > nome
            && salvar.Contains("_erro = ConfirmacaoCadastroProduto.Validar(", StringComparison.Ordinal)
            && salvar.Contains("catch (Exception ex)", StringComparison.Ordinal)
            && salvar.Contains("_erro =", StringComparison.Ordinal)
            && erro > validar
            && fechar > erro;
    }

    private static bool PreviaMostraExcecao(string fonte)
    {
        var metodo = Metodo(SemComentarios(fonte), "Task CadastrarAsync()");
        return metodo.Contains("catch (Exception ex)", StringComparison.Ordinal)
            && metodo.Contains("Snackbar.Add", StringComparison.Ordinal)
            && !metodo.Contains("catch (InvalidOperationException", StringComparison.Ordinal);
    }

    private static string Metodo(string fonte, string assinatura)
    {
        var inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        if (inicio < 0)
            return string.Empty;

        var abre = fonte.IndexOf('{', inicio);
        if (abre < 0)
            return string.Empty;

        var profundidade = 0;
        for (var i = abre; i < fonte.Length; i++)
        {
            if (fonte[i] == '{')
                profundidade++;
            else if (fonte[i] == '}')
            {
                profundidade--;
                if (profundidade == 0)
                    return fonte[inicio..(i + 1)];
            }
        }

        return string.Empty;
    }

    private static string SemComentarios(string fonte)
    {
        var texto = new StringBuilder(fonte.Length);
        for (var i = 0; i < fonte.Length; i++)
        {
            var c = fonte[i];
            if (c == '"')
            {
                texto.Append(c);
                for (i++; i < fonte.Length; i++)
                {
                    texto.Append(fonte[i]);
                    if (fonte[i] == '\\' && i + 1 < fonte.Length)
                    {
                        texto.Append(fonte[++i]);
                        continue;
                    }

                    if (fonte[i] == '"')
                        break;
                }

                continue;
            }

            if (c == '@' && i + 1 < fonte.Length && fonte[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < fonte.Length && !(fonte[i] == '*' && fonte[i + 1] == '@'))
                    i++;
                if (i + 1 < fonte.Length)
                    i++;
                continue;
            }

            if (c == '<' && FonteComecaCom(fonte, i, "<!--"))
            {
                i += 4;
                while (i + 2 < fonte.Length && !FonteComecaCom(fonte, i, "-->"))
                    i++;
                if (i + 2 < fonte.Length)
                    i += 2;
                continue;
            }

            if (c == '/' && i + 1 < fonte.Length && fonte[i + 1] == '/')
            {
                while (i < fonte.Length && fonte[i] != '\n')
                    i++;
                if (i < fonte.Length)
                    texto.Append('\n');
                continue;
            }

            if (c == '/' && i + 1 < fonte.Length && fonte[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < fonte.Length && !(fonte[i] == '*' && fonte[i + 1] == '/'))
                    i++;
                if (i + 1 < fonte.Length)
                    i++;
                continue;
            }

            texto.Append(c);
        }

        return texto.ToString();
    }

    private static bool FonteComecaCom(string fonte, int indice, string trecho)
    {
        if (indice + trecho.Length > fonte.Length)
            return false;

        for (var i = 0; i < trecho.Length; i++)
        {
            if (fonte[indice + i] != trecho[i])
                return false;
        }

        return true;
    }

    private static string Ler(params string[] partes)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var caminho = Path.Combine(new[] { dir.FullName }.Concat(partes).ToArray());
            if (File.Exists(caminho))
                return File.ReadAllText(caminho);

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar " + string.Join('/', partes));
    }
}
