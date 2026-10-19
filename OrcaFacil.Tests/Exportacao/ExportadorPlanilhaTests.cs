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
    private const string SentinelaFoto = "SENTINELA-FOTO";
    private const string SentinelaHash = "SENTINELA-HASH";
    private const string SentinelaLog = "SENTINELA-LOG";
    private const string SentinelaChave = "SENTINELA-CHAVE";
    private const string SentinelaUri = "SENTINELA-URI";
    private const string SentinelaSenha = "SENTINELA-SENHA";
    private const string SentinelaMigration = "SENTINELA-MIGRATION";
    private const string SentinelaToken = "SENTINELA-TOKEN";

    private static readonly string[] Sentinelas =
    [
        SentinelaFoto, SentinelaHash, SentinelaLog, SentinelaChave,
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
            ["Tema", "NotificacoesAtivas", "SomAtivo", "PrazosAviso", "Provedor", "Url", "Modelo"],
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
            Provedor = "Ollama",
            Url = "https://api.exemplo.test/v1?token=" + SentinelaToken,
            Modelo = "llama3"
        };

        using var destino = new MemoryStream();
        ExportadorPlanilha.Exportar(contexto, configuracao, destino);
        var bytes = destino.ToArray();
        var pacote = Encoding.Latin1.GetString(bytes);
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
        Cabecalho(perfis, "Nome", "Apelido", "Papel", "Ativo", "Tem foto", "Criado em");
        Assert.Equal("Daniel", perfis.Cell(2, 1).GetText());
        Assert.Equal("Dani", perfis.Cell(2, 2).GetText());
        Assert.Equal("Administrador", perfis.Cell(2, 3).GetText());
        Assert.Equal(PlanilhaPtBr.Sim, perfis.Cell(2, 4).GetText());
        Assert.Equal(PlanilhaPtBr.Sim, perfis.Cell(2, 5).GetText());
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
            "Categoria", "Perfil", "Lançamento de origem", "Cartão");
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
        Assert.Equal("3 dias antes do vencimento, No dia do vencimento", config.Cell(5, 2).GetText());
        Assert.Equal("Ollama", config.Cell(6, 2).GetText());
        Assert.Equal("https://api.exemplo.test/v1", config.Cell(7, 2).GetText());
        Assert.Equal("llama3", config.Cell(8, 2).GetText());

        var fonte = File.ReadAllText(Path.Combine(Raiz(), "OrcaFacil.Core", "Exportacao", "ExportadorPlanilha.cs"));
        Assert.DoesNotContain("AdjustToContents", fonte, StringComparison.Ordinal);
    }

    private static Cartao Semear(AppDbContext contexto)
    {
        var criado = new DateTime(2026, 10, 19, 2, 30, 45, DateTimeKind.Utc);
        var perfil = new MembroFamilia
        {
            Nome = "Daniel",
            Apelido = "Dani",
            FotoPath = "content://" + SentinelaFoto + "/foto.jpg",
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
