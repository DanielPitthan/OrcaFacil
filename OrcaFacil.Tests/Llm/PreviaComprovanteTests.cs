using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Llm;

namespace OrcaFacil.Tests.Llm;

public class PreviaComprovanteTests
{
    [Fact]
    public void SegundaTentativa_Unico_MostraNomeDoCatalogoEtiquetaETextoLido()
    {
        var catalogo = new List<ProdutoCesta> { Produto(1, "Arroz", 2, "Alimentos") };
        var previa = Previa(
            catalogo,
            Item("arroz", "ARROZ T1", reconhecido: true, 6m, quantidade: "2", unidade: "kg"));

        var linha = Assert.Single(previa.Linhas);
        Assert.True(linha.CasadoPelaDescricao);
        Assert.False(linha.NomeNovo);
        Assert.False(linha.NaoIdentificado);
        Assert.Equal("Arroz", linha.Nome);
        Assert.Equal("Arroz", linha.NomeGravado);
        Assert.Equal("arroz", linha.DescricaoLida);
        Assert.Equal("Arroz · casado pela descrição · arroz", linha.Titulo);
        Assert.Equal("Alimentos · 2 kg", linha.Subtitulo);
        Assert.Equal(1, linha.ProdutoCestaId);
        Assert.True(previa.PodeConfirmar);
        Assert.Null(previa.MensagemBloqueio);

        var compra = previa.ParaCompra();
        var item = Assert.Single(compra.Itens);
        Assert.Equal(1, item.ProdutoCestaId);
        Assert.Equal(6m, item.Preco);
        Assert.DoesNotContain("casado pela descrição", linha.NomeGravado, StringComparison.Ordinal);
    }

    [Fact]
    public void SegundaTentativa_DescricaoAmbigua_MostraNovoProdutoComTextoLido()
    {
        var catalogo = new List<ProdutoCesta>
        {
            Produto(1, "Açúcar", 2, "Alimentos"),
            Produto(2, "AÇÚCAR", 3, "Mercearia")
        };
        var previa = Previa(catalogo, Item("açúcar", "Doce", reconhecido: true, 2m));
        var linha = Assert.Single(previa.Linhas);

        Assert.True(linha.Ambiguo);
        Assert.False(linha.NomeNovo);
        Assert.False(linha.CasadoPelaDescricao);
        Assert.Null(linha.ProdutoCestaId);
        Assert.Equal("açúcar", linha.Nome);
        Assert.Equal("açúcar · ambíguo", linha.Titulo);
        Assert.Equal(new[] { 1, 2 }, linha.CandidatosIds);
        Assert.True(linha.PodeCadastrar);
        Assert.Equal("açúcar", linha.DescricaoLida);
        Assert.False(previa.PodeConfirmar);
        Assert.Equal(PreviaComprovante.MensagemSemProdutoOuCategoria, previa.MensagemBloqueio);
        Assert.Throws<InvalidOperationException>(() => previa.ParaCompra());
    }

