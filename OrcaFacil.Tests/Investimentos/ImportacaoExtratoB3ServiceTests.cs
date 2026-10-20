using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OrcaFacil.Core.B3;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Services;

namespace OrcaFacil.Tests.Investimentos;

public class ImportacaoExtratoB3ServiceTests
{
    [Fact]
    public void Hash_UsaOsCamposEstaveis_ComDecimalInvariant()
    {
        var linha = new LinhaExtratoB3(
            2,
            DirecaoB3.Credito,
            new DateOnly(2026, 9, 16),
            "APLICAÇÃO",
            TipoOperacaoB3.Aplicacao,
            "CDB9268YCA7",
            "CDB - CDB9268YCA7",
            ClasseAtivoB3.RendaFixa,
            "ITAU UNIBANCO S.A.",
            5002m,
            1.4909m,
            5002.00m);

        var texto = HashLinhaExtratoB3.Texto(linha);
        Assert.Equal("2026-09-16|Aplicacao|CDB9268YCA7|5002|5002.00|ITAU UNIBANCO S.A.|2", texto);
        var esperado = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto))).ToLowerInvariant();
        Assert.Equal(esperado, HashLinhaExtratoB3.Calcular(linha));
        Assert.Equal(64, esperado.Length);

        var outraLinha = linha with { Linha = 3 };
        Assert.NotEqual(HashLinhaExtratoB3.Calcular(linha), HashLinhaExtratoB3.Calcular(outraLinha));
    }

    [Fact]
    public async Task PreviaDoModelo_ContaOsStatus()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var previa = await ambiente.PreverModeloAsync();

        Assert.Equal(98, previa.Linhas.Count);
        Assert.Equal(0, previa.Contar(StatusLinhaImportacaoB3.Erro));
        Assert.Equal(1, previa.Contar(StatusLinhaImportacaoB3.Informativa));
        Assert.Equal(1, previa.Contar(StatusLinhaImportacaoB3.SemPosicao));
        Assert.Equal(0, previa.Contar(StatusLinhaImportacaoB3.MesFechado));
        Assert.Equal(0, previa.Contar(StatusLinhaImportacaoB3.DuplicataImportada));
        Assert.Equal(0, previa.Contar(StatusLinhaImportacaoB3.PossivelDuplicataManual));
        Assert.Equal(96, previa.Contar(StatusLinhaImportacaoB3.Nova));

        var semPosicao = Assert.Single(previa.Linhas, l => l.Status == StatusLinhaImportacaoB3.SemPosicao);
        Assert.Equal("CDBC25AQGPU", semPosicao.Titulo);
        Assert.False(semPosicao.Marcada);
        Assert.False(semPosicao.PodeMarcar);

        var informativa = Assert.Single(previa.Linhas, l => l.Status == StatusLinhaImportacaoB3.Informativa);
        Assert.Contains("BOVA11", informativa.Titulo, StringComparison.Ordinal);
        Assert.False(informativa.Marcada);

        Assert.All(previa.Linhas.Where(l => l.Status == StatusLinhaImportacaoB3.Nova), l => Assert.True(l.Marcada));
        Assert.Contains(previa.Linhas, l => l.Titulo == "SNAG11" && l.Comando!.TipoAtivo == TipoAtivo.Fii);
    }

    [Fact]
    public async Task SegundaImportacao_LinhasGravadasFicamDuplicata()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var primeira = await ambiente.PreverModeloAsync();
        var gravadas = await ambiente.Service.GravarAsync(primeira, ambiente.MembroId);

        var segunda = await ambiente.PreverModeloAsync();
        Assert.Equal(gravadas, segunda.Contar(StatusLinhaImportacaoB3.DuplicataImportada));
        Assert.Equal(0, segunda.Contar(StatusLinhaImportacaoB3.Nova));
        Assert.Equal(1, segunda.Contar(StatusLinhaImportacaoB3.Informativa));
        Assert.Equal(1, segunda.Contar(StatusLinhaImportacaoB3.SemPosicao));
        Assert.All(segunda.Linhas.Where(l => l.Status == StatusLinhaImportacaoB3.DuplicataImportada), l =>
        {
            Assert.False(l.Marcada);
            Assert.False(l.PodeMarcar);
        });
    }

    [Fact]
    public async Task ManualIgual_ViraPossivelDuplicata_DesmarcadaEPodeMarcar()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var previa = await ambiente.PreverModeloAsync();
        var compra = previa.Linhas.First(l =>
            l.Comando?.Destino == DestinoImportacaoB3.OperacaoAtivo
            && l.Comando.TipoOperacao == TipoOperacao.Compra);
        var comando = compra.Comando!;

        await using (var contexto = await ambiente.Factory.CreateDbContextAsync())
        {
            var ativo = new AtivoFinanceiro
            {
                Ticker = comando.Ticker,
                Nome = comando.Nome,
                Tipo = comando.TipoAtivo
            };
            contexto.AtivosFinanceiros.Add(ativo);
            await contexto.SaveChangesAsync();
            await ambiente.Resultado.SalvarAsync(new OperacaoAtivoFormModel
            {
                AtivoId = ativo.Id,
                Tipo = comando.TipoOperacao,
                Data = comando.Data,
                Quantidade = comando.Quantidade,
                PrecoUnitario = comando.PrecoUnitario,
                ValorOperacao = comando.Valor,
                Origem = OrigemOperacaoAtivo.Manual
            }, ambiente.MembroId);
        }

        var deNovo = await ambiente.PreverModeloAsync();
        var linha = deNovo.Linhas.Single(l => l.Numero == compra.Numero);
        Assert.Equal(StatusLinhaImportacaoB3.PossivelDuplicataManual, linha.Status);
        Assert.False(linha.Marcada);
        Assert.True(linha.PodeMarcar);

        linha.Marcada = true;
        var reclassificada = await ambiente.Service.ReclassificarAsync(deNovo, ambiente.MembroId);
        var marcada = reclassificada.Linhas.Single(l => l.Numero == compra.Numero);
        Assert.Equal(StatusLinhaImportacaoB3.PossivelDuplicataManual, marcada.Status);
        Assert.True(marcada.Marcada);
    }

    [Fact]
    public async Task MesFechado_BloqueiaENaoGrava()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        await ambiente.FecharAsync(4, 2025);
        var previa = await ambiente.PreverModeloAsync();

        Assert.True(previa.Contar(StatusLinhaImportacaoB3.MesFechado) > 0);
        Assert.All(previa.Linhas.Where(l => l.Status == StatusLinhaImportacaoB3.MesFechado), l =>
        {
            Assert.False(l.Marcada);
            Assert.False(l.PodeMarcar);
        });

        await ambiente.Service.GravarAsync(previa, ambiente.MembroId);

        await using var contexto = await ambiente.Factory.CreateDbContextAsync();
        Assert.DoesNotContain(await contexto.Transacoes.ToListAsync(), t => t.Data.Year == 2025 && t.Data.Month == 4);
        Assert.DoesNotContain(await contexto.TitulosRendaFixa.Include(t => t.Operacoes).ToListAsync(),
            t => t.Operacoes.Any(o => o.Data.Year == 2025 && o.Data.Month == 4 && o.Tipo == TipoOperacaoRendaFixa.Resgate));
    }

    [Fact]
    public async Task MesFechadoDepoisDaPrevia_NaoGravaNada()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var previa = await ambiente.PreverModeloAsync();
        Assert.Contains(previa.Linhas, l => l.Marcada && l.Comando!.Data.Year == 2025 && l.Comando.Data.Month == 4);
        await ambiente.FecharAsync(4, 2025);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ambiente.Service.GravarAsync(previa, ambiente.MembroId));
        Assert.Equal(ImportacaoExtratoB3Service.MensagemNadaGravado, ex.Message);

        await using var contexto = await ambiente.Factory.CreateDbContextAsync();
        Assert.Empty(await contexto.OperacoesAtivo.ToListAsync());
        Assert.Empty(await contexto.Proventos.ToListAsync());
        Assert.Empty(await contexto.OperacoesRendaFixa.ToListAsync());
        Assert.Empty(await contexto.Transacoes.ToListAsync());
    }

    [Fact]
    public async Task Gravar_Nikos893_Tesouro1928_ESemPosicaoNaoEntra()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var previa = await ambiente.PreverModeloAsync();
        await ambiente.Service.GravarAsync(previa, ambiente.MembroId);

        await using var contexto = await ambiente.Factory.CreateDbContextAsync();
        var lancamentos = await contexto.Transacoes.ToListAsync();
        var nikos = lancamentos.Where(t => t.Data.Year == 2025 && t.Data.Month == 4).ToList();
        Assert.Equal(9, nikos.Count);
        Assert.All(nikos, t => Assert.Equal(TipoTransacao.Receita, t.Tipo));
        Assert.Equal(8.93m, nikos.Sum(t => t.Valor));

        var tesouro = lancamentos.Where(t => t.Data.Year == 2023 && t.Data.Month == 5).ToList();
        Assert.Equal(3, tesouro.Count);
        Assert.All(tesouro, t => Assert.Equal(TipoTransacao.Receita, t.Tipo));
        Assert.Equal(19.28m, tesouro.Sum(t => t.Valor));

        Assert.DoesNotContain(await contexto.TitulosRendaFixa.ToListAsync(), t => t.Codigo == "CDBC25AQGPU");
        Assert.All(await contexto.OperacoesAtivo.ToListAsync(), o =>
        {
            Assert.Equal(OrigemOperacaoAtivo.ExtratoB3, o.Origem);
            Assert.False(string.IsNullOrWhiteSpace(o.HashImportacao));
        });
        Assert.All(await contexto.Proventos.ToListAsync(), p =>
        {
            Assert.Equal(OrigemProvento.ExtratoB3, p.Origem);
            Assert.False(string.IsNullOrWhiteSpace(p.HashImportacao));
        });
        Assert.Contains(await contexto.AtivosFinanceiros.ToListAsync(), a => a.Ticker == "SNAG11" && a.Tipo == TipoAtivo.Fii);
    }

    [Fact]
    public async Task Gravar_IgualaReceitaEDespesaDoLancamentoManual()
    {
        await using var importado = await Ambiente.CriarAsync();
        var previa = await importado.PreverModeloAsync();
        await importado.Service.GravarAsync(previa, importado.MembroId);
        var (receitaImportada, despesaImportada) = await TotaisAsync(importado.Factory);

        await using var manual = await Ambiente.CriarAsync();
        await RepetirManualAsync(manual, previa);
        var (receitaManual, despesaManual) = await TotaisAsync(manual.Factory);

        Assert.Equal(receitaManual, receitaImportada);
        Assert.Equal(despesaManual, despesaImportada);
        Assert.True(receitaImportada > 0);
    }

    [Fact]
    public async Task FalhaNoMeio_DesfazTudo()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var falha = new FalhaNoMeio();
        var opcoes = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var opcoesFalha = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(falha).Options;

        int membroId;
        await using (var contexto = new AppDbContext(opcoes))
        {
            contexto.Database.EnsureCreated();
            var membro = new MembroFamilia { Nome = "Usuário" };
            contexto.MembrosFamilia.Add(membro);
            await contexto.SaveChangesAsync();
            membroId = membro.Id;
        }

        var fabrica = new Fabrica(opcoesFalha);
        var servico = new ImportacaoExtratoB3Service(
            new ParserExtratoB3(),
            fabrica,
            new ResultadoRealizadoService(fabrica),
            new ProventoService(fabrica),
            new RendaFixaService(fabrica));

        PreviaExtratoB3 previa;
        await using (var csv = File.OpenRead(CaminhoCsv()))
            previa = await servico.PreverAsync(csv, membroId);

        falha.Armado = true;
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => servico.GravarAsync(previa, membroId));
        Assert.Equal(ImportacaoExtratoB3Service.MensagemFalha, ex.Message);

        await using var conferido = new AppDbContext(opcoes);
        Assert.Empty(await conferido.OperacoesAtivo.ToListAsync());
        Assert.Empty(await conferido.Proventos.ToListAsync());
        Assert.Empty(await conferido.OperacoesRendaFixa.ToListAsync());
        Assert.Empty(await conferido.Transacoes.ToListAsync());
        Assert.Empty(await conferido.AtivosFinanceiros.ToListAsync());
        Assert.Empty(await conferido.TitulosRendaFixa.ToListAsync());
    }

    private static async Task RepetirManualAsync(Ambiente ambiente, PreviaExtratoB3 previa)
    {
        var linhas = previa.Linhas
            .Where(l => l.Marcada && l.Comando is not null)
            .OrderBy(l => l.Comando!.Data)
            .ThenBy(l => l.Comando!.Destino == DestinoImportacaoB3.Provento ? 2
                : l.Comando!.Destino == DestinoImportacaoB3.RendaFixa
                    ? (l.Comando.TipoRendaFixa == TipoOperacaoRendaFixa.Aplicacao ? 0 : 1)
                    : (l.Comando!.TipoOperacao == TipoOperacao.Compra ? 0 : 1))
            .ThenBy(l => l.Numero)
            .ToList();

        var ativos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var titulos = new Dictionary<(string, string), int>();
        foreach (var linha in linhas)
        {
            var comando = linha.Comando!;
            if (comando.Destino == DestinoImportacaoB3.OperacaoAtivo)
            {
                var ativoId = await GarantirAtivoManualAsync(ambiente, ativos, comando);
                await ambiente.Resultado.SalvarAsync(new OperacaoAtivoFormModel
                {
                    AtivoId = ativoId,
                    Tipo = comando.TipoOperacao,
                    Data = comando.Data,
                    Quantidade = comando.Quantidade,
                    PrecoUnitario = comando.PrecoUnitario,
                    ValorOperacao = comando.Valor,
                    Corretora = comando.Corretora,
                    Origem = OrigemOperacaoAtivo.Manual
                }, ambiente.MembroId);
            }
            else if (comando.Destino == DestinoImportacaoB3.Provento)
            {
                var ativoId = await GarantirAtivoManualAsync(ambiente, ativos, comando);
                await ambiente.Provento.SalvarAsync(new ProventoFormModel
                {
                    AtivoId = ativoId,
                    Tipo = comando.TipoProvento,
                    DataPagamento = comando.Data,
                    ValorLiquido = comando.Valor,
                    Origem = OrigemProvento.Manual
                }, ambiente.MembroId);
            }
            else if (comando.Destino == DestinoImportacaoB3.RendaFixa)
            {
                var tituloId = await GarantirTituloManualAsync(ambiente, titulos, comando);
                await ambiente.RendaFixa.SalvarAsync(new OperacaoRendaFixaFormModel
                {
                    TituloRendaFixaId = tituloId,
                    Data = comando.Data,
                    Tipo = comando.TipoRendaFixa,
                    Quantidade = comando.Quantidade,
                    PrecoUnitario = comando.PrecoUnitario,
                    Valor = comando.Valor,
                    Origem = OrigemOperacaoRendaFixa.Manual
                }, ambiente.MembroId);
            }
        }
    }

    private static async Task<int> GarantirAtivoManualAsync(
        Ambiente ambiente,
        Dictionary<string, int> cache,
        ComandoImportacaoB3 comando)
    {
        if (cache.TryGetValue(comando.Ticker, out var id))
            return id;
        await using var contexto = await ambiente.Factory.CreateDbContextAsync();
        var ativo = new AtivoFinanceiro { Ticker = comando.Ticker, Nome = comando.Nome, Tipo = comando.TipoAtivo };
        contexto.AtivosFinanceiros.Add(ativo);
        await contexto.SaveChangesAsync();
        cache[comando.Ticker] = ativo.Id;
        return ativo.Id;
    }

    private static async Task<int> GarantirTituloManualAsync(
        Ambiente ambiente,
        Dictionary<(string, string), int> cache,
        ComandoImportacaoB3 comando)
    {
        var chave = (comando.Ticker, comando.Corretora);
        if (cache.TryGetValue(chave, out var id))
            return id;
        await using var contexto = await ambiente.Factory.CreateDbContextAsync();
        var titulo = new TituloRendaFixa
        {
            Tipo = comando.TipoTitulo,
            Codigo = comando.Ticker,
            Nome = comando.Nome,
            Emissor = comando.Emissor,
            AnoVencimento = comando.AnoVencimento,
            Corretora = comando.Corretora
        };
        contexto.TitulosRendaFixa.Add(titulo);
        await contexto.SaveChangesAsync();
        cache[chave] = titulo.Id;
        return titulo.Id;
    }

    private static async Task<(decimal Receita, decimal Despesa)> TotaisAsync(TestDbContextFactory factory)
    {
        await using var contexto = await factory.CreateDbContextAsync();
        var lancamentos = await contexto.Transacoes.ToListAsync();
        return (
            lancamentos.Where(t => t.Tipo == TipoTransacao.Receita).Sum(t => t.Valor),
            lancamentos.Where(t => t.Tipo == TipoTransacao.Despesa).Sum(t => t.Valor));
    }

    private static string CaminhoCsv()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var noProjeto = Path.Combine(dir.FullName, "Fixtures", "B3", "extrato-b3-modelo.csv");
            if (File.Exists(noProjeto))
                return noProjeto;
            var naRaiz = Path.Combine(dir.FullName, "OrcaFacil.Tests", "Fixtures", "B3", "extrato-b3-modelo.csv");
            if (File.Exists(naRaiz))
                return naRaiz;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Fixture do extrato B3 não encontrada.");
    }

    private sealed class FalhaNoMeio : SaveChangesInterceptor
    {
        public bool Armado { get; set; }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            if (Armado)
                throw new IOException("falha no meio do SaveChanges");
            return new ValueTask<int>(result);
        }
    }

    private sealed class Fabrica(DbContextOptions<AppDbContext> opcoes) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(opcoes);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class Ambiente : IAsyncDisposable
    {
        public TestDbContextFactory Factory { get; }
        public ImportacaoExtratoB3Service Service { get; }
        public ResultadoRealizadoService Resultado { get; }
        public ProventoService Provento { get; }
        public RendaFixaService RendaFixa { get; }
        public int MembroId { get; }

        private Ambiente(
            TestDbContextFactory factory,
            ImportacaoExtratoB3Service service,
            ResultadoRealizadoService resultado,
            ProventoService provento,
            RendaFixaService rendaFixa,
            int membroId)
        {
            Factory = factory;
            Service = service;
            Resultado = resultado;
            Provento = provento;
            RendaFixa = rendaFixa;
            MembroId = membroId;
        }

        public static async Task<Ambiente> CriarAsync()
        {
            var factory = new TestDbContextFactory();
            await using var contexto = await factory.CreateDbContextAsync();
            var membro = new MembroFamilia { Nome = "Usuário" };
            contexto.MembrosFamilia.Add(membro);
            await contexto.SaveChangesAsync();
            var resultado = new ResultadoRealizadoService(factory);
            var provento = new ProventoService(factory);
            var renda = new RendaFixaService(factory);
            var servico = new ImportacaoExtratoB3Service(new ParserExtratoB3(), factory, resultado, provento, renda);
            return new Ambiente(factory, servico, resultado, provento, renda, membro.Id);
        }

        public async Task<PreviaExtratoB3> PreverModeloAsync()
        {
            await using var csv = File.OpenRead(CaminhoCsv());
            return await Service.PreverAsync(csv, MembroId);
        }

        public async Task FecharAsync(int mes, int ano)
        {
            await using var contexto = await Factory.CreateDbContextAsync();
            contexto.FechamentosPeriodo.Add(new FechamentoPeriodo { Mes = mes, Ano = ano, Fechado = true });
            await contexto.SaveChangesAsync();
        }

        public ValueTask DisposeAsync()
        {
            Factory.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
