using OrcaFacil.Core.Exportacao;
using OrcaFacil.Core.Llm;

namespace OrcaFacil.Tests.Exportacao;

public class OrquestradorExportacaoPlanilhaTests
{
    private const string Sufixo = "ab12cd34";

    [Fact]
    public async Task SegundaChamadaEnquantoGrava_EIgnorada()
    {
        using var pasta = Pasta();
        var orquestrador = Novo();
        var segure = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entrou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entradas = 0;
        var viuFlag = false;

        Task<ResultadoExportacao> Chamar() => orquestrador.ExportarAsync(
            () => new ConfiguracaoExportavel(),
            () => pasta.Cache,
            (caminho, _) =>
            {
                Interlocked.Increment(ref entradas);
                viuFlag = orquestrador.Exportando;
                entrou.TrySetResult();
                File.WriteAllText(caminho, "x");
                segure.Task.Wait(TimeSpan.FromSeconds(8));
            },
            _ => Task.FromResult(DesfechoCompartilhamentoPlanilha.FolhaAberta));

        var primeira = Chamar();
        var segunda = Chamar();
        try
        {
            Assert.True(segunda.IsCompletedSuccessfully);
            var ignorada = await segunda;
            Assert.False(ignorada.Executou);
            await entrou.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, Volatile.Read(ref entradas));
            Assert.True(viuFlag);
            Assert.True(orquestrador.Exportando);
        }
        finally
        {
            segure.TrySetResult();
            await primeira;
            if (!segunda.IsCompleted)
                await segunda.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.False(orquestrador.Exportando);
        var seguinte = await Chamar();
        Assert.True(seguinte.Executou);
        Assert.Equal(2, Volatile.Read(ref entradas));
        Assert.False(orquestrador.Exportando);
    }

    [Fact]
    public async Task GravacaoRodaForaDaThreadDeQuemChamou()
    {
        using var pasta = Pasta();
        var orquestrador = Novo();
        var chamador = 0;
        var gravacao = 0;
        Exception? erro = null;
        using var feito = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            try
            {
                chamador = Environment.CurrentManagedThreadId;
                orquestrador.ExportarAsync(
                    () => new ConfiguracaoExportavel(),
                    () => pasta.Cache,
                    (caminho, _) =>
                    {
                        gravacao = Environment.CurrentManagedThreadId;
                        File.WriteAllText(caminho, "x");
                    },
                    _ => Task.FromResult(DesfechoCompartilhamentoPlanilha.FolhaAberta)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                erro = ex;
            }
            finally
            {
                feito.Set();
            }
        })
        {
            IsBackground = true
        };