    [Fact]
    public void NaoIdentificados_BloqueiamAteTodosTeremProdutoECategoria()
    {
        var catalogo = new List<ProdutoCesta>
        {
            Produto(1, "Arroz", 2, "Alimentos"),
            Produto(4, "Feijão", 2, "Alimentos")
        };
        var previa = Previa(
            catalogo,
            Item("ARROZ", "Arroz", reconhecido: true, 8m),
            Item("BISC 12345", "", reconhecido: false, 4m),
            Item("SAB LIQ", "", reconhecido: false, 3m));

        Assert.Equal(2, previa.NaoIdentificados);
        Assert.Equal("2 não identificados", previa.TextoNaoIdentificados);
        Assert.False(previa.PodeConfirmar);
        Assert.Equal(PreviaComprovante.MensagemSemProdutoOuCategoria, previa.MensagemBloqueio);

        var biscoito = Assert.Single(previa.Linhas, linha => linha.DescricaoLida == "BISC 12345");
        Assert.Equal("Não identificado: BISC 12345", biscoito.Titulo);
        Assert.Equal("BISC 12345", biscoito.NomeGravado);
        Assert.NotEqual(biscoito.Rotulo, biscoito.NomeGravado);
        Assert.False(biscoito.NomeGravado.StartsWith(NormalizadorComprovante.PrefixoNaoIdentificado, StringComparison.Ordinal));

        previa.Classificar(biscoito, catalogo[1]);
        Assert.Equal(1, previa.NaoIdentificados);
        Assert.Equal("1 não identificado", previa.TextoNaoIdentificados);
        Assert.Equal("Feijão", biscoito.NomeGravado);
        Assert.Equal("Alimentos", biscoito.Subtitulo);
        Assert.False(biscoito.NaoIdentificado);
        Assert.False(previa.PodeConfirmar);

        var sabao = Assert.Single(previa.Linhas, linha => linha.NaoIdentificado);
        previa.Classificar(sabao, Produto(9, "Sabão", 3, "Limpeza"));
        Assert.Equal(0, previa.NaoIdentificados);
        Assert.Equal("0 não identificados", previa.TextoNaoIdentificados);
        Assert.True(previa.PodeConfirmar);

        var compra = previa.ParaCompra();
        Assert.Equal([1, 4, 9], compra.Itens.Select(item => item.ProdutoCestaId).ToArray());
        Assert.Equal([8m, 4m, 3m], compra.Itens.Select(item => item.Preco).ToArray());
        Assert.All(previa.Linhas, linha =>
            Assert.DoesNotContain(NormalizadorComprovante.PrefixoNaoIdentificado, linha.NomeGravado, StringComparison.Ordinal));
    }

    [Fact]
    public void ParaCompra_CasaLocalSemCaixa_EDeixaPagamentoComOUsuario()
    {
        var catalogo = new List<ProdutoCesta> { Produto(1, "Arroz", 2, "Alimentos") };
        var locais = new List<LocalCompra>
        {
            new() { Id = 7, Nome = "Boa Supermercado", Ativo = true },
            new() { Id = 8, Nome = "boa supermercado", Ativo = false },
            new() { Id = 9, Nome = "Outro", Ativo = true }
        };
        var previa = PreviaComprovante.Criar(
            NormalizadorComprovante.Normalizar(Cupom(
                [Item("ARROZ", "Arroz", reconhecido: true, 8m)],
                total: 8m,
                loja: "  boa supermercado  ",
                data: new DateOnly(2026, 10, 18)), catalogo),
            locais,
            catalogo,
            new DateTime(2026, 10, 1));

        Assert.Equal(7, previa.LocalCompraId);
        Assert.Equal("Boa Supermercado", previa.LocalCasadoNome);
        Assert.Equal("Local: Boa Supermercado", previa.TextoLocal);
        Assert.Equal(new DateTime(2026, 10, 18), previa.Data);

        var compra = previa.ParaCompra();
        Assert.Equal(7, compra.LocalCompraId);
        Assert.Equal(new DateTime(2026, 10, 18), compra.Data);
        Assert.Equal(0m, compra.ValorBeneficio);
        Assert.Equal(0m, compra.ValorDinheiro);
        Assert.Equal(0m, compra.ValorCartaoCredito);
        Assert.Null(compra.CartaoBeneficioId);
        Assert.Null(compra.CartaoCreditoId);
        Assert.Null(compra.TipoCartaoBeneficio);
        Assert.Null(compra.Id);

        var semLoja = PreviaComprovante.Criar(
            NormalizadorComprovante.Normalizar(
                Cupom([Item("ARROZ", "Arroz", reconhecido: true, 8m)], loja: "Mercado que não existe"),
                catalogo),
            locais,
            catalogo,
            new DateTime(2026, 9, 3));
        Assert.Null(semLoja.LocalCompraId);
        Assert.Equal(new DateTime(2026, 9, 3), semLoja.Data);
        Assert.Equal(0, semLoja.ParaCompra().LocalCompraId);

        var repetido = PreviaComprovante.CasarLocal("Mercado", [
            new LocalCompra { Id = 1, Nome = "Mercado", Ativo = true },
            new LocalCompra { Id = 2, Nome = "mercado", Ativo = true }
        ]);
        Assert.Null(repetido.Id);
    }

