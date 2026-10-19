using System.IO.Compression;
using System.Reflection;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Exportacao;
using OrcaFacil.Core.Planilha;
using OrcaFacil.Core.Tema;

namespace OrcaFacil.Tests.Exportacao;

public class ExportadorPlanilhaTests
{
    private const string SentinelaHash = "SENTINELA-HASH";
    private const string SentinelaLog = "SENTINELA-LOG";
    private const string SentinelaChave = "SENTINELA-CHAVE";
    private const string SentinelaUri = "SENTINELA-URI";
    private const string SentinelaSenha = "SENTINELA-SENHA";
    private const string SentinelaMigration = "SENTINELA-MIGRATION";
    private const string SentinelaToken = "SENTINELA-TOKEN";

    private static readonly string[] Sentinelas =
    [
        SentinelaHash, SentinelaLog, SentinelaChave,
        SentinelaUri, SentinelaSenha, SentinelaMigration, SentinelaToken
    ];

    [Fact]
    public void CadaDbSetTemDecisaoDeAbaOuExclusao()
    {
        var conjuntos = typeof(AppDbContext)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        var incluidos = ExportadorPlanilha.AbasPorConjunto.Keys.ToHashSet(StringComparer.Ordinal);
        var excluidos = ExportadorPlanilha.ConjuntosExcluidos.ToHashSet(StringComparer.Ordinal);
        var decididos = incluidos.Union(excluidos).ToHashSet(StringComparer.Ordinal);

        var semDecisao = conjuntos.Except(decididos).Order(StringComparer.Ordinal).ToArray();
        Assert.True(
            semDecisao.Length == 0,
            "DbSet novo sem decisão, incluído ou excluído: " + string.Join(", ", semDecisao));

        var fantasma = decididos.Except(conjuntos).Order(StringComparer.Ordinal).ToArray();
        Assert.True(fantasma.Length == 0, "Decisão para conjunto que não existe: " + string.Join(", ", fantasma));

        var duplicado = incluidos.Intersect(excluidos).Order(StringComparer.Ordinal).ToArray();
        Assert.True(duplicado.Length == 0, "Conjunto incluído e excluído ao mesmo tempo: " + string.Join(", ", duplicado));

        var abas = incluidos
            .Select(nome => ExportadorPlanilha.AbasPorConjunto[nome])
            .Append("Configurações")
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(abas, ExportadorPlanilha.NomesDeAba.Order(StringComparer.Ordinal).ToArray());
        Assert.Contains(nameof(AppDbContext.LogsEvento), excluidos);
        Assert.Contains(nameof(AppDbContext.AtivosFinanceiros), incluidos);
        Assert.Contains(nameof(AppDbContext.OperacoesAtivo), incluidos);
    }

