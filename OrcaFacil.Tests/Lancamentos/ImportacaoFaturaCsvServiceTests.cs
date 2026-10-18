using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Lancamentos;
using OrcaFacil.Core.Repositories;
using OrcaFacil.Core.Services;

namespace OrcaFacil.Tests.Lancamentos;

public class ImportacaoFaturaCsvServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly ImportacaoFaturaCsvService _service;
    private readonly TransacaoRepository _transacoes;
    private readonly int _cartaoCategoriaId;
    private readonly int _alimentacaoId;
    private readonly int _lazerId;
    private readonly int _comprasId;
    private readonly int _membroId;

    public ImportacaoFaturaCsvServiceTests()
    {
        var estornos = new EstornoFaturaService(_factory);
        _service = new ImportacaoFaturaCsvService(_factory, estornos);
        _transacoes = new TransacaoRepository(_factory);
        var categorias = new CategoriaRepository(_factory);
        var membros = new MembroFamiliaRepository(_factory);

        var cartao = new Categoria { Nome = "Cartão de Crédito", Icone = "💳", CorHex = "#B2CFE6", PermiteItensFatura = true };
        var alimentacao = new Categoria { Nome = "Alimentação", Icone = "🛒", CorHex = "#B2CFE6" };
        var lazer = new Categoria { Nome = "Lazer", Icone = "🎬", CorHex = "#C9B2E6" };
        var compras = new Categoria { Nome = "Compras", Icone = "🛍️", CorHex = "#E6C9B2" };
        categorias.AddAsync(cartao).GetAwaiter().GetResult();
        categorias.AddAsync(alimentacao).GetAwaiter().GetResult();
        categorias.AddAsync(lazer).GetAwaiter().GetResult();
        categorias.AddAsync(compras).GetAwaiter().GetResult();
        var membro = new MembroFamilia { Nome = "Usuário" };
        membros.AddAsync(membro).GetAwaiter().GetResult();

        _cartaoCategoriaId = cartao.Id;
        _alimentacaoId = alimentacao.Id;
        _lazerId = lazer.Id;
        _comprasId = compras.Id;
        _membroId = membro.Id;
    }

    [Fact]
    public void Arredondar_MeioExato_AfastaDeZero()
    {
        Assert.Equal(0.01m, DinheiroEstorno.Arredondar(0.005m));
        Assert.Equal(1.01m, DinheiroEstorno.Arredondar(1.005m));
        Assert.Equal(-0.01m, DinheiroEstorno.Arredondar(-0.005m));
        Assert.Equal(10.00m, DinheiroEstorno.Arredondar(10.00m));
    }

    [Fact]
    public async Task PreverAsync_ArquivoValido_ListaLinhasTotaisENaoGrava()
    {
        var fatura = await CriarFaturaAsync(1_000m);
        var texto = Arquivo(
            "16/10/2026;Supermercado;234,56;Alimentação;Avulsa;;;Compras da semana",
            "02/04/2026;Netflix;39,90;Lazer;Assinatura;;;",
            "10/03/2026;Notebook;350,00;Compras;Parcelada;2;12;Loja",
            "05/01/2026;Farmácia;-18,00;Lazer;;;;");

        var previa = await _service.PreverAsync(fatura.Id, texto);
        var porBytes = await _service.PreverAsync(fatura.Id, FaturaCsvLayout.Codificar(texto));

        Assert.True(previa.ArquivoAceito);
        Assert.Null(previa.ErroArquivo);
        Assert.False(previa.MesFechado);
        Assert.Empty(previa.Erros);
        Assert.Equal(4, previa.LinhasValidas.Count);
        Assert.Equal(4, porBytes.LinhasValidas.Count);
        Assert.Equal(previa.TotalLiquido, porBytes.TotalLiquido);
        Assert.All(previa.LinhasValidas, linha => Assert.False(linha.Duplicada));

        var mercado = previa.LinhasValidas[0];
        Assert.Equal(3, mercado.Numero);
        Assert.Equal(234.56m, mercado.Valor);
        Assert.False(mercado.EhEstorno);
        Assert.Equal(_alimentacaoId, mercado.CategoriaId);
        Assert.Equal("Compras da semana", mercado.Descritivo);
        Assert.Matches("^[0-9A-F]{64}$", mercado.Hash);

        var notebook = previa.LinhasValidas[2];
        Assert.Equal(NaturezaItemLancamento.Parcelada, notebook.Natureza);
        Assert.Equal(2, notebook.ParcelaAtual);
        Assert.Equal(12, notebook.TotalParcelas);

        var farmacia = previa.LinhasValidas[3];
        Assert.True(farmacia.EhEstorno);
        Assert.Equal(18m, farmacia.Valor);
        Assert.Equal(NaturezaItemLancamento.Avulsa, farmacia.Natureza);

        Assert.Equal(624.46m, previa.TotalCompras);
        Assert.Equal(18m, previa.TotalEstornos);
        Assert.Equal(606.46m, previa.TotalLiquido);

        var estado = await CarregarAsync(fatura.Id);
        Assert.Equal(1_000m, estado.Valor);
        Assert.Empty(estado.Itens);
    }

    [Fact]
    public async Task PreverAsync_ErrosPorLinhaEArquivoRecusado_NaoGrava()
    {
        var fatura = await CriarFaturaAsync(500m);
        var texto = Arquivo(
            "16/10/2026;Supermercado;100,00;Alimentação;Avulsa;;;Semana",
            "16/10/2026;Ruim;;Alimentação;;;;");

        var previa = await _service.PreverAsync(fatura.Id, texto);

        Assert.True(previa.ArquivoAceito);
        Assert.Single(previa.LinhasValidas);
        Assert.Equal(100m, previa.TotalCompras);
        var erro = Assert.Single(previa.Erros);
        Assert.Equal(4, erro.Numero);
        Assert.Equal(FaturaCsvLayout.ColunaValor, erro.Coluna);

        var semBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(texto);
        var recusado = await _service.PreverAsync(fatura.Id, semBom);
        Assert.False(recusado.ArquivoAceito);
        Assert.Contains("BOM", recusado.ErroArquivo);
        Assert.Empty(recusado.LinhasValidas);

        var estado = await CarregarAsync(fatura.Id);
        Assert.Equal(500m, estado.Valor);
        Assert.Empty(estado.Itens);
    }

    [Fact]
    public async Task PreverAsync_MesFechado_AindaMontaAPrevia()
    {
        var fatura = await CriarFaturaAsync(200m);
        await FecharMesAsync();

        var previa = await _service.PreverAsync(fatura.Id, Arquivo("16/10/2026;Padaria;10,00;Alimentação;;;;"));

        Assert.True(previa.MesFechado);
        Assert.True(previa.ArquivoAceito);
        Assert.Equal(10m, Assert.Single(previa.LinhasValidas).Valor);
        Assert.Empty((await CarregarAsync(fatura.Id)).Itens);
    }

    [Fact]
    public async Task PreverAsync_DuplicataNoArquivoENaFatura_MarcaPeloHash()
    {
        var fatura = await CriarFaturaAsync(400m, new DateTime(2026, 10, 16), itens:
        [
            new ItemLancamento
            {
                Descricao = "Supermercado",
                Descritivo = "Semana",
                Valor = 100m,
                CategoriaId = _alimentacaoId,
                Natureza = NaturezaItemLancamento.Avulsa,
                Ordem = 0
            }
        ]);
        var texto = Arquivo(
            "16/10/2026;Supermercado;100,00;Alimentação;Avulsa;;;Semana",
            "16/10/2026;Supermercado;100,00;Alimentação;Avulsa;;;Semana",
            "17/10/2026;Padaria;12,00;Alimentação;;;;");

        var previa = await _service.PreverAsync(fatura.Id, texto);

        Assert.True(previa.LinhasValidas[0].Duplicada);
        Assert.True(previa.LinhasValidas[1].Duplicada);
        Assert.False(previa.LinhasValidas[2].Duplicada);
        Assert.Equal(previa.LinhasValidas[0].Hash, previa.LinhasValidas[1].Hash);
        Assert.Equal(12m, previa.TotalCompras);
        Assert.Equal(0m, previa.TotalEstornos);
        Assert.Equal(12m, previa.TotalLiquido);
        Assert.Equal(400m, previa.TotalFatura);
        Assert.Equal(100m, previa.SomaItensAtual);
    }

    [Fact]
    public async Task PreverAsync_HashDeImportacaoAnterior_MarcaMesmoComDescricaoEditada()
    {
        var texto = Arquivo("16/10/2026;Supermercado;80,00;Alimentação;Avulsa;;;Semana");
        var hash = FaturaCsv.Ler(FaturaCsvLayout.Codificar(texto), await CategoriasAsync()).Linhas[0].Hash;
        var fatura = await CriarFaturaAsync(200m, itens:
        [
            new ItemLancamento
            {
                Descricao = "Editado na mão",
                Descritivo = "Outro texto",
                Valor = 10m,
                CategoriaId = _lazerId,
                Natureza = NaturezaItemLancamento.Avulsa,
                Ordem = 0,
                HashImportacao = hash
            }
        ]);

        var previa = await _service.PreverAsync(fatura.Id, texto);

        Assert.True(Assert.Single(previa.LinhasValidas).Duplicada);
        Assert.Equal(0m, previa.TotalCompras);
    }

    [Fact]
    public async Task PreverAsync_MesmaCompraEmOutroDia_NaoEDuplicataDoHashGravado()
    {
        var fatura = await CriarFaturaAsync(200m, new DateTime(2026, 10, 18));
        var dia02 = Arquivo("02/10/2026;Uber;20,00;Lazer;;;;");
        await _service.ConfirmarAsync(fatura.Id, dia02);

        var dia18 = Arquivo("18/10/2026;Uber;20,00;Lazer;;;;");
        var previa = await _service.PreverAsync(fatura.Id, dia18);

        var linha = Assert.Single(previa.LinhasValidas);
        var gravado = (await CarregarAsync(fatura.Id)).Itens.Single().HashImportacao;
        Assert.Equal(FaturaCsv.Ler(FaturaCsvLayout.Codificar(dia02), await CategoriasAsync()).Linhas[0].Hash, gravado);
        Assert.NotEqual(gravado, linha.Hash);
        Assert.False(linha.Duplicada);
        Assert.Equal(new DateTime(2026, 10, 18), linha.Data);
        Assert.Equal(20m, previa.TotalCompras);

        var deNovo = await _service.PreverAsync(fatura.Id, dia02);
        Assert.True(Assert.Single(deNovo.LinhasValidas).Duplicada);

        var resultado = await _service.ConfirmarAsync(fatura.Id, dia18);
        Assert.Equal(1, resultado.Gravados);
        Assert.Equal(0, resultado.DuplicatasIgnoradas);
        var gravada = await CarregarAsync(fatura.Id);
        Assert.Equal(2, gravada.Itens.Count);
        Assert.Equal(200m, gravada.Valor);
        Assert.Equal(2, gravada.Itens.Select(i => i.HashImportacao).Distinct().Count());
    }

    [Fact]
    public async Task PreverAsync_ItemManual_SoDuplicaNaDataDaFatura()
    {
        var fatura = await CriarFaturaAsync(200m, new DateTime(2026, 10, 18), itens:
        [
            new ItemLancamento
            {
                Descricao = "Uber",
                Valor = 20m,
                CategoriaId = _lazerId,
                Natureza = NaturezaItemLancamento.Avulsa,
                Ordem = 0
            }
        ]);

        var noVencimento = await _service.PreverAsync(fatura.Id, Arquivo("18/10/2026;Uber;20,00;Lazer;;;;"));
        var outroDia = await _service.PreverAsync(fatura.Id, Arquivo("02/10/2026;Uber;20,00;Lazer;;;;"));

        Assert.True(Assert.Single(noVencimento.LinhasValidas).Duplicada);
        Assert.False(Assert.Single(outroDia.LinhasValidas).Duplicada);
        Assert.Equal(20m, outroDia.TotalCompras);
    }

    [Fact]
    public async Task ConfirmarAsync_ArquivoMaiorQueUmMega_NaoGrava()
    {
        var fatura = await CriarFaturaAsync(100m);
        var grande = new byte[FaturaCsvLayout.TamanhoMaximoBytes + 1];

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmarAsync(fatura.Id, grande));

        Assert.Contains("1 MB", ex.Message);
        Assert.Contains("Nada foi importado", ex.Message);
        Assert.Empty((await CarregarAsync(fatura.Id)).Itens);
    }

    [Fact]
    public async Task PreverAsync_FaturaInexistente_Recusa()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.PreverAsync(999, Arquivo("16/10/2026;Padaria;10,00;Alimentação;;;;")));

        Assert.Equal("Lançamento não encontrado.", ex.Message);
    }

    [Fact]
    public async Task ConfirmarAsync_GravaCompraParceladaEEstorno_AbateOTotal()
    {
        var fatura = await CriarFaturaAsync(1_000m, pago: true);
        var texto = Arquivo(
            "16/10/2026;Supermercado;234,56;Alimentação;Avulsa;;;Compras da semana",
            "02/04/2026;Netflix;39,90;Lazer;Assinatura;;;",
            "10/03/2026;Notebook;350,00;Compras;Parcelada;2;12;Loja",
            "05/01/2026;Farmácia;-18,00;Lazer;;;;");

        var resultado = await _service.ConfirmarAsync(fatura.Id, texto);

        Assert.Equal(4, resultado.Gravados);
        Assert.Equal(0, resultado.DuplicatasIgnoradas);
        Assert.Equal(624.46m, resultado.TotalCompras);
        Assert.Equal(18m, resultado.TotalEstornos);
        Assert.Equal(606.46m, resultado.TotalLiquido);
        Assert.Equal(982m, resultado.TotalFatura);

        var gravada = await CarregarAsync(fatura.Id);
        Assert.Equal(982m, gravada.Valor);
        Assert.True(gravada.Pago);
        Assert.Equal(4, gravada.Itens.Count);
        Assert.Equal([0, 1, 2, 3], gravada.Itens.OrderBy(i => i.Ordem).Select(i => i.Ordem));

        var mercado = gravada.Itens.Single(i => i.Descricao == "Supermercado");
        Assert.False(mercado.EhEstorno);
        Assert.Equal("Compras da semana", mercado.Descritivo);
        Assert.Equal(234.56m, mercado.Valor);
        Assert.Equal(_alimentacaoId, mercado.CategoriaId);
        Assert.Matches("^[0-9A-F]{64}$", mercado.HashImportacao);

        var notebook = gravada.Itens.Single(i => i.Descricao == "Notebook");
        Assert.Equal(NaturezaItemLancamento.Parcelada, notebook.Natureza);
        Assert.Equal(2, notebook.ParcelaAtual);
        Assert.Equal(12, notebook.TotalParcelas);
        Assert.False(notebook.EhEstorno);

        var estorno = gravada.Itens.Single(i => i.EhEstorno);
        Assert.Equal(18m, estorno.Valor);
        Assert.Equal("Farmácia", estorno.Descricao);
        Assert.Equal("Farmácia", estorno.Descritivo);
        Assert.Equal(NaturezaItemLancamento.Avulsa, estorno.Natureza);
        Assert.Null(estorno.ParcelaAtual);
        Assert.Null(estorno.TotalParcelas);
        Assert.Equal(_lazerId, estorno.CategoriaId);
        Assert.False(string.IsNullOrWhiteSpace(estorno.HashImportacao));
    }

    [Fact]
    public async Task ConfirmarAsync_EstornoNoMeio_ArredondaOLiquidoParaCima()
    {
        var fatura = await CriarFaturaAsync(10.005m);

        var resultado = await _service.ConfirmarAsync(
            fatura.Id,
            Arquivo("16/10/2026;Devolução;-10,00;Lazer;;;;Loja"));

        Assert.Equal(1, resultado.Gravados);
        Assert.Equal(0.01m, resultado.TotalFatura);
        var gravada = await CarregarAsync(fatura.Id);
        Assert.Equal(0.01m, gravada.Valor);
        var estorno = Assert.Single(gravada.Itens);
        Assert.True(estorno.EhEstorno);
        Assert.Equal(10m, estorno.Valor);
        Assert.Equal("Loja", estorno.Descritivo);
        Assert.Equal(NaturezaItemLancamento.Avulsa, estorno.Natureza);
    }

    [Fact]
    public async Task ConfirmarAsync_MesFechado_RecusaComMensagemPadraoENaoGrava()
    {
        var fatura = await CriarFaturaAsync(200m);
        await FecharMesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmarAsync(fatura.Id, Arquivo("16/10/2026;Padaria;10,00;Alimentação;;;;")));

        Assert.Equal(ItensFaturaRegras.MensagemMesFechado(fatura.Data), ex.Message);
        Assert.Contains("10/2026", ex.Message);
        var gravada = await CarregarAsync(fatura.Id);
        Assert.Equal(200m, gravada.Valor);
        Assert.Empty(gravada.Itens);
    }

    [Fact]
    public async Task ConfirmarAsync_LiquidoAbaixoDoMinimo_DesfazACompraQueCabia()
    {
        var fatura = await CriarFaturaAsync(50m);
        var texto = Arquivo(
            "16/10/2026;Padaria;10,00;Alimentação;;;;",
            "16/10/2026;Devolução;-50,00;Lazer;;;;");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmarAsync(fatura.Id, texto));

        Assert.Contains("0,01", ex.Message);
        var gravada = await CarregarAsync(fatura.Id);
        Assert.Equal(50m, gravada.Valor);
        Assert.Empty(gravada.Itens);
    }

    [Fact]
    public async Task ConfirmarAsync_SomaPassaDoTotal_NaoGravaNenhumaLinha()
    {
        var fatura = await CriarFaturaAsync(100m, itens:
        [
            new ItemLancamento
            {
                Descricao = "Já lançado",
                Valor = 20m,
                CategoriaId = _alimentacaoId,
                Natureza = NaturezaItemLancamento.Avulsa,
                Ordem = 0
            }
        ]);
        var texto = Arquivo(
            "16/10/2026;Padaria;40,00;Alimentação;;;;",
            "17/10/2026;Feira;70,00;Alimentação;;;;");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmarAsync(fatura.Id, texto));

        Assert.Contains("soma dos itens", ex.Message, StringComparison.OrdinalIgnoreCase);
        var gravada = await CarregarAsync(fatura.Id);
        Assert.Equal(100m, gravada.Valor);
        var restante = Assert.Single(gravada.Itens);
        Assert.Equal("Já lançado", restante.Descricao);
        Assert.Equal(20m, restante.Valor);
    }

    [Fact]
    public async Task ConfirmarAsync_CategoriaDaFatura_RecusaOConjunto()
    {
        var fatura = await CriarFaturaAsync(300m);
        var texto = Arquivo(
            "16/10/2026;Padaria;10,00;Alimentação;;;;",
            "17/10/2026;Tarifa;5,00;Cartão de Crédito;;;;");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmarAsync(fatura.Id, texto));

        Assert.Contains("diferente da fatura", ex.Message, StringComparison.OrdinalIgnoreCase);
        var gravada = await CarregarAsync(fatura.Id);
        Assert.Equal(300m, gravada.Valor);
        Assert.Empty(gravada.Itens);
    }

    [Fact]
    public async Task ConfirmarAsync_ArquivoComErros_NaoGravaNemAsLinhasValidas()
    {
        var fatura = await CriarFaturaAsync(500m);
        var texto = Arquivo(
            "16/10/2026;Supermercado;100,00;Alimentação;Avulsa;;;Semana",
            "16/10/2026;Ruim;;Alimentação;;;;");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmarAsync(fatura.Id, texto, new PedidoConfirmacaoImportacaoFaturaCsv([3])));

        Assert.Contains("Nada foi importado", ex.Message);
        var gravada = await CarregarAsync(fatura.Id);
        Assert.Equal(500m, gravada.Valor);
        Assert.Empty(gravada.Itens);
    }

    [Fact]
    public async Task ConfirmarAsync_ArquivoRecusado_NaoGrava()
    {
        var fatura = await CriarFaturaAsync(200m);
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(
            Arquivo("16/10/2026;Padaria;10,00;Alimentação;;;;"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmarAsync(fatura.Id, bytes));

        Assert.Contains("Nada foi importado", ex.Message);
        Assert.Empty((await CarregarAsync(fatura.Id)).Itens);
    }

    [Fact]
    public async Task PreverAsync_TetoUsaOTotalEfetivoGravado()
    {
        var fatura = await CriarFaturaAsync(100m);
        await _service.ConfirmarAsync(fatura.Id, Arquivo("16/10/2026;Devolução;-40,00;Lazer;;;;"));
        var gravada = await CarregarAsync(fatura.Id);
        Assert.Equal(60m, gravada.Valor);

        var texto = Arquivo("17/10/2026;Padaria;110,00;Alimentação;;;;");
        var previa = await _service.PreverAsync(fatura.Id, texto);

        Assert.Equal(60m, previa.TotalFatura);
        Assert.Equal(-40m, previa.SomaItensAtual);

        var vista = ImportacaoFaturaCsvApresentacao.Montar(
            previa,
            ImportacaoFaturaCsvApresentacao.SelecaoInicial(previa),
            CultureInfo.GetCultureInfo("pt-BR"));

        Assert.False(vista.PodeConfirmar);
        Assert.Equal(ItensFaturaRegras.MensagemSomaAcimaDoTotal, vista.Bloqueio!.Texto);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ConfirmarAsync(fatura.Id, texto));
        Assert.Equal(ItensFaturaRegras.MensagemSomaAcimaDoTotal, ex.Message);
        var depois = await CarregarAsync(fatura.Id);
        Assert.Equal(60m, depois.Valor);
        Assert.Single(depois.Itens);
    }

    [Fact]
    public async Task SalvarFaturaDepoisDeImportar_PreservaHashEReimportarNaoDuplica()
    {
        var fatura = await CriarFaturaAsync(500m, new DateTime(2026, 10, 18));
        var texto = Arquivo("16/10/2026;Supermercado;100,00;Alimentação;Avulsa;;;Semana");

        var primeira = await _service.ConfirmarAsync(fatura.Id, texto);
        Assert.Equal(1, primeira.Gravados);

        var gravada = await CarregarAsync(fatura.Id);
        var hash = gravada.Itens.Single().HashImportacao;
        Assert.False(string.IsNullOrWhiteSpace(hash));

        var formulario = ImportacaoFaturaCsvApresentacao.CopiarItens(gravada.Itens);
        Assert.Equal(hash, formulario.Single().HashImportacao);

        var regravar = FaturaCartaoEditor.MapearItens(formulario);
        Assert.Equal(hash, regravar.Single().HashImportacao);
        await _transacoes.SubstituirItensAsync(fatura.Id, regravar);

        var depois = await CarregarAsync(fatura.Id);
        Assert.Equal(hash, depois.Itens.Single().HashImportacao);

        var segunda = await _service.ConfirmarAsync(fatura.Id, texto);
        Assert.Equal(0, segunda.Gravados);
        Assert.Equal(1, segunda.DuplicatasIgnoradas);
        Assert.Single((await CarregarAsync(fatura.Id)).Itens);
    }

    [Fact]
    public async Task ConfirmarAsync_ReimportarOMesmoArquivo_NaoDuplicaNemAbateDeNovo()
    {
        var fatura = await CriarFaturaAsync(500m);
        var texto = Arquivo(
            "16/10/2026;Supermercado;100,00;Alimentação;Avulsa;;;Semana",
            "16/10/2026;Devolução;-25,00;Lazer;;;;Loja");

        var primeira = await _service.ConfirmarAsync(fatura.Id, texto);
        Assert.Equal(2, primeira.Gravados);
        Assert.Equal(475m, primeira.TotalFatura);

        var previa = await _service.PreverAsync(fatura.Id, texto);
        Assert.All(previa.LinhasValidas, linha => Assert.True(linha.Duplicada));
        Assert.Equal(0m, previa.TotalCompras);
        Assert.Equal(0m, previa.TotalEstornos);

        var segunda = await _service.ConfirmarAsync(fatura.Id, texto);
        Assert.Equal(0, segunda.Gravados);
        Assert.Equal(2, segunda.DuplicatasIgnoradas);
        Assert.Equal(475m, segunda.TotalFatura);

        var gravada = await CarregarAsync(fatura.Id);
        Assert.Equal(475m, gravada.Valor);
        Assert.Equal(2, gravada.Itens.Count);
        Assert.Equal(1, gravada.Itens.Count(i => i.EhEstorno));
    }

    [Fact]
    public async Task ConfirmarAsync_IgnorarDuplicatasFalso_RecusaENaoGravaALinhaNova()
    {
        var fatura = await CriarFaturaAsync(400m);
        var texto = Arquivo(
            "16/10/2026;Padaria;10,00;Alimentação;;;;",
            "16/10/2026;Padaria;10,00;Alimentação;;;;");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmarAsync(fatura.Id, texto, new PedidoConfirmacaoImportacaoFaturaCsv(IgnorarDuplicatas: false)));

        Assert.Contains("repete um item", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Nada foi importado", ex.Message);
        Assert.Empty((await CarregarAsync(fatura.Id)).Itens);
    }

    [Fact]
    public async Task ConfirmarAsync_DuplicataNoArquivo_GravaSoAPrimeira()
    {
        var fatura = await CriarFaturaAsync(400m);
        var texto = Arquivo(
            "16/10/2026;Padaria;10,00;Alimentação;;;;",
            "16/10/2026;Padaria;10,00;Alimentação;;;;",
            "17/10/2026;Feira;8,00;Alimentação;;;;");

        var resultado = await _service.ConfirmarAsync(fatura.Id, texto);

        Assert.Equal(2, resultado.Gravados);
        Assert.Equal(1, resultado.DuplicatasIgnoradas);
        Assert.Equal(18m, resultado.TotalCompras);
        var gravada = await CarregarAsync(fatura.Id);
        Assert.Equal(2, gravada.Itens.Count);
        Assert.Equal(18m, gravada.Itens.Sum(i => i.Valor));
    }

    [Fact]
    public async Task ConfirmarAsync_ReimportarSemIgnorar_NaoAbateOEstornoOutraVez()
    {
        var fatura = await CriarFaturaAsync(200m);
        var texto = Arquivo("16/10/2026;Devolução;-40,00;Lazer;;;;");
        await _service.ConfirmarAsync(fatura.Id, texto);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmarAsync(fatura.Id, texto, new PedidoConfirmacaoImportacaoFaturaCsv(IgnorarDuplicatas: false)));

        Assert.Contains("Nada foi importado", ex.Message);
        var gravada = await CarregarAsync(fatura.Id);
        Assert.Equal(160m, gravada.Valor);
        Assert.Single(gravada.Itens);
    }

    [Fact]
    public async Task ConfirmarAsync_LinhasEscolhidas_GravaSoAsPedidas()
    {
        var fatura = await CriarFaturaAsync(500m);
        var texto = Arquivo(
            "16/10/2026;Padaria;10,00;Alimentação;;;;",
            "17/10/2026;Cinema;30,00;Lazer;;;;",
            "18/10/2026;Feira;8,00;Alimentação;;;;");

        var resultado = await _service.ConfirmarAsync(
            fatura.Id,
            texto,
            new PedidoConfirmacaoImportacaoFaturaCsv([5, 3]));

        Assert.Equal(2, resultado.Gravados);
        Assert.Equal(500m, resultado.TotalFatura);
        var gravada = await CarregarAsync(fatura.Id);
        Assert.Equal(500m, gravada.Valor);
        Assert.Equal(["Padaria", "Feira"], gravada.Itens.OrderBy(i => i.Ordem).Select(i => i.Descricao));
        Assert.Equal([0, 1], gravada.Itens.OrderBy(i => i.Ordem).Select(i => i.Ordem));
    }

    [Fact]
    public async Task ConfirmarAsync_LinhaEscolhidaInexistente_NaoGravaAsOutras()
    {
        var fatura = await CriarFaturaAsync(200m);
        var texto = Arquivo("16/10/2026;Padaria;10,00;Alimentação;;;;");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmarAsync(fatura.Id, texto, new PedidoConfirmacaoImportacaoFaturaCsv([3, 99])));

        Assert.Contains("99", ex.Message);
        Assert.Contains("Nada foi importado", ex.Message);
        Assert.Empty((await CarregarAsync(fatura.Id)).Itens);
    }

    [Fact]
    public async Task ConfirmarAsync_CategoriaSemItens_Recusa()
    {
        var fatura = await CriarFaturaAsync(200m, categoriaId: _alimentacaoId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmarAsync(fatura.Id, Arquivo("16/10/2026;Padaria;10,00;Lazer;;;;")));

        Assert.Contains("não permite itens", ex.Message, StringComparison.OrdinalIgnoreCase);
        var gravada = await CarregarAsync(fatura.Id);
        Assert.Equal(200m, gravada.Valor);
        Assert.Empty(gravada.Itens);
    }

    [Fact]
    public async Task ConfirmarAsync_SoExemplos_NaoAlteraAFatura()
    {
        var fatura = await CriarFaturaAsync(80m);
        var resultado = await _service.ConfirmarAsync(fatura.Id, FaturaCsvLayout.GerarModelo());

        Assert.Equal(0, resultado.Gravados);
        Assert.Equal(80m, resultado.TotalFatura);
        Assert.Empty((await CarregarAsync(fatura.Id)).Itens);
    }

    private async Task<Transacao> CarregarAsync(int id)
    {
        await using var contexto = _factory.CreateDbContext();
        return await contexto.Transacoes.Include(t => t.Itens).SingleAsync(t => t.Id == id);
    }

    private async Task<List<Categoria>> CategoriasAsync()
    {
        await using var contexto = _factory.CreateDbContext();
        return await contexto.Categorias.AsNoTracking().ToListAsync();
    }

    private async Task FecharMesAsync()
    {
        await using var contexto = _factory.CreateDbContext();
        contexto.FechamentosPeriodo.Add(new FechamentoPeriodo { Mes = 10, Ano = 2026, Fechado = true });
        await contexto.SaveChangesAsync();
    }

    private async Task<Transacao> CriarFaturaAsync(
        decimal valor,
        DateTime? data = null,
        bool pago = false,
        int? categoriaId = null,
        List<ItemLancamento>? itens = null)
    {
        var fatura = new Transacao
        {
            Descricao = "Fatura Nubank",
            Valor = valor,
            Data = data ?? new DateTime(2026, 10, 18),
            Tipo = TipoTransacao.Despesa,
            Pago = pago,
            CategoriaId = categoriaId ?? _cartaoCategoriaId,
            MembroFamiliaId = _membroId,
            Itens = itens ?? []
        };
        await _transacoes.AddAsync(fatura);
        return fatura;
    }

    private static string Arquivo(params string[] dados)
    {
        var sb = new StringBuilder();
        sb.Append(FaturaCsvLayout.LinhaVersao).Append('\n');
        sb.Append(FaturaCsvLayout.Cabecalho).Append('\n');
        foreach (var linha in dados)
            sb.Append(linha).Append('\n');
        return sb.ToString();
    }

    public void Dispose() => _factory.Dispose();
}