    [Fact]
    public void Cadastrar_CancelarNaoCria_ConfirmarCasaSoALinhaPendente()
    {
        var previa = Previa([], Item("BISC 12345", "", reconhecido: false, 4m), Item("SAB LIQ", "", reconhecido: false, 3m));
        var biscoito = previa.Linhas[0];
        var sabao = previa.Linhas[1];
        var nomeAntes = biscoito.Nome;
        var rotuloAntes = biscoito.Rotulo;

        var formulario = previa.IniciarCadastro(biscoito);
        Assert.NotNull(formulario);
        Assert.True(previa.CadastroPendente);
        Assert.Null(formulario.Id);
        Assert.Equal("BISC 12345", formulario.Nome);
        Assert.Equal(nomeAntes, biscoito.Nome);
        Assert.Equal(rotuloAntes, biscoito.Rotulo);
        Assert.Null(biscoito.ProdutoCestaId);
        Assert.True(biscoito.NaoIdentificado);
        Assert.NotEqual(biscoito.Rotulo, formulario.Nome);
        Assert.DoesNotContain(NormalizadorComprovante.PrefixoNaoIdentificado, formulario.Nome, StringComparison.Ordinal);

        previa.CancelarCadastro();
        Assert.False(previa.CadastroPendente);
        Assert.Null(biscoito.ProdutoCestaId);
        Assert.True(biscoito.NaoIdentificado);
        Assert.Equal(nomeAntes, biscoito.NomeGravado);
        Assert.Equal(2, previa.NaoIdentificados);
        Assert.Throws<InvalidOperationException>(() => previa.ConfirmarCadastro(
            new ProdutoCestaFormModel { Nome = "Biscoito", CategoriaId = 3 }, 4m));

        formulario = previa.IniciarCadastro(biscoito);
        Assert.NotNull(formulario);
        formulario.Nome = "Biscoito cream cracker";
        formulario.CategoriaId = 3;
        previa.ConfirmarCadastro(formulario, 4.5m, "Limpeza");

        Assert.False(previa.CadastroPendente);
        Assert.True(biscoito.NovoSeraCriado);
        Assert.True(biscoito.ProdutoCestaId < 0);
        Assert.Equal(3, biscoito.CategoriaId);
        Assert.Equal("Limpeza", biscoito.Subtitulo);
        Assert.Equal(4.5m, biscoito.Preco);
        Assert.Equal("Biscoito cream cracker · novo, será criado ao confirmar · BISC 12345", biscoito.Titulo);
        Assert.Equal("Biscoito cream cracker", biscoito.NomeGravado);
        Assert.False(biscoito.NaoIdentificado);
        Assert.False(biscoito.NomeGravado.StartsWith(NormalizadorComprovante.PrefixoNaoIdentificado, StringComparison.Ordinal));
        Assert.DoesNotContain("novo, será criado ao confirmar", biscoito.NomeGravado, StringComparison.Ordinal);
        Assert.True(sabao.NaoIdentificado);
        Assert.Null(sabao.ProdutoCestaId);
        Assert.Equal(1, previa.NaoIdentificados);
        Assert.False(previa.PodeConfirmar);
    }