        thread.Start();
        Assert.True(feito.Wait(TimeSpan.FromSeconds(10)));
        Assert.Null(erro?.ToString());
        Assert.NotEqual(0, gravacao);
        Assert.NotEqual(chamador, gravacao);
    }

    [Fact]
    public async Task ProvedorDesconhecido_SoltaAFlag_EMostraTextoEmPortugues()
    {
        using var pasta = Pasta();
        var orquestrador = Novo();
        var leuCache = false;
        var antigo = Path.Combine(pasta.Cache, ArquivoExportacaoPlanilha.Nome(new DateTime(2020, 1, 1), "11111111"));
        var csv = Path.Combine(pasta.Cache, "notas.csv");
        File.WriteAllText(antigo, "antigo");
        File.WriteAllText(csv, "csv");

        ResultadoExportacao resultado;
        try
        {
            resultado = await orquestrador.ExportarAsync(
                () =>
                {
                    _ = ChipsInteligencia.Texto((ProvedorIa)99);
                    return new ConfiguracaoExportavel();
                },
                () =>
                {
                    leuCache = true;
                    return pasta.Cache;
                },
                (_, _) => { },
                _ => Task.FromResult(DesfechoCompartilhamentoPlanilha.FolhaAberta));
        }
        catch (Exception ex)
        {
            Assert.Fail(ex.GetType().Name);
            return;
        }

        Assert.False(leuCache);
        Assert.True(resultado.Executou);
        Assert.False(orquestrador.Exportando);
        Assert.Equal(MensagemExportacaoPlanilha.Generica, resultado.Aviso);
        Assert.DoesNotContain("ArgumentOutOfRange", resultado.Aviso, StringComparison.Ordinal);
        Assert.DoesNotContain("exportado", resultado.Aviso, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("antigo", File.ReadAllText(antigo));
        Assert.Equal("csv", File.ReadAllText(csv));

        var gravou = false;
        var seguinte = await orquestrador.ExportarAsync(
            () => new ConfiguracaoExportavel(),
            () => pasta.Cache,
            (caminho, _) =>
            {
                gravou = true;
                File.WriteAllText(caminho, "ok");
            },
            _ => Task.FromResult(DesfechoCompartilhamentoPlanilha.FolhaAberta));

        Assert.True(gravou);
        Assert.True(seguinte.Executou);
        Assert.False(orquestrador.Exportando);
    }

    [Fact]
    public async Task CacheIndisponivel_ViraMensagemEmPortugues_ESoltaAFlag()
    {
        var orquestrador = Novo();
        ResultadoExportacao resultado;
        try
        {
            resultado = await orquestrador.ExportarAsync(
                () => new ConfiguracaoExportavel(),
                () => throw new InvalidOperationException("FileSystem.CacheDirectory"),
                (_, _) => { },
                _ => Task.FromResult(DesfechoCompartilhamentoPlanilha.FolhaAberta));
        }
        catch (Exception ex)
        {
            Assert.Fail(ex.GetType().Name);
            return;
        }

        Assert.Equal(MensagemExportacaoPlanilha.Generica, resultado.Aviso);
        Assert.False(orquestrador.Exportando);
        Assert.Null(resultado.Caminho);
    }

    [Fact]
    public async Task FolhaAberta_MantemOArquivo_ENaoDizExportado()
    {
        using var pasta = Pasta();
        var orquestrador = Novo();
        var csv = Path.Combine(pasta.Cache, "notas.csv");
        File.WriteAllText(csv, "csv");

        var resultado = await orquestrador.ExportarAsync(
            () => new ConfiguracaoExportavel(),
            () => pasta.Cache,
            (caminho, _) =>
            {
                File.WriteAllText(caminho, "planilha");
                Assert.True(ArquivoExportacaoPlanilha.EstaReservado(caminho));
                ArquivoExportacaoPlanilha.ApagarAnteriores(pasta.Cache);
                Assert.True(File.Exists(caminho));
            },
            caminho =>
            {
                Assert.True(File.Exists(caminho));
                return Task.FromResult(DesfechoCompartilhamentoPlanilha.FolhaAberta);
            });

        Assert.True(resultado.FolhaAberta);
        Assert.Equal(MensagemExportacaoPlanilha.ProntaParaCompartilhar, resultado.Aviso);
        Assert.Equal("orcafacil-20261019-163007-ab12cd34.xlsx", Path.GetFileName(resultado.Caminho));
        Assert.Equal("planilha", File.ReadAllText(resultado.Caminho!));
        Assert.Equal("csv", File.ReadAllText(csv));
        Assert.False(ArquivoExportacaoPlanilha.EstaReservado(resultado.Caminho));
        Assert.False(orquestrador.Exportando);
    }

    [Fact]
    public async Task CancelarOuFalharOCompartilhamento_NaoApagaOArquivo()
    {
        using var pasta = Pasta();
        var sufixos = new Queue<string>(["aaaaaaaa", "bbbbbbbb"]);
        var orquestrador = Novo(() => sufixos.Dequeue());

        var cancelado = await Exportar(orquestrador, pasta.Cache, DesfechoCompartilhamentoPlanilha.Cancelado);
        Assert.Null(cancelado.Aviso);
        Assert.False(cancelado.FolhaAberta);
        Assert.True(File.Exists(cancelado.Caminho));

        var falhou = await Exportar(orquestrador, pasta.Cache, DesfechoCompartilhamentoPlanilha.Falhou);
        Assert.Equal(MensagemExportacaoPlanilha.Compartilhamento, falhou.Aviso);
        Assert.DoesNotContain("exportado", falhou.Aviso, StringComparison.OrdinalIgnoreCase);
        Assert.False(falhou.FolhaAberta);
        Assert.False(File.Exists(cancelado.Caminho));
        Assert.True(File.Exists(falhou.Caminho));
    }

    [Fact]
    public async Task FalhaNaGravacao_RemoveSoOParcial()
    {
        using var pasta = Pasta();
        var orquestrador = Novo();
        var csv = Path.Combine(pasta.Cache, "notas.csv");
        var fora = Path.Combine(pasta.Raiz, "fora.txt");
        var espelho = Path.Combine(pasta.Cache, "orcafacil-espelho");
        Directory.CreateDirectory(espelho);
        var aninhado = Path.Combine(espelho, ArquivoExportacaoPlanilha.Nome(new DateTime(2026, 10, 19, 16, 30, 7), "ccccccc1"));
        File.WriteAllText(csv, "csv");
        File.WriteAllText(fora, "fora");
        File.WriteAllText(aninhado, "aninhado");

        string? parcial = null;
        var compartilhou = false;
        var resultado = await orquestrador.ExportarAsync(
            () => new ConfiguracaoExportavel(),
            () => pasta.Cache,
            (caminho, _) =>
            {
                parcial = caminho;
                File.WriteAllText(caminho, "parcial");
                File.WriteAllText(Path.Combine(pasta.Cache, "orcafacil-20000101-000000-aaaaaaaa.xlsx"), "irmao");
                throw new IOException("disco cheio /tmp/segredo.xlsx");
            },
            _ =>
            {
                compartilhou = true;
                return Task.FromResult(DesfechoCompartilhamentoPlanilha.FolhaAberta);
            });

        Assert.False(compartilhou);
        Assert.False(File.Exists(parcial));
        Assert.Equal("irmao", File.ReadAllText(Path.Combine(pasta.Cache, "orcafacil-20000101-000000-aaaaaaaa.xlsx")));
        Assert.Equal("csv", File.ReadAllText(csv));
        Assert.Equal("fora", File.ReadAllText(fora));
        Assert.Equal("aninhado", File.ReadAllText(aninhado));
        Assert.Equal(MensagemExportacaoPlanilha.Generica, resultado.Aviso);
        Assert.DoesNotContain("segredo", resultado.Aviso, StringComparison.Ordinal);
        Assert.False(orquestrador.Exportando);
        Assert.False(ArquivoExportacaoPlanilha.EstaReservado(parcial));
    }

    private static async Task<ResultadoExportacao> Exportar(
        OrquestradorExportacaoPlanilha orquestrador,
        string cache,
        DesfechoCompartilhamentoPlanilha desfecho)
        => await orquestrador.ExportarAsync(
            () => new ConfiguracaoExportavel(),
            () => cache,
            (caminho, _) => File.WriteAllText(caminho, "planilha"),
            _ => Task.FromResult(desfecho));

    private static OrquestradorExportacaoPlanilha Novo(Func<string>? sufixo = null)
        => new(
            new RelogioFixo(new DateTimeOffset(2026, 10, 19, 16, 30, 7, TimeSpan.Zero)),
            sufixo ?? (() => Sufixo));

    private static Temporario Pasta() => new();

    private sealed class RelogioFixo(DateTimeOffset utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utc;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class Temporario : IDisposable
    {
        public Temporario()
        {
            Raiz = Directory.CreateTempSubdirectory("orca-orquestrador-").FullName;
            Cache = Path.Combine(Raiz, "cache");
            Directory.CreateDirectory(Cache);
        }

        public string Raiz { get; }

        public string Cache { get; }

        public void Dispose() => Directory.Delete(Raiz, recursive: true);
    }
}