    [Fact]
    public void ConfiguracaoExportavelNaoTemCampoDeSegredo()
    {
        var nomes = typeof(ConfiguracaoExportavel)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(p => p.Name)
            .ToArray();

        Assert.Equal(
            ["Tema", "NotificacoesAtivas", "SomAtivo", "GraficoLancamentosExpandido", "PrazosAviso", "Provedor", "Url", "Modelo"],
            nomes);

        foreach (var nome in nomes)
        {
            Assert.DoesNotContain("Chave", nome, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Senha", nome, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Segredo", nome, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Token", nome, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Foto", nome, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Hash", nome, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Espelho", nome, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ExportaAbasPedidasENaoVazaSentinela()
    {
        using var fabrica = new TestDbContextFactory();
        using var contexto = fabrica.CreateDbContext();
        var credito = Semear(contexto);
        var configuracao = new ConfiguracaoExportavel
        {
            Tema = TemaPreferido.Escuro,
            NotificacoesAtivas = true,
            SomAtivo = false,
            PrazosAviso = PrazoAviso.TresDiasAntes | PrazoAviso.NoDia,
            GraficoLancamentosExpandido = true,
            Provedor = "Ollama",
            Url = "https://user:" + SentinelaSenha + "@api.exemplo.test/v1;token=" + SentinelaToken,
            Modelo = "llama3"
        };

        using var destino = new MemoryStream();
        ExportadorPlanilha.Exportar(contexto, configuracao, destino);
        Assert.Equal(0, destino.Position);
        var bytes = new byte[destino.Length];
        Assert.Equal(bytes.Length, destino.Read(bytes, 0, bytes.Length));
        var pacote = XmlDasPartes(bytes);
        foreach (var sentinela in Sentinelas)
            Assert.True(pacote.IndexOf(sentinela, StringComparison.Ordinal) < 0, "A planilha contém " + sentinela + ".");
        Assert.True(pacote.IndexOf("content://", StringComparison.Ordinal) < 0, "A planilha contém content://.");

        using var pasta = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(ExportadorPlanilha.NomesDeAba, pasta.Worksheets.Select(a => a.Name).ToArray());
        Assert.DoesNotContain(pasta.Worksheets, a => a.Name.Equals("History", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(PlanilhaPtBr.LarguraColuna, pasta.Worksheet("Ativos").Column(1).Width);
        Assert.Equal(PlanilhaPtBr.LarguraColuna, pasta.Worksheet("Operações").Column(1).Width);

        foreach (var aba in pasta.Worksheets)
        {
            foreach (var celula in aba.CellsUsed())
            {
                var texto = celula.GetFormattedString();
                foreach (var sentinela in Sentinelas)
                    Assert.True(texto.IndexOf(sentinela, StringComparison.Ordinal) < 0, aba.Name + " contém " + sentinela + ".");
            }
        }

        var perfis = pasta.Worksheet("Perfis");
        Cabecalho(perfis, "Nome", "Apelido", "Papel", "Ativo", "Foto", "Criado em");
        Assert.Equal("Daniel", perfis.Cell(2, 1).GetText());
        Assert.Equal("Dani", perfis.Cell(2, 2).GetText());
        Assert.Equal("Administrador", perfis.Cell(2, 3).GetText());
        Assert.Equal(PlanilhaPtBr.Sim, perfis.Cell(2, 4).GetText());
        Assert.Equal("foto-daniel.jpg", perfis.Cell(2, 5).GetText());
        DataHora(perfis.Cell(2, 6), new DateTime(2026, 10, 18, 23, 30, 0));
        Assert.Equal(2, perfis.LastRowUsed()!.RowNumber());

        var categorias = pasta.Worksheet("Categorias");
        Cabecalho(categorias, "Nome", "Ícone", "Cor", "Tipo padrão", "Padrão do sistema", "Permite itens de fatura", "Ativa");
        Assert.Equal("Mercado", categorias.Cell(2, 1).GetText());
        Assert.Equal("Despesa", categorias.Cell(2, 4).GetText());
        Assert.Equal(PlanilhaPtBr.Sim, categorias.Cell(2, 5).GetText());

        var lancamentos = pasta.Worksheet("Lançamentos");
        Cabecalho(lancamentos,
            "Descrição", "Valor", "Data", "Tipo", "Recorrência", "Pago", "Observação",
            "Categoria", "Perfil", "Lançamento de origem", "Cartão", "Criado em");
        Assert.Equal("Aluguel de outubro", lancamentos.Cell(3, 1).GetText());
        Moeda(lancamentos.Cell(3, 2), 10.50m);
        Data(lancamentos.Cell(3, 3), new DateTime(2026, 10, 19));
        Assert.Equal("Despesa", lancamentos.Cell(3, 4).GetText());
        Assert.Equal("Mensal", lancamentos.Cell(3, 5).GetText());
        Assert.Equal(PlanilhaPtBr.Nao, lancamentos.Cell(3, 6).GetText());
        Assert.Equal("Mercado", lancamentos.Cell(3, 8).GetText());
        Assert.Equal("Daniel (Dani)", lancamentos.Cell(3, 9).GetText());
        Assert.Equal("Aluguel modelo — 01/09/2026", lancamentos.Cell(3, 10).GetText());
        Assert.Equal(credito.Rotulo, lancamentos.Cell(3, 11).GetText());
        DataHora(lancamentos.Cell(3, 12), new DateTime(2026, 10, 18, 23, 30, 0));

        var itens = pasta.Worksheet("Itens da fatura");
        Cabecalho(itens,
            "Descrição", "Descritivo", "Valor", "Estorno", "Natureza", "Parcela",
            "Total de parcelas", "Ordem", "Lançamento", "Categoria");
        Assert.Equal("Pão", itens.Cell(2, 1).GetText());
        Assert.Equal("Padaria da esquina", itens.Cell(2, 2).GetText());
        Moeda(itens.Cell(2, 3), 8.50m);
        Assert.Equal("Parcelada", itens.Cell(2, 5).GetText());
        Numero(itens.Cell(2, 6), 2m);
        Numero(itens.Cell(2, 7), 10m);
        Assert.Equal("Aluguel de outubro", itens.Cell(2, 9).GetText());
        Assert.Equal("Mercado", itens.Cell(2, 10).GetText());

        var orcamentos = pasta.Worksheet("Orçamentos");
        Cabecalho(orcamentos, "Categoria", "Limite", "Meta de redução", "Mês", "Ano", "Criado em");
        Assert.Equal("Mercado", orcamentos.Cell(2, 1).GetText());
        Moeda(orcamentos.Cell(2, 2), 800m);
        Numero(orcamentos.Cell(2, 3), 10m);
        Numero(orcamentos.Cell(2, 4), 10m);
        Numero(orcamentos.Cell(2, 5), 2026m);

        var metas = pasta.Worksheet("Metas");
        Cabecalho(metas, "Nome", "Valor alvo", "Valor atual", "Prazo", "Ícone", "Concluída", "Perfil", "Percentual", "Criado em");
        Moeda(metas.Cell(2, 2), 200m);
        Moeda(metas.Cell(2, 3), 50m);
        Data(metas.Cell(2, 4), new DateTime(2027, 1, 15));
        Assert.Equal(PlanilhaPtBr.Nao, metas.Cell(2, 6).GetText());
        Assert.Equal("Daniel (Dani)", metas.Cell(2, 7).GetText());
        Numero(metas.Cell(2, 8), 25m);

        var ativos = pasta.Worksheet("Ativos");
        Cabecalho(ativos, "Ticker", "Nome", "Tipo", "Ativo", "Criado em");
        Assert.Equal("PETR4", ativos.Cell(2, 1).GetText());
        Assert.Equal("Petrobras", ativos.Cell(2, 2).GetText());
        Assert.Equal("Ação", ativos.Cell(2, 3).GetText());
        Assert.Equal(PlanilhaPtBr.Sim, ativos.Cell(2, 4).GetText());

        var operacoes = pasta.Worksheet("Operações");
        Cabecalho(operacoes,
            "Ticker", "Nome do ativo", "Perfil", "Operação", "Data", "Quantidade",
            "Preço unitário", "Custos", "Corretora", "Criado em", "Atualizado em");
        Assert.Equal("PETR4", operacoes.Cell(2, 1).GetText());
        Assert.Equal("Petrobras", operacoes.Cell(2, 2).GetText());
        Assert.Equal("Daniel (Dani)", operacoes.Cell(2, 3).GetText());
        Assert.Equal("Compra", operacoes.Cell(2, 4).GetText());
        Data(operacoes.Cell(2, 5), new DateTime(2026, 10, 2));
        Numero(operacoes.Cell(2, 6), 10m);
        Moeda(operacoes.Cell(2, 7), 36.50m);
        Moeda(operacoes.Cell(2, 8), 1.25m);
        Assert.Equal("Clara", operacoes.Cell(2, 9).GetText());

        var compras = pasta.Worksheet("Compras da cesta");
        Cabecalho(compras,
            "Mês", "Ano", "Data", "Local", "Benefício", "Tipo do benefício", "Cartão de benefício",
            "Dinheiro", "Valor no crédito", "Cartão de crédito", "Item da fatura", "Lançamento", "Criado em");
        Assert.Equal("Mercado Central", compras.Cell(2, 4).GetText());
        Moeda(compras.Cell(2, 5), 20m);
        Assert.Equal("Vale alimentação", compras.Cell(2, 6).GetText());
        Assert.Equal("Padaria da esquina", compras.Cell(2, 11).GetText());
        Assert.Equal("Aluguel de outubro", compras.Cell(2, 12).GetText());

        var cartoes = pasta.Worksheet("Cartões");
        Cabecalho(cartoes, "Nome", "Bandeira", "Últimos dígitos", "Tipo", "Tipo do benefício", "Ativo", "Criado em");
        Assert.Equal("Nubank", cartoes.Cell(2, 1).GetText());
        Assert.Equal(XLDataType.Text, cartoes.Cell(2, 3).DataType);
        Assert.Equal("0012", cartoes.Cell(2, 3).GetText());
        Assert.Equal("Crédito", cartoes.Cell(2, 4).GetText());
        Assert.Equal(XLDataType.Blank, cartoes.Cell(2, 5).DataType);
        Assert.Equal("Benefício", cartoes.Cell(3, 4).GetText());
        Assert.Equal("Vale alimentação", cartoes.Cell(3, 5).GetText());

        var config = pasta.Worksheet("Configurações");
        Cabecalho(config, "Item", "Valor");
        Assert.Equal("Escuro", config.Cell(2, 2).GetText());
        Assert.Equal(PlanilhaPtBr.Sim, config.Cell(3, 2).GetText());
        Assert.Equal(PlanilhaPtBr.Nao, config.Cell(4, 2).GetText());
        Assert.Equal("Gráfico de lançamentos expandido", config.Cell(5, 1).GetText());
        Assert.Equal(PlanilhaPtBr.Sim, config.Cell(5, 2).GetText());
        Assert.Equal("3 dias antes do vencimento, No dia do vencimento", config.Cell(6, 2).GetText());
        Assert.Equal("Ollama", config.Cell(7, 2).GetText());
        Assert.Equal("https://api.exemplo.test", config.Cell(8, 2).GetText());
        Assert.Equal("llama3", config.Cell(9, 2).GetText());

        var fonte = File.ReadAllText(Path.Combine(Raiz(), "OrcaFacil.Core", "Exportacao", "ExportadorPlanilha.cs"));
        Assert.DoesNotContain("AdjustToContents", fonte, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://user:SENTINELA-SENHA@api.exemplo.test/v1?token=SENTINELA-TOKEN", "https://api.exemplo.test")]
    [InlineData("https://user:SENTINELA-SENHA@api.exemplo.test/v1?token=X", "https://api.exemplo.test")]
    [InlineData("https://api.exemplo.test/path;token=SENTINELA-TOKEN", "https://api.exemplo.test")]
    [InlineData("https://user:SENTINELA-SENHA@api.exemplo.test:8443/v1;token=X?y=1#frag", "https://api.exemplo.test:8443")]
    [InlineData("https://api.exemplo.test:443/v1", "https://api.exemplo.test")]
    [InlineData("http://api.exemplo.test:80/v1", "http://api.exemplo.test")]
    [InlineData("http://api.exemplo.test:8080/v1", "http://api.exemplo.test:8080")]
    [InlineData("https://api.exemplo.test/v1", "https://api.exemplo.test")]
    [InlineData("HTTP://api.exemplo.test/v1", "http://api.exemplo.test")]
    [InlineData("HTTPS://HOST/V1", "https://host")]
    [InlineData("https://user:segre@do@host/v1", "https://host")]
    [InlineData("https://user:SENTINELA-SEGRE@SENTINELA-DO@host/v1", "https://host")]
    [InlineData(" https://user:SENTINELA-SEGRE@SENTINELA-DO@host/v1", "https://host")]
    [InlineData("https://host/v1%3Ftoken=SENTINELA", "https://host")]
    [InlineData("https://host/v1%3ftoken=SENTINELA", "https://host")]
    [InlineData("https://host/v1%3Btoken=SENTINELA", "https://host")]
    [InlineData("https://host/v1%23SENTINELA", "https://host")]
    [InlineData("https://host/v1%253Ftoken=SEGREDO", "https://host")]
    [InlineData("https://user:SENTINELA%40x@host/v1", "https://host")]
    [InlineData("https://host%40evil/SENTINELA", "")]
    [InlineData("https://host/v1%3ASEGREDO", "https://host")]
    [InlineData("https://[::1]/v1", "https://[::1]")]
    [InlineData("https://user:SENTINELA@[::1]:8443/v1", "https://[::1]:8443")]
    [InlineData("https://xn--caf-dma.example/v1", "https://xn--caf-dma.example")]
    [InlineData("https://café.example/v1", "https://café.example")]
    [InlineData("http:/host", "")]
    [InlineData("http:\\\\host", "")]
    [InlineData("ht\u200Btp://host", "")]
    [InlineData("\thttps://host/SEGREDO", "https://host")]
    [InlineData("https://ex\\ample.com/SENTINELA", "")]
    [InlineData("https://user\\SENTINELA@host/v1", "https://host")]
    [InlineData("https://host/../SEGREDO", "https://host")]
    [InlineData("https://host/a%2F..%2FSEGREDO", "https://host")]
    [InlineData("https://host/a%252F..%252FSEGREDO", "https://host")]
    [InlineData("https:///x", "")]
    [InlineData("https://@host/x", "https://host")]
    [InlineData("https://user@", "")]
    [InlineData("/path;token=SENTINELA-TOKEN?x=1", "")]
    [InlineData("/path;token=X", "")]
    [InlineData("//user:SENTINELA-SENHA@host/v1?token=SENTINELA-TOKEN", "")]
    [InlineData("user:SENTINELA-SENHA@host/v1;token=SENTINELA-TOKEN", "")]
    [InlineData("user:SENTINELA-SENHA@host", "")]
    [InlineData("user:sec/ret@host", "")]
    [InlineData("user:SENTINELA-SEC/ret@host", "")]
    [InlineData("//user:SENTINELA-SENHA@host/v1;token=SENTINELA-TOKEN#frag", "")]
    [InlineData("//host/caminho/a@b.txt", "")]
    [InlineData("//host/v1?q=a@b", "")]
    [InlineData("//host/v1?token=SENTINELA-SEGREDO", "")]
    [InlineData("//host/v1?token=SENTINELA-TOKEN", "")]
    [InlineData(" //user:SENTINELA-SENHA@host/v1?token=X", "")]
    [InlineData("\u00A0//user:SENTINELA-SENHA@host/v1?token=X", "")]
    [InlineData("//user:SENTINELA-SEGRE@SENTINELA-DO@host/v1", "")]
    [InlineData("//host/v1?token=SENTINELA&next=https://x", "")]
    [InlineData("//host/v1?next=https://evil.example/x", "")]
    [InlineData("//host/caminho/a@b.txt?q=https://x", "")]
    [InlineData("//host/a://b.txt", "")]
    [InlineData("file:///SENTINELA-SENHA", "")]
    [InlineData("content://SENTINELA-HOST/a", "")]
    [InlineData("javascript:SENTINELA-SENHA", "")]
    [InlineData("data:text/plain,SENTINELA-SENHA", "")]
    [InlineData("ftp://SENTINELA-SENHA@host/v1", "")]
    [InlineData("..", "")]
    public void UrlSemSegredo_TiraUserinfoConsultaEMatriz(string url, string esperado)
    {
        var limpa = ExportadorPlanilha.UrlSemSegredo(url);
        Assert.Equal(esperado, limpa);
        Assert.DoesNotContain("SENTINELA", limpa, StringComparison.Ordinal);
        Assert.DoesNotContain("SEGREDO", limpa, StringComparison.Ordinal);
    }

    [Fact]
    public void UrlForaDaLista_NaoVazaSentinelaNoXml()
    {
        using var fabrica = new TestDbContextFactory();
        using var contexto = fabrica.CreateDbContext();
        using var destino = new MemoryStream();
        ExportadorPlanilha.Exportar(contexto, new ConfiguracaoExportavel
        {
            Url = "//host/v1?token=SENTINELA-TOKEN&next=https://x"
        }, destino);

        var xml = XmlDasPartes(destino.ToArray());
        Assert.DoesNotContain("SENTINELA", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("file://", xml, StringComparison.Ordinal);
        using var pasta = new XLWorkbook(destino);
        Assert.Equal(XLDataType.Blank, pasta.Worksheet("Configurações").Cell(8, 2).DataType);
    }

    [Fact]
    public void FotoSaiSoComONomeDoArquivo()
    {
        using var fabrica = new TestDbContextFactory();
        using var contexto = fabrica.CreateDbContext();
        var quando = DateTime.UtcNow;
        contexto.MembrosFamilia.AddRange(
            new MembroFamilia { Nome = "Ana", FotoPath = "pasta/foto-ana.jpg", CriadoEm = quando },
            new MembroFamilia { Nome = "Bia", FotoPath = "content://midia/SENTINELA-FOTO/bia.jpg?token=SEGREDO", CriadoEm = quando },
            new MembroFamilia { Nome = "Cris", FotoPath = "C:\\Users\\d\\cris.jpg", CriadoEm = quando },
            new MembroFamilia { Nome = "Duda", FotoPath = "/var/fotos/duda.jpg", CriadoEm = quando });
        contexto.SaveChanges();

        using var destino = new MemoryStream();
        ExportadorPlanilha.Exportar(contexto, new ConfiguracaoExportavel(), destino);
        var xml = XmlDasPartes(destino.ToArray());
        Assert.DoesNotContain("SENTINELA-FOTO", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("content://", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("SEGREDO", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\Users", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("/var/fotos", xml, StringComparison.Ordinal);

        using var pasta = new XLWorkbook(destino);
        var aba = pasta.Worksheet("Perfis");
        Assert.DoesNotContain("pasta/", xml, StringComparison.Ordinal);
        Assert.Equal("foto-ana.jpg", aba.Cell(2, 5).GetText());
        Assert.Equal(XLDataType.Blank, aba.Cell(3, 5).DataType);
        Assert.Equal("cris.jpg", aba.Cell(4, 5).GetText());
        Assert.Equal("duda.jpg", aba.Cell(5, 5).GetText());
    }

    [Theory]
    [InlineData("pasta/foto-ana.jpg", "foto-ana.jpg")]
    [InlineData("\u00A0/var/fotos/duda.jpg", "")]
    [InlineData("\n/var/fotos/duda.jpg", "")]
    [InlineData("\r/var/fotos/duda.jpg", "")]
    [InlineData("\t/var/fotos/duda.jpg", "")]
    [InlineData(" /var/fotos/duda.jpg", "duda.jpg")]
    [InlineData("content://X", "X")]
    [InlineData("\u00A0content://X", "")]
    [InlineData("content://midia/bia.jpg", "bia.jpg")]
    [InlineData("\u200B/var/fotos/duda.jpg", "")]
    [InlineData("\uFEFF/var/fotos/duda.jpg", "")]
    [InlineData("/var/fotos/duda.jpg\u200B", "")]
    [InlineData("C:Users\\daniel\\foto.jpg", "foto.jpg")]
    [InlineData("foto\u0001.jpg", "")]
    [InlineData("C:\\Users\\daniel\\", "")]
    [InlineData("/home/daniel/", "")]
    [InlineData("pasta/", "")]
    [InlineData("C:", "")]
    [InlineData("..", "")]
    [InlineData(".", "")]
    [InlineData("   ", "")]
    [InlineData("C:foto.jpg", "foto.jpg")]
    [InlineData("C%3Afoto.jpg", "")]
    [InlineData("foto%3Aana.jpg", "")]
    [InlineData("pasta/%2F%20", "")]
    [InlineData("%2E", "")]
    [InlineData("%2E%2E", "")]
    [InlineData("%20", "")]
    [InlineData("%E2%80%8B", "")]
    [InlineData("Users%2525252Fdaniel%2525252Ffoto.jpg", "")]
    [InlineData("Users%25252525252Fdaniel%25252525252Ffoto.jpg", "")]
    [InlineData("bia.jpg%2525253Ftoken=SEGREDO", "")]
    [InlineData("bia.jpg%25252525253Ftoken=SEGREDO", "")]
    [InlineData("pasta/%2525252F", "")]
    [InlineData("pasta/%25252525252F", "")]
    [InlineData("content://a/b/c.jpg?x", "")]
    [InlineData("content://midia/bia.jpg%3Ftoken=SEGREDO", "")]
    [InlineData("bia.jpg%3Ftoken=SEGREDO", "")]
    [InlineData("bia.jpg%253Ftoken=SEGREDO", "")]
    [InlineData("bia.jpg%3bx=SEGREDO", "")]
    [InlineData("bia.jpg%23SEGREDO", "")]
    [InlineData("bia.jpg?token=/SEGREDO", "")]
    [InlineData("bia.jpg%3Ftoken=%2FSEGREDO", "")]
    [InlineData("bia.jpg%253Ftoken=%252FSEGREDO", "")]
    [InlineData("bia.jpg%2525253Ftoken=%2525252FSEGREDO", "")]
    [InlineData("bia.jpg%25252525253Ftoken=%25252525252FSEGREDO", "")]
    [InlineData("foto.jpg#/SEGREDO", "")]
    [InlineData("foto.jpg;x=/SEGREDO", "")]
    [InlineData("foto.jpg?a:SEGREDO", "")]
    [InlineData("foto.jpg?x=\\\\SEGREDO", "")]
    [InlineData("pasta/foto.jpg?x=/etc/SEGREDO.jpg", "")]
    [InlineData("C:\\Users\\daniel#1\\foto.jpg", "")]
    [InlineData("/home/daniel;old/foto.jpg", "")]
    [InlineData("pasta%23x/foto.jpg", "")]
    [InlineData("Fotos?/ana.jpg", "")]
    [InlineData("C:\\Users#1\\daniel\\foto.jpg", "")]
    [InlineData("/home/da#niel/foto.jpg", "")]
    [InlineData("/home/da%23niel/foto.jpg", "")]
    [InlineData("/home/da%2523niel/foto.jpg", "")]
    [InlineData("/home/da%25252523niel/foto.jpg", "")]
    [InlineData("%2Fhome%2Fda%23niel%2Ffoto.jpg", "")]
    [InlineData("%252Fhome%252Fda%2523niel%252Ffoto.jpg", "")]
    [InlineData("%2525252Fhome%2525252Fda%25252523niel%2525252Ffoto.jpg", "")]
    [InlineData("C:\\Users\\dan;iel\\foto.jpg", "")]
    [InlineData("C:\\Users\\dan%3Biel\\foto.jpg", "")]
    [InlineData("C:\\Users\\dan%253Biel\\foto.jpg", "")]
    [InlineData("C:\\Users\\dan%2525253Biel\\foto.jpg", "")]
    [InlineData("C:%5CUsers%5Cdan%3Biel%5Cfoto.jpg", "")]
    [InlineData("C:%255CUsers%255Cdan%253Biel%255Cfoto.jpg", "")]
    [InlineData("C:%2525255CUsers%2525255Cdan%2525253Biel%2525255Cfoto.jpg", "")]
    [InlineData("/home/daniel/fo?to/x.jpg", "")]
    [InlineData("pasta%23daniel%2Ffoto.jpg", "")]
    [InlineData("pasta%2523daniel%252Ffoto.jpg", "")]
    [InlineData("pasta%25252523daniel%2525252Ffoto.jpg", "")]
    [InlineData("content://media/x;y/foto.jpg", "")]
    [InlineData("content://media/x%3By/foto.jpg", "")]
    [InlineData("content://media/x%253By/foto.jpg", "")]
    [InlineData("content://media/x%2525253By/foto.jpg", "")]
    [InlineData("foto.jpg?x=1", "")]
    [InlineData("pasta/foto.jpg?x=1", "")]
    [InlineData("pasta%2Fdaniel%2Ffoto.jpg", "")]
    [InlineData("fo\u200Blder/SENTINELA-CF/foto.jpg", "")]
    [InlineData("/home/daniel/\u200B", "")]
    [InlineData("foto.jpg\u200B", "")]
    [InlineData("bia.jpg\uFF1Ftoken=/SEGREDO", "")]
    [InlineData("bia.jpg\uFF03token=/SEGREDO", "")]
    [InlineData("bia.jpg\uFF1Btoken=/SEGREDO", "")]
    [InlineData("bia.jpg%EF%BC%9Ftoken=/SEGREDO", "")]
    [InlineData("bia.jpg\uFF053Ftoken=/SEGREDO", "")]
    [InlineData("bia.jpg\uFF05\uFF13\uFF26token=/SEGREDO", "")]
    [InlineData("bia.jpg%3\u200BFtoken=/SEGREDO", "")]
    [InlineData("/home/da?niel\uFF0Ffoto.jpg", "")]
    [InlineData("/home/da?niel\uFF3Cfoto.jpg", "")]
    [InlineData("foto-20261017120000.jpg", "foto-20261017120000.jpg")]
    [InlineData("pasta/foto.jpg", "foto.jpg")]
    [InlineData("C:\\Fotos\\foto.jpg", "foto.jpg")]
    [InlineData("content://media/foto.jpg", "foto.jpg")]
    [InlineData(".foto.jpg", "")]
    [InlineData("-foto.jpg", "")]
    [InlineData("_foto.jpg", "")]
    [InlineData("...", "")]
    [InlineData(". .", "")]
    public void NomeDeFoto_SaiSoONomeDoArquivo(string valor, string esperado)
    {
        var nome = ExportadorPlanilha.NomeDeFoto(valor);
        Assert.Equal(esperado, nome);
        Assert.DoesNotContain("SEGREDO", nome, StringComparison.Ordinal);
        Assert.DoesNotContain("SENTINELA", nome, StringComparison.Ordinal);
        Assert.DoesNotContain("daniel", nome, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NomeDeFoto_LimiteDe128Caracteres()
    {
        var passa = new string('a', 128);
        var longo = new string('a', 129);
        Assert.Equal(passa, ExportadorPlanilha.NomeDeFoto(passa));
        Assert.Equal(passa, ExportadorPlanilha.NomeDeFoto("pasta/" + passa));
        Assert.Equal("", ExportadorPlanilha.NomeDeFoto(longo));
        Assert.Equal("", ExportadorPlanilha.NomeDeFoto("C:\\Fotos\\" + longo));
    }

    [Fact]
    public void NomeDeFoto_PrefixoInvisivelNaoVazaPastaNoXml()
    {
        var caminhos = new[] { "/home/daniel/foto.jpg", "C:\\Users\\daniel\\foto.jpg" };
        var prefixos = new List<string>
        {
            "\uFEFF",
            "\u200B", "\u200C", "\u200D", "\u200E", "\u200F",
            "\u2060",
            "\u180E",
            "\u0001",
            "\u00A0\uFEFF",
            new string(['\uD800'])
        };

        var fotos = new List<string>
        {
            "C:Users\\daniel\\foto.jpg",
            "/home/daniel/\u200B",
            "/home/daniel/\u200B\uFEFF",
            "/home/daniel/foto.jpg\u200B",
            "pasta%2Fdaniel%2Ffoto.jpg",
            "content://midia/bia.jpg%3Ftoken=SEGREDO",
            "bia.jpg%253Ftoken=SEGREDO"
        };
        foreach (var prefixo in prefixos)
        {
            foreach (var caminho in caminhos)
                fotos.Add(prefixo + caminho);
        }

        foreach (var foto in fotos)
        {
            var nome = ExportadorPlanilha.NomeDeFoto(foto);
            Assert.True(nome == EsperadoDaFoto(foto), foto + " deu " + nome);
            Assert.DoesNotContain("daniel", nome, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SEGREDO", nome, StringComparison.Ordinal);
        }

        using var fabrica = new TestDbContextFactory();
        using var contexto = fabrica.CreateDbContext();
        var quando = DateTime.UtcNow;
        for (var i = 0; i < fotos.Count; i++)
        {
            contexto.MembrosFamilia.Add(new MembroFamilia
            {
                Nome = $"Pessoa {i + 1}",
                FotoPath = fotos[i],
                CriadoEm = quando
            });
        }

        contexto.SaveChanges();
        using var destino = new MemoryStream();
        ExportadorPlanilha.Exportar(
            contexto,
            new ConfiguracaoExportavel { Url = "https://host/v1%3Ftoken=SENTINELA-TOKEN" },
            destino);
        var bytes = destino.ToArray();
        var compartilhadas = Parte(bytes, "xl/sharedStrings.xml");
        Assert.DoesNotContain("daniel", compartilhadas, StringComparison.Ordinal);
        Assert.DoesNotContain("SENTINELA", compartilhadas, StringComparison.Ordinal);
        Assert.DoesNotContain("SEGREDO", compartilhadas, StringComparison.Ordinal);
        var xml = XmlDasPartes(bytes);
        Assert.DoesNotContain("daniel", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("SEGREDO", xml, StringComparison.Ordinal);

        using var pasta = new XLWorkbook(destino);
        var aba = pasta.Worksheet("Perfis");
        for (var i = 0; i < fotos.Count; i++)
        {
            var esperado = EsperadoDaFoto(fotos[i]);
            var celula = aba.Cell(i + 2, 5);
            if (esperado.Length == 0)
                Assert.Equal(XLDataType.Blank, celula.DataType);
            else
                Assert.Equal(esperado, celula.GetText());
        }

        Assert.Equal("https://host", pasta.Worksheet("Configurações").Cell(8, 2).GetText());
    }

    [Fact]
    public void NomeDeFoto_SeparadorNaConsultaNaoVazaNoXml()
    {
        (string Foto, string Esperado)[] fotos =
        [
            ("bia.jpg?token=/SEGREDO", ""),
            ("bia.jpg%3Ftoken=%2FSEGREDO", ""),
            ("foto.jpg#/SEGREDO", ""),
            ("foto.jpg;x=/SEGREDO", ""),
            ("foto.jpg?a:SEGREDO", ""),
            ("foto.jpg?x=\\\\SEGREDO", ""),
            ("pasta/foto.jpg?x=/etc/SEGREDO.jpg", "")
        ];

        foreach (var (foto, esperado) in fotos)
        {
            var nome = ExportadorPlanilha.NomeDeFoto(foto);
            Assert.Equal(esperado, nome);
            Assert.DoesNotContain("SEGREDO", nome, StringComparison.Ordinal);
        }

        using var fabrica = new TestDbContextFactory();
        using var contexto = fabrica.CreateDbContext();
        var quando = DateTime.UtcNow;
        for (var i = 0; i < fotos.Length; i++)
        {
            contexto.MembrosFamilia.Add(new MembroFamilia
            {
                Nome = $"Pessoa {i + 1}",
                FotoPath = fotos[i].Foto,
                CriadoEm = quando
            });
        }

        contexto.SaveChanges();
        using var destino = new MemoryStream();
        ExportadorPlanilha.Exportar(contexto, new ConfiguracaoExportavel(), destino);
        var bytes = destino.ToArray();
        var xml = XmlDasPartes(bytes);
        Assert.DoesNotContain("SEGREDO", Parte(bytes, "xl/sharedStrings.xml"), StringComparison.Ordinal);
        Assert.DoesNotContain("SEGREDO", xml, StringComparison.Ordinal);

        using var pasta = new XLWorkbook(destino);
        var aba = pasta.Worksheet("Perfis");
        for (var i = 0; i < fotos.Length; i++)
        {
            var celula = aba.Cell(i + 2, 5);
            if (fotos[i].Esperado.Length == 0)
                Assert.Equal(XLDataType.Blank, celula.DataType);
            else
                Assert.Equal(fotos[i].Esperado, celula.GetText());
        }
    }

    private static string EsperadoDaFoto(string foto)
        => foto == "C:Users\\daniel\\foto.jpg" ? "foto.jpg" : "";

    [Fact]
    public void FotoComBrancoOuUriSemSegmento_NaoVazaPastaNemHost()
    {
        using var fabrica = new TestDbContextFactory();
        using var contexto = fabrica.CreateDbContext();
        var quando = DateTime.UtcNow;
        contexto.MembrosFamilia.AddRange(
            new MembroFamilia { Nome = "Ana", FotoPath = "pasta/foto-ana.jpg", CriadoEm = quando },
            new MembroFamilia { Nome = "Eva", FotoPath = "\n/SENTINELA-PASTA/eva.jpg", CriadoEm = quando },
            new MembroFamilia { Nome = "Lia", FotoPath = "\u00A0content://SENTINELA-HOST", CriadoEm = quando });
        contexto.SaveChanges();

        using var destino = new MemoryStream();
        ExportadorPlanilha.Exportar(contexto, new ConfiguracaoExportavel(), destino);
        var xml = XmlDasPartes(destino.ToArray());
        Assert.DoesNotContain("SENTINELA-PASTA", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("SENTINELA-HOST", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("content://", xml, StringComparison.Ordinal);

        using var pasta = new XLWorkbook(destino);
        var aba = pasta.Worksheet("Perfis");
        Assert.Equal("foto-ana.jpg", aba.Cell(2, 5).GetText());
        Assert.Equal(XLDataType.Blank, aba.Cell(3, 5).DataType);
        Assert.Equal(XLDataType.Blank, aba.Cell(4, 5).DataType);
    }

    [Fact]
    public void CriadoEmEmNovembroDe2018_UsaUtcMenosDuas()
    {
        using var fabrica = new TestDbContextFactory();
        using var contexto = fabrica.CreateDbContext();
        contexto.MembrosFamilia.Add(new MembroFamilia
        {
            Nome = "Vera",
            CriadoEm = new DateTime(2018, 11, 15, 3, 30, 45, DateTimeKind.Utc)
        });
        contexto.SaveChanges();

        using var destino = new MemoryStream();
        ExportadorPlanilha.Exportar(contexto, new ConfiguracaoExportavel(), destino);
        using var pasta = new XLWorkbook(destino);
        DataHora(pasta.Worksheet("Perfis").Cell(2, 6), new DateTime(2018, 11, 15, 1, 30, 0));
    }

    [Fact]
    public void CriadoEmComKindLocal_PassaPorUtcAntesDeSaoPaulo()
    {
        // UTC−10 fixo, não o fuso da máquina. Sem a conversão de Local o relógio
        // 10:00 seguiria como UTC e São Paulo (UTC−2 em nov/2018) marcaria 08:00.
        var fuso = TimeZoneInfo.CreateCustomTimeZone(
            "FusoDeTeste", TimeSpan.FromHours(-10), "Fuso de teste", "Fuso de teste");
        var local = new DateTime(2018, 11, 15, 10, 0, 0, DateTimeKind.Local);
        var utc = ExportadorPlanilha.InstanteUtc(local, fuso);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.Equal(new DateTime(2018, 11, 15, 20, 0, 0, DateTimeKind.Utc), utc);

        using var pasta = new XLWorkbook();
        var aba = PlanilhaPtBr.CriarAba(pasta, "Perfis");
        PlanilhaPtBr.GravarDataHora(aba.Cell(2, 1), utc);
        Assert.Equal(new DateTime(2018, 11, 15, 18, 0, 0), aba.Cell(2, 1).GetDateTime());
    }

    [Fact]
    public void HorarioLocalInexistente_ViraTextoENaoLanca()
    {
        var fuso = FusoComBuraco();
        var inexistente = new DateTime(2018, 11, 4, 2, 30, 45, DateTimeKind.Local);

        using var pasta = new XLWorkbook();
        var aba = PlanilhaPtBr.CriarAba(pasta, "Perfis");
        ExportadorPlanilha.GravarInstante(aba.Cell(2, 1), inexistente, fuso);
        using var destino = new MemoryStream();
        pasta.SaveAs(destino);
        destino.Position = 0;
        using var reaberta = new XLWorkbook(destino);
        var celula = reaberta.Worksheet("Perfis").Cell(2, 1);
        Assert.Equal(XLDataType.Text, celula.DataType);
        Assert.Equal("04/11/2018 02:30:45", celula.GetText());
        Assert.False(celula.HasFormula);
    }

    [Fact]
    public void HorarioLocalAmbiguo_UsaOffsetPadrao()
    {
        // 17/02 01:30 ocorre duas vezes: UTC−2 (verão) e UTC−3 (padrão).
        // A conversão fica no offset padrão, 04:30 UTC, e não nas 03:30 do verão.
        var fuso = FusoComBuraco();
        var ambiguo = new DateTime(2018, 2, 17, 1, 30, 0, DateTimeKind.Local);
        Assert.True(fuso.IsAmbiguousTime(DateTime.SpecifyKind(ambiguo, DateTimeKind.Unspecified)));

        var utc = ExportadorPlanilha.InstanteUtc(ambiguo, fuso);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.Equal(new DateTime(2018, 2, 17, 4, 30, 0, DateTimeKind.Utc), utc);

        using var pasta = new XLWorkbook();
        var aba = PlanilhaPtBr.CriarAba(pasta, "Perfis");
        PlanilhaPtBr.GravarDataHora(aba.Cell(2, 1), utc);
        Assert.Equal(new DateTime(2018, 2, 17, 2, 30, 0), aba.Cell(2, 1).GetDateTime());
    }

    private static TimeZoneInfo FusoComBuraco()
    {
        var ajuste = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            DateTime.MinValue.Date,
            DateTime.MaxValue.Date,
            TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 11, 4),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 2, 17));
        return TimeZoneInfo.CreateCustomTimeZone(
            "FusoComBuraco", TimeSpan.FromHours(-3), "Fuso com buraco", "Padrão", "Verão", [ajuste]);
    }

    [Fact]
    public void LeituraPaginada_TrazALinhaDaSegundaPagina()
    {
        using var fabrica = new TestDbContextFactory();
        using var contexto = fabrica.CreateDbContext();
        var total = ExportadorPlanilha.TamanhoLote + 1;
        for (var i = 0; i < total; i++)
        {
            contexto.Categorias.Add(new Categoria
            {
                Nome = $"Cat{i:000}",
                Icone = "A",
                CorHex = "#112233"
            });
        }

        contexto.SaveChanges();
        using var destino = new MemoryStream();
        ExportadorPlanilha.Exportar(contexto, new ConfiguracaoExportavel(), destino);
        using var pasta = new XLWorkbook(destino);
        var aba = pasta.Worksheet("Categorias");
        Assert.Equal(total + 1, aba.LastRowUsed()!.RowNumber());
        Assert.Equal("Cat000", aba.Cell(2, 1).GetText());
        Assert.Equal($"Cat{total - 1:000}", aba.Cell(total + 1, 1).GetText());
    }

    [Fact]
    public void AcimaDoTetoDeDados_FalhaComAContagem()
    {
        var teto = PlanilhaPtBr.MaximoLinhas - 1;
        ExportadorPlanilha.ExigirLimiteDeDados("Categorias", teto);
        var erro = Assert.Throws<InvalidOperationException>(
            () => ExportadorPlanilha.ExigirLimiteDeDados("Categorias", teto + 1));
        Assert.Contains("Categorias", erro.Message, StringComparison.Ordinal);
        Assert.Contains((teto + 1).ToString(), erro.Message, StringComparison.Ordinal);
        Assert.Contains(teto.ToString(), erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TodoValorDeEnumExportadoTemRotulo_EDesconhecidoFalha()
    {
        foreach (var papel in Enum.GetValues<PerfilRole>())
            Assert.False(string.IsNullOrWhiteSpace(ExportadorPlanilha.TextoPapel(papel)));
        foreach (var tipo in Enum.GetValues<TipoTransacao>())
            Assert.False(string.IsNullOrWhiteSpace(ExportadorPlanilha.TextoTipo(tipo)));
        foreach (var recorrencia in Enum.GetValues<TipoRecorrencia>())
            Assert.False(string.IsNullOrWhiteSpace(ExportadorPlanilha.TextoRecorrencia(recorrencia)));
        foreach (var natureza in Enum.GetValues<NaturezaItemLancamento>())
            Assert.False(string.IsNullOrWhiteSpace(ExportadorPlanilha.TextoNatureza(natureza)));
        foreach (var ativo in Enum.GetValues<TipoAtivo>())
            Assert.False(string.IsNullOrWhiteSpace(ExportadorPlanilha.TextoAtivo(ativo)));
        foreach (var operacao in Enum.GetValues<TipoOperacao>())
            Assert.False(string.IsNullOrWhiteSpace(ExportadorPlanilha.TextoOperacao(operacao)));
        foreach (var cartao in Enum.GetValues<TipoCartao>())
            Assert.False(string.IsNullOrWhiteSpace(ExportadorPlanilha.TextoCartao(cartao)));
        foreach (var beneficio in Enum.GetValues<TipoCartaoBeneficio>())
            Assert.False(string.IsNullOrWhiteSpace(ExportadorPlanilha.TextoBeneficio(beneficio)));
        foreach (var tema in Enum.GetValues<TemaPreferido>())
            Assert.False(string.IsNullOrWhiteSpace(ExportadorPlanilha.TextoTema(tema)));
        foreach (var prazo in Enum.GetValues<PrazoAviso>())
            _ = ExportadorPlanilha.TextoPrazos(prazo);

        Assert.Equal("", ExportadorPlanilha.TextoBeneficio(null));
        Assert.Throws<InvalidOperationException>(() => ExportadorPlanilha.TextoPapel((PerfilRole)99));
        Assert.Throws<InvalidOperationException>(() => ExportadorPlanilha.TextoTipo((TipoTransacao)99));
        Assert.Throws<InvalidOperationException>(() => ExportadorPlanilha.TextoRecorrencia((TipoRecorrencia)99));
        Assert.Throws<InvalidOperationException>(() => ExportadorPlanilha.TextoNatureza((NaturezaItemLancamento)99));
        Assert.Throws<InvalidOperationException>(() => ExportadorPlanilha.TextoAtivo((TipoAtivo)99));
        Assert.Throws<InvalidOperationException>(() => ExportadorPlanilha.TextoOperacao((TipoOperacao)99));
        Assert.Throws<InvalidOperationException>(() => ExportadorPlanilha.TextoCartao((TipoCartao)99));
        Assert.Throws<InvalidOperationException>(() => ExportadorPlanilha.TextoBeneficio((TipoCartaoBeneficio)99));
        Assert.Throws<InvalidOperationException>(() => ExportadorPlanilha.TextoTema((TemaPreferido)99));
        Assert.Throws<InvalidOperationException>(() => ExportadorPlanilha.TextoPrazos((PrazoAviso)32));
    }

    private static string XmlDasPartes(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var xml = new StringBuilder();
        foreach (var entrada in zip.Entries)
        {
            using var leitor = new StreamReader(entrada.Open(), Encoding.UTF8);
            xml.Append(leitor.ReadToEnd());
        }

        return xml.ToString();
    }

    private static string Parte(byte[] bytes, string nome)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var entrada = zip.GetEntry(nome);
        Assert.NotNull(entrada);
        using var leitor = new StreamReader(entrada!.Open(), Encoding.UTF8);
        return leitor.ReadToEnd();
    }

    private static Cartao Semear(AppDbContext contexto)
    {
        var criado = new DateTime(2026, 10, 19, 2, 30, 45, DateTimeKind.Utc);
        var perfil = new MembroFamilia
        {
            Nome = "Daniel",
            Apelido = "Dani",
            FotoPath = "foto-daniel.jpg",
            Role = PerfilRole.Admin,
            Ativo = true,
            CriadoEm = criado
        };
        var categoria = new Categoria
        {
            Nome = "Mercado",
            Icone = "🛒",
            CorHex = "#112233",
            TipoPadrao = TipoTransacao.Despesa,
            EhPadraoDoSistema = true,
            PermiteItensFatura = true,
            Ativo = true
        };
        var credito = new Cartao
        {
            Nome = "Nubank",
            Bandeira = "Mastercard",
            UltimosDigitos = "0012",
            Tipo = TipoCartao.Credito,
            Ativo = true,
            CriadoEm = criado
        };
        var beneficio = new Cartao
        {
            Nome = "VR",
            Bandeira = "VR",
            UltimosDigitos = "0099",
            Tipo = TipoCartao.Beneficio,
            TipoBeneficio = TipoCartaoBeneficio.ValeAlimentacao,
            Ativo = true,
            CriadoEm = criado
        };
        var origem = new Transacao
        {
            Descricao = "Aluguel modelo",
            Valor = 1500m,
            Data = new DateTime(2026, 9, 1),
            Tipo = TipoTransacao.Despesa,
            Recorrencia = TipoRecorrencia.Unica,
            Pago = true,
            Categoria = categoria,
            MembroFamilia = perfil,
            CriadoEm = criado
        };
        var filha = new Transacao
        {
            Descricao = "Aluguel de outubro",
            Valor = 10.50m,
            Data = new DateTime(2026, 10, 19),
            Tipo = TipoTransacao.Despesa,
            Recorrencia = TipoRecorrencia.Mensal,
            Pago = false,
            Observacao = "pago no cartão",
            Categoria = categoria,
            MembroFamilia = perfil,
            TransacaoOrigem = origem,
            Cartao = credito,
            CriadoEm = criado
        };
        var item = new ItemLancamento
        {
            Descricao = "Pão",
            Descritivo = "Padaria da esquina",
            Valor = 8.50m,
            EhEstorno = false,
            Natureza = NaturezaItemLancamento.Parcelada,
            ParcelaAtual = 2,
            TotalParcelas = 10,
            Ordem = 1,
            HashImportacao = SentinelaHash,
            Transacao = filha,
            Categoria = categoria
        };
        var ativo = new AtivoFinanceiro
        {
            Ticker = "PETR4",
            Nome = "Petrobras",
            Tipo = TipoAtivo.Acao,
            Ativo = true,
            CriadoEm = criado
        };
        var produto = new ProdutoCesta { Nome = "Arroz", Categoria = categoria, Ativo = true, CriadoEm = criado };
        var local = new LocalCompra { Nome = "Mercado Central", Ativo = true, CriadoEm = criado };
        var compra = new CompraCesta
        {
            Mes = 10,
            Ano = 2026,
            Data = new DateTime(2026, 10, 19),
            LocalCompra = local,
            ValorBeneficio = 20m,
            TipoCartaoBeneficio = TipoCartaoBeneficio.ValeAlimentacao,
            CartaoBeneficio = beneficio,
            ValorDinheiro = 4m,
            ValorCartaoCredito = 16m,
            CartaoCredito = credito,
            ItemFatura = item,
            Transacao = filha,
            CriadoEm = criado
        };

        contexto.AddRange(
            perfil, categoria, credito, beneficio, origem, filha, item, ativo, produto, local, compra,
            new OperacaoAtivo
            {
                Ativo = ativo,
                MembroFamilia = perfil,
                Tipo = TipoOperacao.Compra,
                Data = new DateTime(2026, 10, 2),
                Quantidade = 10m,
                PrecoUnitario = 36.50m,
                Custos = 1.25m,
                Corretora = "Clara",
                CriadoEm = criado,
                AtualizadoEm = criado
            },
            new CompraCestaItem { CompraCesta = compra, Produto = produto, Preco = 40m, Ordem = 1 },
            new Orcamento
            {
                Categoria = categoria,
                ValorLimite = 800m,
                MetaReducaoPercentual = 10m,
                Mes = 10,
                Ano = 2026,
                CriadoEm = criado
            },
            new Meta
            {
                Nome = "Reserva",
                ValorAlvo = 200m,
                ValorAtual = 50m,
                Prazo = new DateTime(2027, 1, 15),
                Icone = "🎯",
                Concluida = false,
                MembroFamilia = perfil,
                CriadoEm = criado
            },
            new FechamentoPeriodo { Mes = 9, Ano = 2026, Fechado = true, AtualizadoEm = criado },
            new RecargaCartao { Cartao = beneficio, Data = new DateTime(2026, 10, 1), Valor = 300m, CriadoEm = criado },
            new LogEvento
            {
                Tipo = TipoLogEvento.Sistema,
                Mensagem = SentinelaLog
                    + " chave " + SentinelaChave
                    + " uri content://" + SentinelaUri
                    + " senha " + SentinelaSenha,
                CriadoEm = criado
            });
        contexto.SaveChanges();

        contexto.Database.ExecuteSqlRaw(
            """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );
            """);
        contexto.Database.ExecuteSqlRaw(
            """INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ({0}, {1});""",
            SentinelaMigration,
            "10.0.11");

        return credito;
    }

    private static void Cabecalho(IXLWorksheet aba, params string[] colunas)
    {
        for (var i = 0; i < colunas.Length; i++)
            Assert.Equal(colunas[i], aba.Cell(1, i + 1).GetText());

        Assert.Equal(XLDataType.Blank, aba.Cell(1, colunas.Length + 1).DataType);
    }

    private static void Moeda(IXLCell celula, decimal esperado)
    {
        Assert.Equal(XLDataType.Number, celula.DataType);
        Assert.Equal(esperado, (decimal)celula.GetDouble());
        Assert.Equal(PlanilhaPtBr.FormatoMoeda, celula.Style.NumberFormat.Format);
    }

    private static void Numero(IXLCell celula, decimal esperado)
    {
        Assert.Equal(XLDataType.Number, celula.DataType);
        Assert.Equal(esperado, (decimal)celula.GetDouble());
        Assert.NotEqual(PlanilhaPtBr.FormatoMoeda, celula.Style.NumberFormat.Format);
    }

    private static void Data(IXLCell celula, DateTime dia)
    {
        Assert.Equal(XLDataType.DateTime, celula.DataType);
        Assert.Equal(dia.Date, celula.GetDateTime().Date);
        Assert.Equal(PlanilhaPtBr.FormatoData, celula.Style.DateFormat.Format);
    }

    private static void DataHora(IXLCell celula, DateTime instante)
    {
        Assert.Equal(XLDataType.DateTime, celula.DataType);
        Assert.Equal(instante, celula.GetDateTime());
        Assert.Equal(PlanilhaPtBr.FormatoDataHora, celula.Style.DateFormat.Format);
    }

    private static string Raiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OrcaFacil.Core", "Exportacao", "ExportadorPlanilha.cs")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar ExportadorPlanilha.cs a partir de " + AppContext.BaseDirectory);
    }
}