    [Fact]
    public void NomeParaCadastro_TiraPrefixoECortaCombinanteESurrogate()
    {
        const string marca = "\u0301";
        var combinante = new string('A', 79) + "e" + marca;
        var comPrefixo = NormalizadorComprovante.PrefixoNaoIdentificado + combinante;
        var previa = Previa([], Item(comPrefixo, "", reconhecido: false, 1m));
        var linha = Assert.Single(previa.Linhas);

        Assert.StartsWith(NormalizadorComprovante.PrefixoNaoIdentificado, linha.Rotulo, StringComparison.Ordinal);
        var formulario = previa.IniciarCadastro(linha);
        Assert.NotNull(formulario);
        Assert.Equal(NormalizadorComprovante.Cortar(combinante), formulario.Nome);
        Assert.Equal(new string('A', 79), formulario.Nome);
        Assert.DoesNotContain("e" + marca, formulario.Nome, StringComparison.Ordinal);
        Assert.DoesNotContain(NormalizadorComprovante.PrefixoNaoIdentificado, formulario.Nome, StringComparison.Ordinal);
        Assert.NotEqual(linha.Rotulo, formulario.Nome);

        var emoji = new string('C', 79) + "\U0001F600";
        var cortado = PreviaComprovante.SemPrefixoECortado(emoji);
        Assert.Equal(NormalizadorComprovante.Cortar(emoji), cortado);
        Assert.Equal(new string('C', 79), cortado);
        Assert.DoesNotContain("\uD83D", cortado, StringComparison.Ordinal);

        previa.CancelarCadastro();
        Assert.Null(linha.ProdutoCestaId);
        Assert.True(linha.NaoIdentificado);
    }

    [Fact]
    public void RemoverPrecoEDuplicata_AtualizamOEstado()
    {
        var arroz = Produto(1, "Arroz", 2, "Alimentos");
        var previa = Previa(
            [arroz],
            Item("ARROZ", "Arroz", reconhecido: true, 8m),
            Item("BISC", "", reconhecido: false, 0m));

        Assert.Equal(8m, previa.SomaLinhas);
        Assert.False(previa.PodeConfirmar);

        previa.Remover(previa.Linhas[1]);
        Assert.Equal(0, previa.NaoIdentificados);
        Assert.True(previa.PodeConfirmar);
        Assert.Equal(8m, Assert.Single(previa.ParaCompra().Itens).Preco);

        var outra = Previa(
            [arroz, Produto(2, "Feijão", 2, "Alimentos")],
            Item("ARROZ", "Arroz", reconhecido: true, 8m),
            Item("FEIJAO", "Feijão", reconhecido: true, 5m));
        previa = outra;
        previa.Classificar(previa.Linhas[1], arroz);
        Assert.Equal(PreviaComprovante.MensagemProdutoRepetido, previa.MensagemBloqueio);

        previa.AlterarPreco(previa.Linhas[0], 0m);
        Assert.Equal(PreviaComprovante.MensagemPreco, previa.MensagemBloqueio);
        previa.AlterarPreco(previa.Linhas[0], 3.005m);
        Assert.Equal(3.01m, previa.Linhas[0].Preco);
        Assert.Equal(PreviaComprovante.MensagemProdutoRepetido, previa.MensagemBloqueio);

        var inativo = Produto(6, "Óleo", 2, "Alimentos", ativo: false);
        var bloqueado = Previa([inativo], Item("OLEO", "Óleo", reconhecido: true, 9m));
        Assert.Equal(NormalizadorComprovante.MotivoProdutoInativo, bloqueado.MensagemBloqueio);
        Assert.False(bloqueado.PodeConfirmar);
    }

