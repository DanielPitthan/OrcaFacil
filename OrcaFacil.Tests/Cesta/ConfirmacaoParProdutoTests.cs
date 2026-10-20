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
/// Trocar e Desfazer desmarcam. Marcar todos não marca pendente. Comentário no fonte não conta.
/// </summary>
public class ConfirmacaoParProdutoTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly CestaCadastroService _service;
    private readonly int _membroId;
    private readonly Categoria _alimentacao;
    private readonly ProdutoCesta _arroz;
    private readonly ProdutoCesta _feijao;
    private readonly ProdutoCesta _leite;
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
        _leite = new ProdutoCesta
        {
            Nome = "Leite",
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
        contexto.ProdutosCesta.AddRange(_arroz, _feijao, _leite, _inativo);
        contexto.SaveChanges();
    }

    [Fact]
    public async Task Trocar_DesmarcaEExigeNovaMarcacao()
    {
        var local = await _service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        var previa = Previa(local, Lido("ARROZ TIPO 1", _arroz.Id, true, 0.95m), _arroz, _feijao, _inativo);
        var linha = Assert.Single(previa.Linhas);
        Assert.Equal(_arroz.Id, linha.ProdutoCestaId);
        Assert.False(linha.Marcado);
        Assert.False(linha.CasamentoConfirmado);
        Assert.False(linha.Pendente);
        linha.Marcado = true;
        previa.ConfirmarMarcados();
        Assert.True(linha.Marcado);
        Assert.True(linha.CasamentoConfirmado);

        Assert.False(ConfirmacaoParProduto.Trocar(previa, linha, new ProdutoCesta { Id = 0, Nome = "Zero", Ativo = true }, Catalogo()));
        Assert.False(ConfirmacaoParProduto.Trocar(previa, linha, new ProdutoCesta { Id = -1, Nome = "Negativo", Ativo = true }, Catalogo()));
        Assert.False(ConfirmacaoParProduto.Trocar(previa, linha, new ProdutoCesta { Id = 1_000_000, Nome = "Ausente", Ativo = true }, Catalogo()));
        Assert.False(ConfirmacaoParProduto.Trocar(previa, linha, _inativo, Catalogo()));
        Assert.Equal(_arroz.Id, linha.ProdutoCestaId);
        Assert.True(linha.Marcado);
        Assert.True(linha.CasamentoConfirmado);

        Assert.True(ConfirmacaoParProduto.Trocar(previa, linha, _arroz, Catalogo()));
        Assert.Equal(_arroz.Id, linha.ProdutoCestaId);
        Assert.False(linha.Marcado);
        Assert.False(linha.CasamentoConfirmado);
        Assert.False(previa.PodeConfirmar);
        Assert.Throws<InvalidOperationException>(() => previa.ParaCompra());

        linha.Marcado = true;
        previa.ConfirmarMarcados();
        Assert.True(ConfirmacaoParProduto.Trocar(previa, linha, _feijao, Catalogo()));
        Assert.Equal(_feijao.Id, linha.ProdutoCestaId);
        Assert.Equal("Feijão", linha.Nome);
        Assert.False(linha.Marcado);
        Assert.False(linha.CasamentoConfirmado);
        Assert.False(previa.PodeConfirmar);
        var bloqueio = Assert.Throws<InvalidOperationException>(() => previa.ParaCompra());
        Assert.Equal(CasamentoComprovante.MensagemConfirmacaoObrigatoria, bloqueio.Message);
        Assert.Equal(0, await ContarComprasAsync());

        linha.Marcado = true;
        previa.ConfirmarMarcados();
        var compra = await _service.SalvarCompraAsync(Copiar(previa.ParaCompra()), _membroId);
        Assert.Equal(_feijao.Id, Assert.Single(compra.Itens).ProdutoCestaId);
    }

    [Fact]
    public async Task Desfazer_DesmarcaEVoltaNaoIdentificado()
    {
        var local = await _service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        var previa = Previa(local, Lido("ARROZ TIPO 1", _arroz.Id, true, 1m), _arroz, _feijao);
        var linha = Assert.Single(previa.Linhas);
        linha.Marcado = true;
        previa.ConfirmarMarcados();
        Assert.True(linha.Marcado);
        Assert.True(linha.CasamentoConfirmado);
        Assert.False(linha.PodeCadastrar);

        ConfirmacaoParProduto.Desfazer(linha);
        Assert.True(linha.NaoIdentificado);
        Assert.True(linha.PodeCadastrar);
        Assert.Null(linha.ProdutoCestaId);
        Assert.False(linha.Marcado);
        Assert.False(linha.CasamentoConfirmado);
        Assert.False(ConfirmacaoParProduto.PodeAjustar(linha));
        Assert.StartsWith(NormalizadorComprovante.PrefixoNaoIdentificado, linha.Rotulo, StringComparison.Ordinal);
        ConfirmacaoParProduto.Desfazer(linha);
        Assert.True(linha.NaoIdentificado);
        Assert.False(linha.Marcado);
        Assert.False(previa.PodeConfirmar);
        Assert.Throws<InvalidOperationException>(() => previa.ParaCompra());

        var formulario = previa.IniciarCadastro(linha);
        Assert.NotNull(formulario);
        Assert.Equal("ARROZ TIPO 1", formulario.Nome);
        Assert.Equal(0, await ContarComprasAsync());
        Assert.Equal(0, await ContarItensAsync());
    }

    [Fact]
    public void MarcarTodos_NaoMarcaPendente()
    {
        var local = new LocalCompra { Id = 1, Nome = "Mercado", Ativo = true };
        var previa = Previa(
            local,
            [
                Lido("ARROZ TIPO 1", _arroz.Id, true, 0.95m),
                Lido("FEIJAO", _feijao.Id, true, 0.2m),
                new ItemComprovanteLido("pacote", "Leite", true, 6m, null, null, null, null)
            ],
            _arroz,
            _feijao,
            _leite);
        var alta = Assert.Single(previa.Linhas, linha => linha.DescricaoLida == "ARROZ TIPO 1");
        var baixa = Assert.Single(previa.Linhas, linha => linha.DescricaoLida == "FEIJAO");
        var porNome = Assert.Single(previa.Linhas, linha => linha.DescricaoLida == "pacote");

        Assert.False(alta.Pendente);
        Assert.True(baixa.Pendente);
        Assert.True(porNome.Pendente);
        Assert.All(previa.Linhas, linha =>
        {
            Assert.False(linha.Marcado);
            Assert.False(linha.CasamentoConfirmado);
        });
        Assert.False(previa.PodeConfirmar);

        previa.MarcarNaoPendentes();
        Assert.True(alta.Marcado);
        Assert.False(alta.CasamentoConfirmado);
        Assert.False(baixa.Marcado);
        Assert.False(baixa.CasamentoConfirmado);
        Assert.False(porNome.Marcado);
        Assert.False(porNome.CasamentoConfirmado);
        Assert.False(previa.PodeConfirmar);

        baixa.Marcado = true;
        porNome.Marcado = true;
        Assert.True(previa.PodeConfirmar);
        previa.ConfirmarMarcados();
        Assert.All(previa.Linhas, linha => Assert.True(linha.CasamentoConfirmado));
        CasamentoComprovante.GarantirConfirmacao(previa.ItensComProduto());
    }

    [Theory]
    [InlineData(1_000_000)]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(null)]
    public void IdInvalidoOuNull_ViraNaoIdentificado(int? id)
    {
        var local = new LocalCompra { Id = 1, Nome = "Mercado " + id, Ativo = true };
        var previa = Previa(local, Lido("ARROZ TIPO 1", id, true, 0.99m), _arroz, _feijao);
        var linha = Assert.Single(previa.Linhas);
        Assert.True(linha.NaoIdentificado);
        Assert.Null(linha.ProdutoCestaId);
        Assert.False(linha.Marcado);
        Assert.False(ConfirmacaoParProduto.PodeAjustar(linha));
        Assert.False(previa.PodeConfirmar);
    }

    [Fact]
    public void Desmarcar_NaoUsaConfianca_ComentarioNaoConta()
    {
        var fonte = Ler("OrcaFacil.Core", "Cesta", "ConfirmacaoParProduto.cs");
        var previa = Ler("OrcaFacil.Core", "Llm", "PreviaComprovante.cs");
        Assert.True(TrocarDesmarca(fonte));
        Assert.True(DesfazerDesmarca(fonte));
        Assert.True(MarcarTodosPulaPendente(previa));

        var semMarca = fonte.Replace(
            """
                    previa.Classificar(linha, produto);
                    linha.Marcado = false;
            """,
            """
                    previa.Classificar(linha, produto);
                    // linha.Marcado = false;
            """,
            StringComparison.Ordinal);
        Assert.False(TrocarDesmarca(semMarca));
        Assert.True(DesfazerDesmarca(semMarca));

        var semDesfazer = fonte.Replace(
            """
                    linha.CasamentoConfirmado = false;
                    linha.Marcado = false;
            """,
            """
                    linha.CasamentoConfirmado = false;
                    // linha.Marcado = false;
            """,
            StringComparison.Ordinal);
        Assert.False(DesfazerDesmarca(semDesfazer));

        var marcaPendente = previa.Replace(
            "if (linha.ProdutoCestaId is > 0 && !linha.Pendente)",
            "if (linha.ProdutoCestaId is > 0)",
            StringComparison.Ordinal);
        Assert.False(MarcarTodosPulaPendente(marcaPendente));

        const string soComentario = """
            // !linha.Pendente
            /* linha.Marcado = false */
            public void MarcarNaoPendentes()
            {
                linha.Marcado = true;
            }
            public void ConfirmarMarcados()
            """;
        Assert.Contains("!linha.Pendente", soComentario, StringComparison.Ordinal);
        Assert.False(MarcarTodosPulaPendente(soComentario));
        Assert.DoesNotContain("!linha.Pendente", SemComentarios(soComentario), StringComparison.Ordinal);
    }

    [Fact]
    public void Previa_TrocarDesfazer_ComentarioNaoConta()
    {
        var dialog = Ler("OrcaFacil", "Components", "Pages", "Cesta", "PreviaComprovanteDialog.razor");
        var par = Ler("OrcaFacil", "Components", "Pages", "Cesta", "ParProdutoPrevia.razor");
        var item = Ler("OrcaFacil", "Components", "Pages", "Cesta", "ItemParPrevia.razor");
        var css = Ler("OrcaFacil", "Components", "Pages", "Cesta", "ParProdutoPrevia.razor.css")
            + Ler("OrcaFacil", "Components", "Pages", "Cesta", "ItemParPrevia.razor.css");
        Assert.True(DialogoUsaChecks(dialog));
        Assert.True(ComponenteSoTroca(par));
        Assert.True(ItemSoTroca(item));
        Assert.True(CssDoPar(css));

        var semTrocar = par.Replace(
            "ConfirmacaoParProduto.Trocar(Previa, linha, produto, Catalogo);",
            "// ConfirmacaoParProduto.Trocar(Previa, linha, produto, Catalogo);",
            StringComparison.Ordinal);
        Assert.False(ComponenteSoTroca(semTrocar));

        var semDesfazer = par.Replace(
            "ConfirmacaoParProduto.Desfazer(linha);",
            "// ConfirmacaoParProduto.Desfazer(linha);",
            StringComparison.Ordinal);
        Assert.False(ComponenteSoTroca(semDesfazer));

        var semBotao = item.Replace(">Trocar</button>", "<!-- >Trocar</button> -->", StringComparison.Ordinal);
        Assert.Contains(">Trocar</button>", semBotao, StringComparison.Ordinal);
        Assert.False(ItemSoTroca(semBotao));

        var semMarcar = dialog.Replace(
            "Previa.MarcarNaoPendentes()",
            "// Previa.MarcarNaoPendentes()",
            StringComparison.Ordinal);
        Assert.False(DialogoUsaChecks(semMarcar));

        var cssMutado = css.Replace("color: #1E2A33;", "/* color: #1E2A33; */", StringComparison.Ordinal);
        Assert.False(CssDoPar(cssMutado));
        Assert.False(CssDoPar(css.Replace(".of-par-botao:focus {", ".of-par-botao:focus-visible {", StringComparison.Ordinal)));
        Assert.False(CssDoPar(css + "\n.of-par:has(.of-par-botao) { color: #1E2A33; }\n"));
        Assert.False(CssDoPar(css.Replace("display: flex;", "display: flex;\n    gap: 8px;", StringComparison.Ordinal)));
    }

    [Fact]
    public void DestaqueEChip_CabemNaLista_ComentarioNaoConta()
    {
        var dialog = Ler("OrcaFacil", "Components", "Pages", "Cesta", "PreviaComprovanteDialog.razor");
        var lista = Ler("OrcaFacil", "Components", "Shared", "ItemList.razor");
        var css = Ler("OrcaFacil", "Components", "Shared", "ItemList.razor.css");
        Assert.True(DestaqueNaLista(dialog, lista));
        Assert.True(ChipCabeNaLista(css));

        var semDestacado = dialog.Replace(
            "Destacado=\"Destacado\"",
            "<!-- Destacado=\"Destacado\" -->",
            StringComparison.Ordinal);
        Assert.Contains("Destacado=\"Destacado\"", semDestacado, StringComparison.Ordinal);
        Assert.False(DestaqueNaLista(semDestacado, lista));

        var semItem = lista.Replace(
            "classe += \" of-lista-item-pendente\";",
            "// classe += \" of-lista-item-pendente\";",
            StringComparison.Ordinal);
        Assert.False(DestaqueNaLista(dialog, semItem));

        var semChip = lista.Replace(
            "return \"of-lista-chip-pendente\";",
            "// return \"of-lista-chip-pendente\";",
            StringComparison.Ordinal);
        Assert.False(DestaqueNaLista(dialog, semChip));

        var comentarioNowrap = css.Replace(
            "white-space: normal;",
            "white-space: normal;\n    /* white-space: nowrap; */",
            StringComparison.Ordinal);
        Assert.Contains("nowrap", comentarioNowrap, StringComparison.Ordinal);
        Assert.True(ChipCabeNaLista(comentarioNowrap));

        var comNowrap = css.Replace(
            "white-space: normal;",
            "white-space: nowrap;",
            StringComparison.Ordinal);
        Assert.False(ChipCabeNaLista(comNowrap));

        var semToque = css.Replace(
            """
            .of-lista-marca {
                flex: 0 0 auto;
                width: var(--of-touch);
                height: var(--of-touch);
            """,
            """
            .of-lista-marca {
                flex: 0 0 auto;
                /* width: var(--of-touch); */
                /* height: var(--of-touch); */
            """,
            StringComparison.Ordinal);
        Assert.False(ChipCabeNaLista(semToque));

        var semFundo = css.Replace(
            "background: #9B2C3A;",
            "/* background: #9B2C3A; */",
            StringComparison.Ordinal);
        Assert.False(ChipCabeNaLista(semFundo));
        Assert.False(ChipCabeNaLista(css.Replace(
            "background: #F59A9A;",
            "background: #1B1F27;",
            StringComparison.Ordinal)));
        Assert.False(ChipCabeNaLista(css.Replace(
            """
                background: #9B2C3A;
                color: #ffffff;
            """,
            """
                background: #9B2C3A;
                color: #9B2C3A;
            """,
            StringComparison.Ordinal)));
    }

    public void Dispose() => _factory.Dispose();

    private List<ProdutoCesta> Catalogo() => [_arroz, _feijao, _inativo];

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

    private static PreviaComprovante Previa(
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

    private static bool TrocarDesmarca(string fonte)
    {
        var trocar = Metodo(SemComentarios(fonte), "bool Trocar(");
        return trocar.Contains("linha.Marcado = false", StringComparison.Ordinal)
            && trocar.Contains("linha.CasamentoConfirmado = false", StringComparison.Ordinal)
            && !trocar.Contains("CasamentoConfirmado = true", StringComparison.Ordinal)
            && !trocar.Contains("Confianca", StringComparison.Ordinal)
            && !trocar.Contains("Pendente", StringComparison.Ordinal);
    }

    private static bool DesfazerDesmarca(string fonte)
    {
        var metodo = Metodo(SemComentarios(fonte), "void AplicarNaoIdentificado(");
        return metodo.Contains("linha.Marcado = false", StringComparison.Ordinal)
            && metodo.Contains("linha.CasamentoConfirmado = false", StringComparison.Ordinal);
    }

    private static bool MarcarTodosPulaPendente(string fonte)
    {
        var metodo = Metodo(SemComentarios(fonte), "void MarcarNaoPendentes()");
        return metodo.Contains("!linha.Pendente", StringComparison.Ordinal)
            && metodo.Contains("linha.Marcado = true", StringComparison.Ordinal);
    }

    private static bool DialogoUsaChecks(string fonte)
    {
        var limpo = SemComentarios(fonte);
        return limpo.Contains("PrimarioDesabilitado=\"@(!Previa.PodeConfirmar)\"", StringComparison.Ordinal)
            && limpo.Contains("Previa.ConfirmarMarcados()", StringComparison.Ordinal)
            && limpo.Contains("Previa.MarcarNaoPendentes()", StringComparison.Ordinal)
            && limpo.Contains("aria-label=\"Marcar todos os itens não pendentes\"", StringComparison.Ordinal)
            && limpo.Contains("linha.MarcadorCasamento", StringComparison.Ordinal)
            && limpo.Contains("<ParProdutoPrevia ", StringComparison.Ordinal)
            && !limpo.Contains("PodeGravarCompra", StringComparison.Ordinal)
            && !limpo.Contains(">Confirmar</button>", StringComparison.Ordinal);
    }

    private static bool ComponenteSoTroca(string fonte)
    {
        var limpo = SemComentarios(fonte);
        if (limpo.Contains(">Confirmar</button>", StringComparison.Ordinal))
            return false;
        if (limpo.Contains("MarcadorCasamento", StringComparison.Ordinal))
            return false;
        if (limpo.Contains("Confianca", StringComparison.Ordinal) || limpo.Contains("confiança", StringComparison.OrdinalIgnoreCase))
            return false;

        return limpo.Contains("<ItemParPrevia ", StringComparison.Ordinal)
            && limpo.Contains("ConfirmacaoParProduto.Trocar(Previa, linha, produto, Catalogo);", StringComparison.Ordinal)
            && limpo.Contains("ConfirmacaoParProduto.Desfazer(linha);", StringComparison.Ordinal);
    }

    private static bool ItemSoTroca(string fonte)
    {
        var limpo = SemComentarios(fonte);
        if (limpo.Contains(">Confirmar</button>", StringComparison.Ordinal))
            return false;
        if (limpo.Contains("MarcadorCasamento", StringComparison.Ordinal))
            return false;
        if (limpo.Contains("Confianca", StringComparison.Ordinal) || limpo.Contains("confiança", StringComparison.OrdinalIgnoreCase))
            return false;

        return limpo.Contains(">Trocar</button>", StringComparison.Ordinal)
            && limpo.Contains(">Desfazer</button>", StringComparison.Ordinal)
            && limpo.Contains("Linha.Nome", StringComparison.Ordinal);
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
        if (!limpo.Contains("min-height: var(--of-touch);", StringComparison.Ordinal))
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
            && Contraste("#E8EAF0", "#1B1F27") >= 4.5
            && Contraste("#9B2C3A", "#ffffff") >= 4.5
            && Contraste("#F59A9A", "#1B1F27") >= 4.5;
    }

    private static bool DestaqueNaLista(string dialog, string lista)
    {
        var dialogo = SemComentarios(dialog);
        var componente = SemComentarios(lista);
        return dialogo.Contains("Destacado=\"Destacado\"", StringComparison.Ordinal)
            && componente.Contains("of-lista-item-pendente", StringComparison.Ordinal)
            && componente.Contains("of-lista-chip-pendente", StringComparison.Ordinal);
    }

    private static bool ChipCabeNaLista(string css)
    {
        var limpo = SemComentarios(css);
        if (limpo.Contains(":focus-visible", StringComparison.Ordinal) || limpo.Contains(":has(", StringComparison.Ordinal))
            return false;
        if (limpo.Contains("gap:", StringComparison.Ordinal))
            return false;

        var marca = Regra(limpo, ".of-lista-marca");
        var chip = Regra(limpo, ".of-lista-chip");
        if (!marca.Contains("width: var(--of-touch);", StringComparison.Ordinal)
            || !marca.Contains("height: var(--of-touch);", StringComparison.Ordinal))
            return false;
        if (chip.Contains("nowrap", StringComparison.Ordinal))
            return false;

        var pendentes = Regras(limpo, ".of-lista-chip-pendente");
        var claro = pendentes.Find(bloco => bloco.Contains("background: #9B2C3A;", StringComparison.Ordinal));
        var escuro = pendentes.Find(bloco => bloco.Contains("background: #F59A9A;", StringComparison.Ordinal));
        if (claro is null || escuro is null)
            return false;

        var corClara = CorDeclarada(claro);
        var corEscura = CorDeclarada(escuro);
        return corClara is not null
            && corEscura is not null
            && Contraste("#9B2C3A", corClara) >= 4.5
            && Contraste("#F59A9A", corEscura) >= 4.5;
    }

    private static string? CorDeclarada(string bloco)
    {
        const string chave = "color:";
        var indice = bloco.IndexOf(chave, StringComparison.Ordinal);
        if (indice < 0)
            return null;

        var resto = bloco[(indice + chave.Length)..].Trim();
        var fim = resto.IndexOf(';');
        return fim < 0 ? null : resto[..fim].Trim();
    }

    private static string Regra(string css, string seletor)
    {
        var regras = Regras(css, seletor);
        return regras.Count == 0 ? string.Empty : regras[0];
    }

    private static List<string> Regras(string css, string seletor)
    {
        var lista = new List<string>();
        var inicio = 0;
        while (inicio < css.Length)
        {
            var indice = css.IndexOf(seletor, inicio, StringComparison.Ordinal);
            if (indice < 0)
                break;

            var depois = indice + seletor.Length;
            if (depois < css.Length && css[depois] is not (' ' or '{' or '\r' or '\n' or ','))
            {
                inicio = depois;
                continue;
            }

            var abre = css.IndexOf('{', indice);
            var fecha = abre < 0 ? -1 : css.IndexOf('}', abre);
            if (fecha < 0)
                break;

            lista.Add(css[indice..(fecha + 1)]);
            inicio = fecha + 1;
        }

        return lista;
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
