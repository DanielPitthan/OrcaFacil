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
/// O casamento da prévia só grava depois de CasamentoConfirmado. Comentário no fonte não conta.
/// </summary>
public class ConfirmacaoParProdutoTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly CestaCadastroService _service;
    private readonly int _membroId;
    private readonly Categoria _alimentacao;
    private readonly ProdutoCesta _arroz;
    private readonly ProdutoCesta _feijao;
    private readonly ProdutoCesta _inativo;

    public ConfirmacaoParProdutoTests()
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
        _arroz = new ProdutoCesta
        {
            Nome = "Arroz",
            CategoriaId = _alimentacao.Id,
            Categoria = _alimentacao,
            Ativo = true
        };
        _feijao = new ProdutoCesta
        {
            Nome = "Feijão",
            CategoriaId = _alimentacao.Id,
            Categoria = _alimentacao,
            Ativo = true
        };
        _inativo = new ProdutoCesta
        {
            Nome = "Velho",
            CategoriaId = _alimentacao.Id,
            Categoria = _alimentacao,
            Ativo = false
        };
        contexto.ProdutosCesta.AddRange(_arroz, _feijao, _inativo);
        contexto.SaveChanges();
    }

    [Fact]
    public async Task ComPar_ConfiancaAlta_SoGravaDepoisDeConfirmar()
    {
        var local = await _service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        var previa = Previa(local, Lido("ARROZ TIPO 1", _arroz.Id, true, 0.95m), _arroz, _feijao);
        var linha = Assert.Single(previa.Linhas);
        Assert.Equal("Arroz", linha.Nome);
        Assert.Equal(_arroz.Id, linha.ProdutoCestaId);
        Assert.False(linha.NaoIdentificado);
        Assert.False(linha.CasamentoConfirmado);
        Assert.True(previa.PodeConfirmar);
        Assert.True(ConfirmacaoParProduto.ExigeConfirmacao(linha));
        Assert.False(ConfirmacaoParProduto.PodeGravar(previa.Linhas));
        Assert.Equal(
            CasamentoComprovante.MensagemConfirmacaoObrigatoria,
            ConfirmacaoParProduto.MensagemBloqueio(previa.Linhas));
        Assert.False(Assert.Single(previa.ItensComProduto()).Confirmado);
        await AssertNaoGravaAsync(previa);

        foreach (var confianca in new[] { 0.8m, 1m, 0.99m })
        {
            var alto = Previa(local, Lido("ARROZ TIPO 1", _arroz.Id, true, confianca), _arroz, _feijao);
            var item = Assert.Single(alto.Linhas);
            Assert.False(item.CasamentoConfirmado);
            Assert.False(ConfirmacaoParProduto.PodeGravar(alto.Linhas));
            await AssertNaoGravaAsync(alto);
        }

        var dois = Previa(
            local,
            [Lido("ARROZ TIPO 1", _arroz.Id, true, 0.8m), Lido("FEIJAO", _feijao.Id, true, 1m)],
            _arroz,
            _feijao);
        Assert.Equal(2, dois.Linhas.Count);
        Assert.False(ConfirmacaoParProduto.PodeGravar(dois.Linhas));
        ConfirmacaoParProduto.Confirmar(dois.Linhas[0]);
        Assert.True(dois.Linhas[0].CasamentoConfirmado);
        Assert.False(dois.Linhas[1].CasamentoConfirmado);
        Assert.False(ConfirmacaoParProduto.PodeGravar(dois.Linhas));
        await AssertNaoGravaAsync(dois);
        ConfirmacaoParProduto.Confirmar(dois.Linhas[1]);
        Assert.True(ConfirmacaoParProduto.PodeGravar(dois.Linhas));
        CasamentoComprovante.GarantirConfirmacao(dois.ItensComProduto());

        ConfirmacaoParProduto.Confirmar(linha);
        Assert.True(linha.CasamentoConfirmado);
        Assert.False(ConfirmacaoParProduto.ExigeConfirmacao(linha));
        Assert.True(ConfirmacaoParProduto.PodeGravar(previa.Linhas));
        Assert.Null(ConfirmacaoParProduto.MensagemBloqueio(previa.Linhas));
        Assert.True(Assert.Single(previa.ItensComProduto()).Confirmado);
        CasamentoComprovante.GarantirConfirmacao(previa.ItensComProduto());

        var compra = await _service.SalvarCompraAsync(Copiar(previa.ParaCompra()), _membroId);
        var gravado = Assert.Single(compra.Itens);
        Assert.Equal(_arroz.Id, gravado.ProdutoCestaId);
        Assert.Equal(10m, gravado.Preco);
        Assert.Equal(1, await ContarComprasAsync());
        Assert.True(ConfirmacaoParProduto.PodeGravar([]));
    }

    [Fact]
    public async Task SemPar_ContinuaNaoIdentificado()
    {
        var local = await _service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        var previa = Previa(local, Lido("SABONETE", null, true, 0.99m), _arroz);
        var linha = Assert.Single(previa.Linhas);
        Assert.True(linha.NaoIdentificado);
        Assert.True(linha.PodeCadastrar);
        Assert.Null(linha.ProdutoCestaId);
        Assert.False(linha.CasamentoConfirmado);
        Assert.False(ConfirmacaoParProduto.ExigeConfirmacao(linha));
        Assert.True(ConfirmacaoParProduto.PodeGravar(previa.Linhas));
        Assert.StartsWith(NormalizadorComprovante.PrefixoNaoIdentificado, linha.Rotulo, StringComparison.Ordinal);
        Assert.False(previa.PodeConfirmar);
        Assert.Throws<InvalidOperationException>(() => previa.ParaCompra());
        ConfirmacaoParProduto.Confirmar(linha);
        Assert.False(linha.CasamentoConfirmado);
        Assert.NotNull(previa.IniciarCadastro(linha));
        Assert.Equal(0, await ContarComprasAsync());
        Assert.Equal(0, await ContarItensAsync());
    }

    [Theory]
    [InlineData(1_000_000)]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(null)]
    public async Task IdInvalidoOuNull_ViraNaoIdentificado(int? id)
    {
        var local = await _service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado " + id });
        var previa = Previa(local, Lido("ARROZ TIPO 1", id, true, 0.99m), _arroz, _feijao);
        var linha = Assert.Single(previa.Linhas);
        Assert.True(linha.NaoIdentificado);
        Assert.Null(linha.ProdutoCestaId);
        Assert.False(linha.CasamentoConfirmado);
        Assert.False(ConfirmacaoParProduto.ExigeConfirmacao(linha));
        Assert.True(ConfirmacaoParProduto.PodeGravar(previa.Linhas));
        Assert.DoesNotContain("Arroz", linha.Nome, StringComparison.Ordinal);
        Assert.StartsWith(NormalizadorComprovante.PrefixoNaoIdentificado, linha.Rotulo, StringComparison.Ordinal);
        Assert.False(previa.PodeConfirmar);
        Assert.Throws<InvalidOperationException>(() => previa.ParaCompra());
        Assert.Equal(0, await ContarComprasAsync());
    }

    [Fact]
    public async Task Trocar_GravaOProdutoEscolhido()
    {
        var local = await _service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        var previa = Previa(local, Lido("ARROZ TIPO 1", _arroz.Id, true, 0.91m), _arroz, _feijao, _inativo);
        var linha = Assert.Single(previa.Linhas);
        Assert.False(linha.CasamentoConfirmado);
        await AssertNaoGravaAsync(previa);

        Assert.False(ConfirmacaoParProduto.Trocar(previa, linha, new ProdutoCesta { Id = 0, Nome = "Zero", Ativo = true }, Catalogo()));
        Assert.False(ConfirmacaoParProduto.Trocar(previa, linha, new ProdutoCesta { Id = -1, Nome = "Negativo", Ativo = true }, Catalogo()));
        Assert.False(ConfirmacaoParProduto.Trocar(previa, linha, new ProdutoCesta { Id = 1_000_000, Nome = "Ausente", Ativo = true }, Catalogo()));
        Assert.False(ConfirmacaoParProduto.Trocar(previa, linha, _inativo, Catalogo()));
        Assert.Equal(_arroz.Id, linha.ProdutoCestaId);
        Assert.False(linha.CasamentoConfirmado);
        Assert.Equal(0, await ContarComprasAsync());

        Assert.True(ConfirmacaoParProduto.Trocar(previa, linha, _feijao, Catalogo()));
        Assert.Equal(_feijao.Id, linha.ProdutoCestaId);
        Assert.Equal("Feijão", linha.Nome);
        Assert.False(linha.NaoIdentificado);
        Assert.True(linha.CasamentoConfirmado);
        Assert.True(ConfirmacaoParProduto.PodeGravar(previa.Linhas));
        var trocado = Assert.Single(previa.ItensComProduto());
        Assert.Equal(_feijao.Id, trocado.ProdutoId);
        Assert.True(trocado.Confirmado);
        CasamentoComprovante.GarantirConfirmacao(previa.ItensComProduto());

        var compra = await _service.SalvarCompraAsync(Copiar(previa.ParaCompra()), _membroId);
        var item = Assert.Single(compra.Itens);
        Assert.Equal(_feijao.Id, item.ProdutoCestaId);
        Assert.NotEqual(_arroz.Id, item.ProdutoCestaId);
    }

    [Fact]
    public async Task Desfazer_VoltaNaoIdentificadoEMantemCadastrar()
    {
        var local = await _service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        var previa = Previa(local, Lido("ARROZ TIPO 1", _arroz.Id, true, 1m), _arroz, _feijao);
        var linha = Assert.Single(previa.Linhas);
        Assert.False(linha.PodeCadastrar);
        Assert.True(ConfirmacaoParProduto.Trocar(previa, linha, _feijao, Catalogo()));

        ConfirmacaoParProduto.Desfazer(linha);
        Assert.True(linha.NaoIdentificado);
        Assert.True(linha.PodeCadastrar);
        Assert.Null(linha.ProdutoCestaId);
        Assert.False(linha.CasamentoConfirmado);
        Assert.False(ConfirmacaoParProduto.ExigeConfirmacao(linha));
        Assert.True(ConfirmacaoParProduto.PodeGravar(previa.Linhas));
        Assert.StartsWith(NormalizadorComprovante.PrefixoNaoIdentificado, linha.Rotulo, StringComparison.Ordinal);
        ConfirmacaoParProduto.Desfazer(linha);
        Assert.True(linha.NaoIdentificado);
        Assert.False(previa.PodeConfirmar);
        Assert.Throws<InvalidOperationException>(() => previa.ParaCompra());

        var formulario = previa.IniciarCadastro(linha);
        Assert.NotNull(formulario);
        Assert.Equal("ARROZ TIPO 1", formulario.Nome);
        Assert.DoesNotContain(NormalizadorComprovante.PrefixoNaoIdentificado, formulario.Nome, StringComparison.Ordinal);
        Assert.Equal(0, await ContarComprasAsync());
        Assert.Equal(0, await ContarItensAsync());
    }

    [Fact]
    public async Task PendenteNaoLiberaGravacao()
    {
        var local = await _service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        var previa = Previa(local, Lido("ARROZ TIPO 1", _arroz.Id, true, 0.2m), _arroz);
        var linha = Assert.Single(previa.Linhas);
        Assert.True(linha.Pendente);
        Assert.Contains("pendente", linha.MarcadorCasamento, StringComparison.Ordinal);
        Assert.False(linha.CasamentoConfirmado);
        Assert.False(ConfirmacaoParProduto.PodeGravar(previa.Linhas));
        await AssertNaoGravaAsync(previa);

        var alta = Previa(local, Lido("ARROZ TIPO 1", _arroz.Id, true, 0.95m), _arroz);
        Assert.False(Assert.Single(alta.Linhas).Pendente);
        Assert.False(alta.Linhas[0].CasamentoConfirmado);
        Assert.NotNull(alta.Linhas[0].MarcadorCasamento);
        await AssertNaoGravaAsync(alta);

        ConfirmacaoParProduto.Confirmar(linha);
        Assert.True(linha.CasamentoConfirmado);
        Assert.True(linha.Pendente);
        Assert.Contains("pendente", linha.MarcadorCasamento, StringComparison.Ordinal);
        Assert.True(ConfirmacaoParProduto.PodeGravar(previa.Linhas));
        CasamentoComprovante.GarantirConfirmacao(previa.ItensComProduto());
    }

    [Fact]
    public void PodeGravar_NaoUsaConfiancaNemPendente_ComentarioNaoConta()
    {
        var fonte = Ler("OrcaFacil.Core", "Cesta", "ConfirmacaoParProduto.cs");
        Assert.True(PortaoIgnoraConfianca(fonte));
        var confirmar = Metodo(SemComentarios(fonte), "void Confirmar(");
        Assert.Contains("linha.CasamentoConfirmado = true", confirmar, StringComparison.Ordinal);
        Assert.DoesNotContain("Confianca", confirmar, StringComparison.Ordinal);
        Assert.DoesNotContain("Pendente", confirmar, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfirmarCasamentos", fonte, StringComparison.Ordinal);

        var mutado = fonte.Replace(
            "linha.ProdutoCestaId is > 0 && !linha.CasamentoConfirmado",
            "linha.ProdutoCestaId is > 0 && !linha.CasamentoConfirmado && linha.Saidas.Min(item => item.Confianca) < 0.8m",
            StringComparison.Ordinal);
        Assert.False(PortaoIgnoraConfianca(mutado));

        const string soComentario = """
            // linha.CasamentoConfirmado
            /* CasamentoConfirmado */
            public static bool PodeGravar(IReadOnlyList<LinhaPreviaComprovante> linhas)
            {
                return linha.Saidas.Any(item => item.Confianca >= 0.8m || !item.Pendente);
            }
            public static string? MensagemBloqueio
            """;
        Assert.Contains("CasamentoConfirmado", soComentario, StringComparison.Ordinal);
        Assert.False(PortaoIgnoraConfianca(soComentario));
        Assert.DoesNotContain("CasamentoConfirmado", SemComentarios(soComentario), StringComparison.Ordinal);
    }

    [Fact]
    public void Previa_BotoesEBloqueio_ComentarioNaoConta()
    {
        var previa = Ler("OrcaFacil", "Components", "Pages", "Cesta", "PreviaComprovanteDialog.razor");
        var par = Ler("OrcaFacil", "Components", "Pages", "Cesta", "ParProdutoPrevia.razor");
        var item = Ler("OrcaFacil", "Components", "Pages", "Cesta", "ItemParPrevia.razor");
        var pagina = Ler("OrcaFacil", "Components", "Pages", "Cesta", "CestaPage.razor");
        var css = Ler("OrcaFacil", "Components", "Pages", "Cesta", "ParProdutoPrevia.razor.css")
            + Ler("OrcaFacil", "Components", "Pages", "Cesta", "ItemParPrevia.razor.css");
        Assert.True(DialogoUsaBarreira(previa));
        Assert.True(ComponenteLigaPares(par));
        Assert.True(ItemMostraAcoes(item));
        Assert.True(PaginaEntregaCatalogo(pagina));
        Assert.True(CssDoPar(css));

        var mutado = par.Replace(
            "ConfirmacaoParProduto.Confirmar(linha);",
            "// ConfirmacaoParProduto.Confirmar(linha);",
            StringComparison.Ordinal);
        Assert.False(ComponenteLigaPares(mutado));

        var semBotao = item.Replace(">Confirmar</button>", "<!-- >Confirmar</button> -->", StringComparison.Ordinal);
        Assert.Contains(">Confirmar</button>", semBotao, StringComparison.Ordinal);
        Assert.False(ItemMostraAcoes(semBotao));

        var semTrocar = par.Replace(
            "ConfirmacaoParProduto.Trocar(Previa, linha, produto, Catalogo);",
            "/* ConfirmacaoParProduto.Trocar(Previa, linha, produto, Catalogo); */",
            StringComparison.Ordinal);
        Assert.False(ComponenteLigaPares(semTrocar));

        var semDesfazer = par.Replace(
            "ConfirmacaoParProduto.Desfazer(linha);",
            "// ConfirmacaoParProduto.Desfazer(linha);",
            StringComparison.Ordinal);
        Assert.False(ComponenteLigaPares(semDesfazer));

        var semMensagem = par.Replace(
            "<p class=\"of-par-alerta of-previa-erro\" role=\"alert\">@mensagem</p>",
            "<!-- <p class=\"of-par-alerta of-previa-erro\" role=\"alert\">@mensagem</p> -->",
            StringComparison.Ordinal);
        Assert.False(ComponenteLigaPares(semMensagem));

        var semMarcador = previa.Replace("linha.MarcadorCasamento", "// linha.MarcadorCasamento", StringComparison.Ordinal);
        Assert.False(DialogoUsaBarreira(semMarcador));

        var cssMutado = css.Replace("color: #1E2A33;", "/* color: #1E2A33; */", StringComparison.Ordinal);
        Assert.Contains("color: #1E2A33;", cssMutado, StringComparison.Ordinal);
        Assert.False(CssDoPar(cssMutado));
        Assert.False(CssDoPar(css.Replace(".of-par-botao:focus {", ".of-par-botao:focus-visible {", StringComparison.Ordinal)));
        Assert.False(CssDoPar(css + "\n.of-par:has(.of-par-botao) { color: #1E2A33; }\n"));
        Assert.False(CssDoPar(css.Replace("display: flex;", "display: flex;\n    gap: 8px;", StringComparison.Ordinal)));

        const string soComentario = """
            @* >Confirmar</button> *@
            // ConfirmacaoParProduto.Confirmar(linha);
            /* ConfirmacaoParProduto.Trocar(Previa, linha, produto, Catalogo); */
            <!-- >Desfazer</button> -->
            <!-- <p class="of-par-alerta of-previa-erro" role="alert">@mensagem</p> -->
            """;
        Assert.Contains(">Confirmar</button>", soComentario, StringComparison.Ordinal);
        Assert.False(ItemMostraAcoes(soComentario));
        Assert.False(ComponenteLigaPares(soComentario));
    }

    public void Dispose() => _factory.Dispose();

    private List<ProdutoCesta> Catalogo() => [_arroz, _feijao, _inativo];

    private async Task AssertNaoGravaAsync(PreviaComprovante previa)
    {
        var compras = await ContarComprasAsync();
        var itens = await ContarItensAsync();
        var excecao = Assert.Throws<InvalidOperationException>(() => previa.ParaCompra());
        Assert.Equal(CasamentoComprovante.MensagemConfirmacaoObrigatoria, excecao.Message);
        Assert.Equal(compras, await ContarComprasAsync());
        Assert.Equal(itens, await ContarItensAsync());
    }

    private async Task<int> ContarComprasAsync()
    {
        await using var contexto = _factory.CreateDbContext();
        return await contexto.ComprasCesta.CountAsync();
    }

    private async Task<int> ContarItensAsync()
    {
        await using var contexto = _factory.CreateDbContext();
        return await contexto.ItensCompraCesta.CountAsync();
    }

    private PreviaComprovante Previa(LocalCompra local, ItemComprovanteLido item, params ProdutoCesta[] produtos) =>
        Previa(local, [item], produtos);

    private PreviaComprovante Previa(
        LocalCompra local,
        IReadOnlyList<ItemComprovanteLido> itens,
        params ProdutoCesta[] produtos) =>
        PreviaComprovante.Criar(
            NormalizadorComprovante.Normalizar(
                new ComprovanteLido(local.Nome, new DateOnly(2026, 10, 18), null, true, itens),
                produtos),
            [local],
            produtos,
            new DateTime(2026, 10, 1));

    private static ItemComprovanteLido Lido(string descricao, int? produtoId, bool informado, decimal confianca) =>
        new(descricao, "", true, 10m, null, null, null, null, produtoId, informado, confianca);

    private static CompraCestaFormModel Copiar(CompraCestaFormModel modelo) => new()
    {
        LocalCompraId = modelo.LocalCompraId,
        Data = modelo.Data,
        ProdutosNovos = modelo.ProdutosNovos,
        Itens = CompraCestaItens.CopiarItens(modelo),
        ItensSaida = modelo.ItensSaida.ToList()
    };

    private static bool PortaoIgnoraConfianca(string fonte)
    {
        var portao = Membro(SemComentarios(fonte), "public static bool PodeGravar(", "public static string? MensagemBloqueio");
        return portao.Contains("CasamentoConfirmado", StringComparison.Ordinal)
            && !portao.Contains("Confianca", StringComparison.Ordinal)
            && !portao.Contains("Pendente", StringComparison.Ordinal);
    }

    private static bool DialogoUsaBarreira(string fonte)
    {
        var limpo = SemComentarios(fonte);
        if (!limpo.Contains("PrimarioDesabilitado=\"@(!PodeGravarCompra)\"", StringComparison.Ordinal))
            return false;
        if (!limpo.Contains("Previa.PodeConfirmar && ConfirmacaoParProduto.PodeGravar(Previa.Linhas)", StringComparison.Ordinal))
            return false;
        if (!limpo.Contains("<ParProdutoPrevia ", StringComparison.Ordinal))
            return false;
        if (!limpo.Contains(">Cadastrar</MudButton>", StringComparison.Ordinal))
            return false;
        if (!limpo.Contains("linha.MarcadorCasamento", StringComparison.Ordinal))
            return false;
        if (limpo.Contains("GarantirAntesDeGravar", StringComparison.Ordinal))
            return false;
        if (limpo.Contains("ParaBarreira", StringComparison.Ordinal))
            return false;
        if (limpo.Contains("TextoConfianca", StringComparison.Ordinal))
            return false;

        var confirmar = Metodo(limpo, "void Confirmar()");
        var bloqueio = confirmar.IndexOf("!Previa.PodeConfirmar || _confirmando", StringComparison.Ordinal);
        var retorno = bloqueio < 0 ? -1 : confirmar.IndexOf("return", bloqueio, StringComparison.Ordinal);
        var garantir = confirmar.IndexOf("CasamentoComprovante.GarantirConfirmacao(comProduto)", StringComparison.Ordinal);
        var trava = confirmar.IndexOf("_confirmando = true", StringComparison.Ordinal);
        var fechar = confirmar.IndexOf("MudDialog.Close", StringComparison.Ordinal);
        if (bloqueio < 0 || retorno <= bloqueio || garantir <= retorno || trava <= garantir || fechar <= trava)
            return false;
        if (confirmar.Split("GarantirConfirmacao(").Length != 2)
            return false;

        var gravar = Membro(limpo, "bool PodeGravarCompra", "private void Cancelar()");
        return !gravar.Contains("Confianca", StringComparison.Ordinal)
            && !gravar.Contains("Pendente", StringComparison.Ordinal);
    }

    private static bool ComponenteLigaPares(string fonte)
    {
        var limpo = SemComentarios(fonte);
        if (limpo.Contains("MarcadorCasamento", StringComparison.Ordinal))
            return false;
        if (limpo.Contains("Confianca", StringComparison.Ordinal))
            return false;
        if (limpo.Contains("confiança", StringComparison.OrdinalIgnoreCase))
            return false;
        if (limpo.Contains("Pendente", StringComparison.Ordinal))
            return false;
        if (limpo.Contains("Revisar", StringComparison.Ordinal))
            return false;

        return limpo.Contains("<ItemParPrevia ", StringComparison.Ordinal)
            && limpo.Contains("ConfirmacaoParProduto.Confirmar(linha);", StringComparison.Ordinal)
            && limpo.Contains("ConfirmacaoParProduto.Trocar(Previa, linha, produto, Catalogo);", StringComparison.Ordinal)
            && limpo.Contains("ConfirmacaoParProduto.Desfazer(linha);", StringComparison.Ordinal)
            && limpo.Contains("<p class=\"of-par-alerta of-previa-erro\" role=\"alert\">@mensagem</p>", StringComparison.Ordinal);
    }

    private static bool ItemMostraAcoes(string fonte)
    {
        var limpo = SemComentarios(fonte);
        if (limpo.Contains("Confianca", StringComparison.Ordinal))
            return false;
        if (limpo.Contains("confiança", StringComparison.OrdinalIgnoreCase))
            return false;
        if (limpo.Contains("Pendente", StringComparison.Ordinal))
            return false;
        if (limpo.Contains("Revisar", StringComparison.Ordinal))
            return false;
        if (limpo.Contains("MarcadorCasamento", StringComparison.Ordinal))
            return false;

        return limpo.Contains(">Confirmar</button>", StringComparison.Ordinal)
            && limpo.Contains(">Trocar</button>", StringComparison.Ordinal)
            && limpo.Contains(">Desfazer</button>", StringComparison.Ordinal)
            && limpo.Contains("Linha.Nome", StringComparison.Ordinal)
            && limpo.Contains("Linha.DescricaoLida", StringComparison.Ordinal);
    }

    private static bool PaginaEntregaCatalogo(string fonte)
    {
        var limpo = SemComentarios(fonte);
        return limpo.Contains("ProdutosCatalogoLeituraAsync", StringComparison.Ordinal)
            && limpo.Contains("CasamentoComprovante.GarantirConfirmacao(comProduto)", StringComparison.Ordinal)
            && !limpo.Contains("_saidas", StringComparison.Ordinal);
    }

    private static bool CssDoPar(string fonte)
    {
        var limpo = SemComentarios(fonte);
        var inicio = limpo.IndexOf(".of-par-bloco", StringComparison.Ordinal);
        if (inicio < 0)
            return false;

        limpo = limpo[inicio..];
        if (limpo.Contains(":focus-visible", StringComparison.Ordinal) || limpo.Contains(":has(", StringComparison.Ordinal))
            return false;
        if (limpo.Contains("gap:", StringComparison.Ordinal))
            return false;
        if (!limpo.Contains("flex-wrap: wrap;", StringComparison.Ordinal))
            return false;
        if (!limpo.Contains("min-width: 0;", StringComparison.Ordinal))
            return false;
        if (!limpo.Contains("max-width: 100%;", StringComparison.Ordinal))
            return false;
        if (!limpo.Contains("color: #1E2A33;", StringComparison.Ordinal))
            return false;
        if (!limpo.Contains("background-color: #ffffff;", StringComparison.Ordinal))
            return false;
        if (!limpo.Contains("color: #E8EAF0;", StringComparison.Ordinal))
            return false;
        if (!limpo.Contains("background-color: #1B1F27;", StringComparison.Ordinal))
            return false;

        return Contraste("#1E2A33", "#ffffff") >= 4.5
            && Contraste("#1E2A33", "#EFF3F7") >= 4.5
            && Contraste("#E8EAF0", "#1B1F27") >= 4.5
            && Contraste("#E8EAF0", "#12151B") >= 4.5
            && Contraste("#9B2C3A", "#ffffff") >= 4.5
            && Contraste("#9B2C3A", "#EFF3F7") >= 4.5
            && Contraste("#F59A9A", "#1B1F27") >= 4.5
            && Contraste("#F59A9A", "#12151B") >= 4.5;
    }

    private static double Contraste(string frente, string fundo)
    {
        var clara = Math.Max(Luminancia(frente), Luminancia(fundo));
        var escura = Math.Min(Luminancia(frente), Luminancia(fundo));
        return (clara + 0.05) / (escura + 0.05);
    }

    private static double Luminancia(string hex)
    {
        hex = hex.TrimStart('#');
        var r = Canal(hex[..2]);
        var g = Canal(hex.Substring(2, 2));
        var b = Canal(hex.Substring(4, 2));
        return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
    }

    private static double Canal(string hex)
    {
        var valor = Convert.ToInt32(hex, 16) / 255d;
        return valor <= 0.04045 ? valor / 12.92 : Math.Pow((valor + 0.055) / 1.055, 2.4);
    }

    private static string Membro(string fonte, string assinatura, string proximo)
    {
        var inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        if (inicio < 0)
            return string.Empty;

        var fim = fonte.IndexOf(proximo, inicio + assinatura.Length, StringComparison.Ordinal);
        return fim < 0 ? fonte[inicio..] : fonte[inicio..fim];
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