    [Fact]
    public void TotalDivergente_FicaNaPrevia_ELeituraIncertaNaoBloqueiaSozinha()
    {
        var catalogo = new List<ProdutoCesta> { Produto(1, "Arroz", 2, "Alimentos") };
        var previa = PreviaComprovante.Criar(
            NormalizadorComprovante.Normalizar(
                Cupom([Item("ARROZ", "Arroz", reconhecido: true, 10m)], total: 9m, legivel: false),
                catalogo),
            [],
            catalogo,
            new DateTime(2026, 10, 1));

        Assert.Equal(9m, previa.TotalCupom);
        Assert.Equal(10m, previa.SomaLinhas);
        Assert.True(previa.TotalDivergente);
        Assert.False(previa.Legivel);
        Assert.True(previa.PodeConfirmar);
        Assert.Equal("PreviaComprovante", previa.ToString());
        Assert.Equal("LinhaPreviaComprovante", previa.Linhas[0].ToString());
    }

    [Fact]
    public void PrimeiraTentativa_NoCatalogo_NaoGanhaEtiquetaDeDescricao()
    {
        var catalogo = new List<ProdutoCesta> { Produto(1, "Arroz", 2, "Alimentos") };
        var linha = Assert.Single(Previa(catalogo, Item("ARROZ T1", "Arroz", reconhecido: true, 5m)).Linhas);

        Assert.False(linha.CasadoPelaDescricao);
        Assert.Equal("Arroz", linha.Titulo);
        Assert.Equal("Arroz", linha.NomeGravado);
    }

    [Fact]
    public void DescricaoUnica_MostraCasadoMesmoSemProdutoDeduzido()
    {
        var catalogo = new List<ProdutoCesta> { Produto(1, "Arroz", 2, "Alimentos") };
        var linha = Assert.Single(Previa(catalogo, Item("  arroz ", "", reconhecido: false, 8m)).Linhas);

        Assert.True(linha.CasadoPelaDescricao);
        Assert.Equal("Arroz", linha.NomeGravado);
        Assert.Equal("Arroz · casado pela descrição · arroz", linha.Titulo);
    }

    [Fact]
    public void SegundoInicioDeCadastro_EIgnorado_ENaoCriaProduto()
    {
        var previa = Previa([], Item("BISC", "", reconhecido: false, 4m), Item("SAB", "", reconhecido: false, 3m));
        Assert.NotNull(previa.IniciarCadastro(previa.Linhas[0]));
        Assert.Null(previa.IniciarCadastro(previa.Linhas[1]));
        Assert.True(previa.CadastroPendente);
        Assert.True(previa.Linhas[1].NaoIdentificado);
        Assert.Null(previa.Linhas[1].ProdutoCestaId);
        Assert.False(previa.Linhas[1].NovoSeraCriado);

        previa.CancelarCadastro();
        Assert.Null(previa.Linhas[0].ProdutoCestaId);
        Assert.False(previa.Linhas[0].NovoSeraCriado);
    }

    [Fact]
    public void PrefixoSo_ViraNomeVazio_EGrafemaEnormeNaoEstoura()
    {
        Assert.Equal(string.Empty, PreviaComprovante.SemPrefixoECortado("Não identificado: "));
        Assert.Equal(string.Empty, PreviaComprovante.SemPrefixoECortado(NormalizadorComprovante.PrefixoNaoIdentificado));
        Assert.Equal("açúcar", PreviaComprovante.SemPrefixoECortado("Não identificado:  açúcar"));

        var enorme = "e" + new string('\u0301', 80);
        var previa = Previa([], Item(enorme, "", reconhecido: false, 1m));
        var formulario = previa.IniciarCadastro(Assert.Single(previa.Linhas));
        Assert.NotNull(formulario);
        Assert.Equal(string.Empty, formulario.Nome);
    }

    [Fact]
    public void Cadastrar_PreencheComADescricaoLida()
    {
        var previa = Previa([], Item("açúcar", "Doce", reconhecido: true, 2m));
        var linha = Assert.Single(previa.Linhas);
        Assert.True(linha.NomeNovo);
        Assert.Equal("Doce · novo produto · açúcar", linha.Titulo);

        var formulario = previa.IniciarCadastro(linha);
        Assert.NotNull(formulario);
        Assert.Equal("açúcar", formulario.Nome);
        Assert.Equal(string.Empty, linha.ProdutoCestaId?.ToString() ?? string.Empty);
        Assert.True(linha.NomeNovo);
    }

