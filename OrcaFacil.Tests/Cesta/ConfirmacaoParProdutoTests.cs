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
/// O par da prévia só grava depois da confirmação. Comentário no fonte não conta.
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
        Assert.True(previa.PodeConfirmar);

        var pares = Pares(_arroz.Id, 0.95m, false, "ARROZ TIPO 1");
        var par = Assert.Single(pares.Linhas);
        Assert.Equal("Arroz", par.NomeExibido);
        Assert.Equal(_arroz.Id, par.ProdutoId);
        Assert.Equal("Confiança 95%", par.TextoConfianca);
        Assert.False(par.NaoIdentificado);
        Assert.False(par.DestacarRevisao);
        Assert.True(par.AguardandoConfirmacao);
        Assert.False(pares.PodeGravar);
        Assert.Equal(ConfirmacaoParProduto.MensagemParNaoConfirmado, pares.MensagemBloqueio);

        foreach (var (confianca, pendente) in new[] { (0.8m, false), (1m, false), (0.99m, true) })
        {
            var alto = Pares(_arroz.Id, confianca, pendente, "ARROZ TIPO 1");
            Assert.False(alto.PodeGravar);
            Assert.Equal(pendente, Assert.Single(alto.Linhas).DestacarRevisao);
            Assert.Equal(ConfirmacaoParProduto.ItemPar.FormatarConfianca(confianca), alto.Linhas[0].TextoConfianca);
            await AssertNaoGravaAsync(alto);
        }

        var dois = ConfirmacaoParProduto.Criar(
            [
                new ConfirmacaoParProduto.Entrada(_arroz.Id, 0.8m, false, "ARROZ TIPO 1"),
                new ConfirmacaoParProduto.Entrada(_feijao.Id, 1m, false, "FEIJAO")
            ],
            Catalogo());
        Assert.False(dois.PodeGravar);
        dois.Confirmar(0);
        Assert.False(dois.PodeGravar);
        await AssertNaoGravaAsync(dois);
        dois.Confirmar(1);
        Assert.True(dois.PodeGravar);
        dois.GarantirAntesDeGravar();

        await AssertNaoGravaAsync(pares);
        pares.Confirmar(0);
        Assert.False(par.AguardandoConfirmacao);
        Assert.Equal("Arroz", par.NomeExibido);
        Assert.Equal(_arroz.Id, par.ProdutoId);
        Assert.True(pares.PodeGravar);
        Assert.Null(pares.MensagemBloqueio);
        pares.GarantirAntesDeGravar();

        var compra = await _service.SalvarCompraAsync(Copiar(previa.ParaCompra()), _membroId);
        var item = Assert.Single(compra.Itens);
        Assert.Equal(_arroz.Id, item.ProdutoCestaId);
        Assert.Equal(10m, item.Preco);
        Assert.Equal(1, await ContarComprasAsync());
    }

    [Fact]
    public async Task SemPar_ContinuaNaoIdentificado()
    {
        var catalogo = Catalogo();
        var saida = CasamentoComprovante.Casar(Lido("SABONETE", null, true, 0.99m), catalogo);
        Assert.Null(saida.ProdutoId);
        Assert.True(saida.Pendente);

        var pares = ConfirmacaoParProduto.Criar(
            [new ConfirmacaoParProduto.Entrada(saida.ProdutoId, saida.Confianca, saida.Pendente, saida.DescricaoLida)],
            catalogo);
        var par = Assert.Single(pares.Linhas);
        Assert.True(par.NaoIdentificado);
        Assert.True(par.PodeCadastrar);
        Assert.False(par.AguardandoConfirmacao);
        Assert.False(par.DestacarRevisao);
        Assert.Null(par.ProdutoId);
        Assert.Equal(NormalizadorComprovante.PrefixoNaoIdentificado + "SABONETE", par.NomeExibido);
        Assert.DoesNotContain("Arroz", par.NomeExibido, StringComparison.Ordinal);
        Assert.True(pares.PodeGravar);
        Assert.Null(pares.MensagemBloqueio);
        pares.Confirmar(0);
        Assert.True(par.NaoIdentificado);
        Assert.Null(par.ProdutoId);

        var local = await _service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        var previa = Previa(local, Lido("SABONETE", null, true, 0.99m), _arroz);
        var linha = Assert.Single(previa.Linhas);
        Assert.True(linha.NaoIdentificado);
        Assert.True(linha.PodeCadastrar);
        Assert.Null(linha.ProdutoCestaId);
        Assert.StartsWith(NormalizadorComprovante.PrefixoNaoIdentificado, linha.Rotulo, StringComparison.Ordinal);
        Assert.False(previa.PodeConfirmar);
        Assert.Throws<InvalidOperationException>(() => previa.ParaCompra());
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
        var catalogo = Catalogo();
        var saida = CasamentoComprovante.Casar(Lido("ARROZ TIPO 1", id, true, 0.99m), catalogo);
        Assert.Null(saida.ProdutoId);

        var direto = ConfirmacaoParProduto.Criar(
            [new ConfirmacaoParProduto.Entrada(id, 0.99m, true, "ARROZ TIPO 1")],
            catalogo);
        var peloContrato = ConfirmacaoParProduto.Criar(
            [new ConfirmacaoParProduto.Entrada(saida.ProdutoId, saida.Confianca, saida.Pendente, saida.DescricaoLida)],
            catalogo);

        AssertNaoIdentificado(Assert.Single(direto.Linhas));
        AssertNaoIdentificado(Assert.Single(peloContrato.Linhas));
        Assert.True(direto.PodeGravar);
        Assert.True(peloContrato.PodeGravar);

        var local = await _service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado " + id });
        var previa = Previa(local, Lido("ARROZ TIPO 1", id, true, 0.99m), _arroz, _feijao);
        var linha = Assert.Single(previa.Linhas);
        Assert.True(linha.NaoIdentificado);
        Assert.Null(linha.ProdutoCestaId);
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
        var pares = Pares(_arroz.Id, 0.91m, false, "ARROZ TIPO 1");
        Assert.False(pares.PodeGravar);
        await AssertNaoGravaAsync(pares);

        Assert.False(pares.Trocar(0, 0));
        Assert.False(pares.Trocar(0, -1));
        Assert.False(pares.Trocar(0, 1_000_000));
        Assert.False(pares.Trocar(0, _inativo.Id));
        Assert.Equal(_arroz.Id, pares.Linhas[0].ProdutoId);
        Assert.False(pares.PodeGravar);
        Assert.Equal(0, await ContarComprasAsync());

        Assert.True(pares.Trocar(0, _feijao.Id));
        var par = pares.Linhas[0];
        Assert.Equal(_feijao.Id, par.ProdutoId);
        Assert.Equal("Feijão", par.NomeExibido);
        Assert.False(par.NaoIdentificado);
        Assert.False(par.AguardandoConfirmacao);
        Assert.Equal(ConfirmacaoParProduto.SituacaoPar.Trocado, par.Situacao);
        Assert.True(pares.PodeGravar);
        pares.GarantirAntesDeGravar();

        previa.Classificar(linha, _feijao);
        var compra = await _service.SalvarCompraAsync(Copiar(previa.ParaCompra()), _membroId);
        var item = Assert.Single(compra.Itens);
        Assert.Equal(_feijao.Id, item.ProdutoCestaId);
        Assert.NotEqual(_arroz.Id, item.ProdutoCestaId);

        var arrozId = Guid.NewGuid();
        var feijaoId = Guid.NewGuid();
        var ligacao = ConfirmacaoParProduto.Ligar(
            ConfirmacaoParProduto.Criar(
                [
                    new ConfirmacaoParProduto.Entrada(_arroz.Id, 0.9m, false, "ARROZ"),
                    new ConfirmacaoParProduto.Entrada(_feijao.Id, 0.9m, false, "ARROZ")
                ],
                Catalogo()).Linhas,
            [
                new ConfirmacaoParProduto.LinhaPreviaChave(arrozId, "ARROZ", _arroz.Id),
                new ConfirmacaoParProduto.LinhaPreviaChave(feijaoId, "ARROZ", _feijao.Id)
            ]);
        Assert.Equal(arrozId, ligacao[0]);
        Assert.Equal(feijaoId, ligacao[1]);
    }

    [Fact]
    public async Task Desfazer_VoltaNaoIdentificadoEMantemCadastrar()
    {
        var local = await _service.SalvarLocalAsync(new LocalCompraFormModel { Nome = "Mercado" });
        var previa = Previa(local, Lido("ARROZ TIPO 1", _arroz.Id, true, 1m), _arroz, _feijao);
        var linha = Assert.Single(previa.Linhas);
        Assert.False(linha.PodeCadastrar);

        var pares = Pares(_arroz.Id, 1m, true, "ARROZ TIPO 1");
        Assert.True(pares.Trocar(0, _feijao.Id));
        pares.Desfazer(0);
        var par = pares.Linhas[0];
        Assert.True(par.NaoIdentificado);
        Assert.True(par.PodeCadastrar);
        Assert.Null(par.ProdutoId);
        Assert.Equal(ConfirmacaoParProduto.SituacaoPar.Desfeito, par.Situacao);
        Assert.Equal(NormalizadorComprovante.PrefixoNaoIdentificado + "ARROZ TIPO 1", par.NomeExibido);
        Assert.False(par.AguardandoConfirmacao);
        Assert.True(pares.PodeGravar);
        pares.Desfazer(0);
        Assert.True(par.NaoIdentificado);

        var ligacao = ConfirmacaoParProduto.Ligar(
            Pares(_arroz.Id, 1m, false, "ARROZ TIPO 1").Linhas,
            [new ConfirmacaoParProduto.LinhaPreviaChave(linha.Id, linha.DescricaoLida, linha.ProdutoCestaId)]);
        Assert.Equal(linha.Id, Assert.Single(ligacao));

        ConfirmacaoParProduto.AplicarNaoIdentificado(linha);
        Assert.True(linha.NaoIdentificado);
        Assert.True(linha.PodeCadastrar);
        Assert.Null(linha.ProdutoCestaId);
        Assert.False(linha.NovoSeraCriado);
        Assert.StartsWith(NormalizadorComprovante.PrefixoNaoIdentificado, linha.Rotulo, StringComparison.Ordinal);
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
        var pendente = Pares(_arroz.Id, 0.2m, true, "ARROZ TIPO 1");
        Assert.True(Assert.Single(pendente.Linhas).DestacarRevisao);
        Assert.True(Assert.Single(pendente.Linhas).Pendente);
        Assert.False(pendente.PodeGravar);
        await AssertNaoGravaAsync(pendente);

        var semDestaque = Pares(_arroz.Id, 0.95m, false, "ARROZ TIPO 1");
        Assert.False(Assert.Single(semDestaque.Linhas).Pendente);
        Assert.False(Assert.Single(semDestaque.Linhas).DestacarRevisao);
        Assert.False(semDestaque.PodeGravar);
        await AssertNaoGravaAsync(semDestaque);

        pendente.Confirmar(0);
        Assert.False(pendente.Linhas[0].DestacarRevisao);
        Assert.True(pendente.Linhas[0].Pendente);
        Assert.True(pendente.PodeGravar);
        pendente.GarantirAntesDeGravar();
    }

    [Fact]
    public void PodeGravar_NaoUsaConfiancaNemPendente_ComentarioNaoConta()
    {
        var fonte = Ler("OrcaFacil.Core", "Cesta", "ConfirmacaoParProduto.cs");
        Assert.True(PortaoIgnoraConfianca(fonte));

        var mutado = fonte.Replace(
            "public bool PodeGravar => _linhas.All(linha => !linha.AguardandoConfirmacao);",
            "public bool PodeGravar => _linhas.All(linha => !linha.AguardandoConfirmacao || linha.Confianca >= 0.8m || !linha.Pendente);",
            StringComparison.Ordinal);
        Assert.False(PortaoIgnoraConfianca(mutado));

        const string soComentario = """
            // public bool PodeGravar => _linhas.All(linha => !linha.AguardandoConfirmacao);
            /* AguardandoConfirmacao */
            public bool PodeGravar => linha.Confianca >= 0.8m || !linha.Pendente;
            public string? MensagemBloqueio
            """;
        Assert.Contains("AguardandoConfirmacao", soComentario, StringComparison.Ordinal);
        Assert.False(PortaoIgnoraConfianca(soComentario));
        Assert.DoesNotContain("AguardandoConfirmacao", SemComentarios(soComentario), StringComparison.Ordinal);
    }

    [Fact]
    public void Previa_BotoesEBloqueio_ComentarioNaoConta()
    {
        var previa = Ler("OrcaFacil", "Components", "Pages", "Cesta", "PreviaComprovanteDialog.razor");
        var pagina = Ler("OrcaFacil", "Components", "Pages", "Cesta", "CestaPage.razor");
        var css = Ler("OrcaFacil", "Components", "Pages", "Cesta", "PreviaComprovanteDialog.razor.css");
        Assert.True(PreviaProtegeGravacao(previa));
        Assert.True(PaginaEntregaSaidas(pagina));
        Assert.True(CssDoPar(css));

        var metodo = Metodo(previa, "void Confirmar()");
        var guarda = "if (!_pares.PodeGravar)\n            return;";
        var mutado = previa.Replace(guarda, "/* if (!_pares.PodeGravar) return; */", StringComparison.Ordinal);
        Assert.False(PreviaProtegeGravacao(mutado));
        Assert.Contains("PodeGravar", mutado, StringComparison.Ordinal);

        var semGarantir = previa.Replace(
            "_pares.GarantirAntesDeGravar();",
            "// _pares.GarantirAntesDeGravar();",
            StringComparison.Ordinal);
        Assert.False(PreviaProtegeGravacao(semGarantir));

        var semBotao = previa.Replace(">Confirmar</button>", "<!-- >Confirmar</button> -->", StringComparison.Ordinal);
        Assert.Contains(">Confirmar</button>", semBotao, StringComparison.Ordinal);
        Assert.False(PreviaProtegeGravacao(semBotao));

        var semTrocar = previa.Replace(
            "_pares.Trocar(indice, produto.Id)",
            "/* _pares.Trocar(indice, produto.Id) */",
            StringComparison.Ordinal);
        Assert.False(PreviaProtegeGravacao(semTrocar));

        var semDesfazer = previa.Replace(
            "_pares.Desfazer(indice);",
            "// _pares.Desfazer(indice);",
            StringComparison.Ordinal);
        Assert.False(PreviaProtegeGravacao(semDesfazer));

        var semMensagem = previa.Replace(
            "<p class=\"of-par-alerta of-previa-erro\" role=\"alert\">@_pares.MensagemBloqueio</p>",
            "<!-- <p class=\"of-par-alerta of-previa-erro\" role=\"alert\">@_pares.MensagemBloqueio</p> -->",
            StringComparison.Ordinal);
        Assert.False(PreviaProtegeGravacao(semMensagem));

        var cssMutado = css.Replace("color: #1E2A33;", "/* color: #1E2A33; */", StringComparison.Ordinal);
        Assert.Contains("color: #1E2A33;", cssMutado, StringComparison.Ordinal);
        Assert.False(CssDoPar(cssMutado));
        Assert.False(CssDoPar(css.Replace(".of-par-botao:focus {", ".of-par-botao:focus-visible {", StringComparison.Ordinal)));
        Assert.False(CssDoPar(css + "\n.of-par:has(.of-par-botao) { color: #1E2A33; }\n"));
        Assert.False(CssDoPar(css.Replace("display: flex;", "display: flex;\n    gap: 8px;", StringComparison.Ordinal)));

        const string soComentario = """
            @* >Confirmar</button> *@
            // _pares.Confirmar(indice)
            /* _pares.Trocar(indice, produto.Id) */
            <!-- >Desfazer</button> -->
            <!-- <p class="of-par-alerta of-previa-erro" role="alert">@_pares.MensagemBloqueio</p> -->
            """;
        Assert.Contains(">Confirmar</button>", soComentario, StringComparison.Ordinal);
        Assert.False(PreviaProtegeGravacao(soComentario));
    }

    public void Dispose() => _factory.Dispose();

    private ConfirmacaoParProduto Pares(int? produtoId, decimal confianca, bool pendente, string descricao) =>
        ConfirmacaoParProduto.Criar(
            [new ConfirmacaoParProduto.Entrada(produtoId, confianca, pendente, descricao)],
            Catalogo());

    private List<ProdutoCesta> Catalogo() => [_arroz, _feijao, _inativo];

    private async Task AssertNaoGravaAsync(ConfirmacaoParProduto pares)
    {
        var compras = await ContarComprasAsync();
        var itens = await ContarItensAsync();
        var excecao = Assert.Throws<InvalidOperationException>(() => pares.GarantirAntesDeGravar());
        Assert.Equal(ConfirmacaoParProduto.MensagemParNaoConfirmado, excecao.Message);
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

    private static void AssertNaoIdentificado(ConfirmacaoParProduto.ItemPar par)
    {
        Assert.True(par.NaoIdentificado);
        Assert.True(par.PodeCadastrar);
        Assert.Null(par.ProdutoId);
        Assert.False(par.AguardandoConfirmacao);
        Assert.StartsWith(NormalizadorComprovante.PrefixoNaoIdentificado, par.NomeExibido, StringComparison.Ordinal);
        Assert.Contains("ARROZ TIPO 1", par.NomeExibido, StringComparison.Ordinal);
        Assert.DoesNotContain("Arroz", par.NomeExibido, StringComparison.Ordinal);
    }

    private PreviaComprovante Previa(
        LocalCompra local,
        ItemComprovanteLido item,
        params ProdutoCesta[] produtos) =>
        PreviaComprovante.Criar(
            NormalizadorComprovante.Normalizar(
                new ComprovanteLido(local.Nome, new DateOnly(2026, 10, 18), null, true, [item]),
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
        Itens = CompraCestaItens.CopiarItens(modelo)
    };

    private static bool PortaoIgnoraConfianca(string fonte)
    {
        var portao = Membro(SemComentarios(fonte), "public bool PodeGravar =>", "public string? MensagemBloqueio");
        return portao.Contains("AguardandoConfirmacao", StringComparison.Ordinal)
            && !portao.Contains("Confianca", StringComparison.Ordinal)
            && !portao.Contains("Pendente", StringComparison.Ordinal);
    }

    private static bool PreviaProtegeGravacao(string fonte)
    {
        var limpo = SemComentarios(fonte);
        if (!limpo.Contains("PrimarioDesabilitado=\"@(!PodeGravarCompra)\"", StringComparison.Ordinal))
            return false;
        if (!limpo.Contains("Previa.PodeConfirmar && _pares.PodeGravar", StringComparison.Ordinal))
            return false;
        if (!limpo.Contains("<p class=\"of-par-alerta of-previa-erro\" role=\"alert\">@_pares.MensagemBloqueio</p>", StringComparison.Ordinal))
            return false;
        if (!limpo.Contains(">Confirmar</button>", StringComparison.Ordinal))
            return false;
        if (!limpo.Contains(">Trocar</button>", StringComparison.Ordinal))
            return false;
        if (!limpo.Contains(">Desfazer</button>", StringComparison.Ordinal))
            return false;
        if (!limpo.Contains("_pares.Confirmar(indice)", StringComparison.Ordinal))
            return false;
        if (!limpo.Contains("_pares.Trocar(indice, produto.Id)", StringComparison.Ordinal))
            return false;
        if (!limpo.Contains("_pares.Desfazer(indice)", StringComparison.Ordinal))
            return false;
        if (!limpo.Contains("ConfirmacaoParProduto.Criar(", StringComparison.Ordinal))
            return false;
        if (!limpo.Contains(">Cadastrar</MudButton>", StringComparison.Ordinal))
            return false;
        if (!limpo.Contains("par.TextoConfianca", StringComparison.Ordinal))
            return false;
        if (!limpo.Contains("par.DestacarRevisao", StringComparison.Ordinal))
            return false;

        var confirmar = Metodo(limpo, "void Confirmar()");
        var bloqueio = confirmar.IndexOf("if (!_pares.PodeGravar)", StringComparison.Ordinal);
        var retorno = bloqueio < 0 ? -1 : confirmar.IndexOf("return", bloqueio, StringComparison.Ordinal);
        var garantir = confirmar.IndexOf("_pares.GarantirAntesDeGravar()", StringComparison.Ordinal);
        var compra = confirmar.IndexOf("ParaCompra()", StringComparison.Ordinal);
        var fechar = confirmar.IndexOf("MudDialog.Close", StringComparison.Ordinal);
        if (bloqueio < 0 || retorno <= bloqueio || garantir <= retorno || compra <= garantir || fechar <= garantir)
            return false;

        var gravar = Membro(limpo, "bool PodeGravarCompra", "private void Cancelar()");
        return !gravar.Contains("Confianca", StringComparison.Ordinal)
            && !gravar.Contains("Pendente", StringComparison.Ordinal);
    }

    private static bool PaginaEntregaSaidas(string fonte)
    {
        var limpo = SemComentarios(fonte);
        return limpo.Contains("CasamentoComprovante.Casar(resultado.Comprovante, _produtos)", StringComparison.Ordinal)
            && limpo.Contains("{ x => x.Saidas, _saidas }", StringComparison.Ordinal);
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