    [Fact]
    public void DoisCadastrosComMesmoNomeNormalizado_UsamUmProdutoEBloqueiam()
    {
        var previa = Previa([], Item("açúcar", "", reconhecido: false, 2m), Item("acucar", "", reconhecido: false, 3m));
        var primeiro = previa.IniciarCadastro(previa.Linhas[0]);
        Assert.NotNull(primeiro);
        primeiro.Nome = "Açúcar";
        primeiro.CategoriaId = 2;
        previa.ConfirmarCadastro(primeiro, 2.5m, "Alimentos");

        var segundo = previa.IniciarCadastro(previa.Linhas[1]);
        Assert.NotNull(segundo);
        segundo.Nome = "acucar";
        segundo.CategoriaId = 2;
        previa.ConfirmarCadastro(segundo, 3m, "Alimentos");

        Assert.Equal(previa.Linhas[0].ProdutoCestaId, previa.Linhas[1].ProdutoCestaId);
        Assert.Equal("Açúcar", previa.Linhas[0].NomeGravado);
        Assert.Equal(2.5m, previa.Linhas[0].Preco);
        Assert.Equal(PreviaComprovante.MensagemProdutoRepetido, previa.MensagemBloqueio);
        Assert.Throws<InvalidOperationException>(() => previa.ParaCompra());
    }

    [Fact]
    public void NomeQueJaExisteNoCatalogo_ReaproveitaSemCriarOutro()
    {
        var catalogo = new List<ProdutoCesta> { Produto(4, "Açúcar", 2, "Alimentos") };
        var previa = Previa(catalogo, Item("ACUCAR CRU", "", reconhecido: false, 4m));
        var linha = Assert.Single(previa.Linhas);
        var formulario = previa.IniciarCadastro(linha);
        Assert.NotNull(formulario);
        formulario.Nome = "acucar";
        formulario.CategoriaId = 9;
        previa.ConfirmarCadastro(formulario, 4.2m, "Outra");

        Assert.Equal(4, linha.ProdutoCestaId);
        Assert.Equal("Açúcar", linha.NomeGravado);
        Assert.False(linha.NovoSeraCriado);
        Assert.Equal(4.2m, linha.Preco);
        Assert.Equal("Açúcar", linha.Titulo);
        var compra = previa.ParaCompra();
        Assert.Empty(compra.ProdutosNovos);
        Assert.Equal(4, Assert.Single(compra.Itens).ProdutoCestaId);
    }

    private static PreviaComprovante Previa(IReadOnlyList<ProdutoCesta> catalogo, params ItemComprovanteLido[] itens) =>
        PreviaComprovante.Criar(
            NormalizadorComprovante.Normalizar(new ComprovanteLido(null, null, null, true, itens), catalogo),
            [],
            catalogo,
            new DateTime(2026, 10, 1));

    private static ComprovanteLido Cupom(
        IReadOnlyList<ItemComprovanteLido> itens,
        decimal? total = null,
        string? loja = null,
        DateOnly? data = null,
        bool legivel = true) =>
        new(loja, data, total, legivel, itens);

    private static ProdutoCesta Produto(int id, string nome, int categoriaId, string categoria, bool ativo = true) =>
        new()
        {
            Id = id,
            Nome = nome,
            CategoriaId = categoriaId,
            Ativo = ativo,
            Categoria = new Categoria { Id = categoriaId, Nome = categoria }
        };

    private static ItemComprovanteLido Item(
        string descricao,
        string produto,
        bool reconhecido,
        decimal preco,
        string? quantidade = null,
        string? unidade = null) =>
        new(descricao, produto, reconhecido, preco, quantidade, unidade, null, null);
}
