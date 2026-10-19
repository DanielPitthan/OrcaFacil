using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Armazenamento;
using OrcaFacil.Core.Armazenamento.Espelho;
using OrcaFacil.Core.Armazenamento.Espelho.Pasta;
using OrcaFacil.Core.Armazenamento.Partida;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Tests.Armazenamento;

/// <summary>
/// Partida com SQLite de verdade. Falha não semeia e não apaga banco, backup nem espelho.
/// </summary>
public class PartidaArmazenamentoRealTests : IClassFixture<PartidaArmazenamentoRealTests.EspelhosReais>
{
    private const string SenhaErrada = "OutraSenha12";
    private readonly EspelhosReais _espelhos;

    public PartidaArmazenamentoRealTests(EspelhosReais espelhos) => _espelhos = espelhos;

    [Fact]
    public async Task BancoIntegro_AbreSemRestaurar_ENaoSemeia()
    {
        using var area = new Area();
        await CriarMarinaAsync(area.Banco, foto: null);
        var backup = area.GravarBackup([7, 7, 7]);
        var volume = VolumeComFoto();
        var semeadura = new SemeaduraMarcador(area.Banco);
        var limpeza = new LimpezaQueConta();

        var resultado = await Montar(area, volume, new SenhaFixa(null), semeadura, limpeza).PrepararAsync();

        Assert.Equal(AcaoArmazenamento.UsarBancoVivo, resultado.Acao);
        Assert.Equal("Marina", await NomeUnicoAsync(area.Banco));
        Assert.Equal(0, semeadura.Chamadas);
        Assert.Equal(0, limpeza.Banco);
        Assert.Equal(0, limpeza.Espelho);
        Assert.Equal(backup, await File.ReadAllBytesAsync(area.Backup));
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
        Assert.Empty(volume.Apagados);
    }

    [Fact]
    public async Task InstalacaoNova_SemeiaUmaVez()
    {
        using var area = new Area();
        var volume = VolumeComFoto();
        var semeadura = new SemeaduraMarcador(area.Banco);
        var limpeza = new LimpezaQueConta();

        var resultado = await Montar(area, volume, new SenhaFixa(null), semeadura, limpeza).PrepararAsync();

        Assert.Equal(AcaoArmazenamento.Semear, resultado.Acao);
        Assert.Equal(1, semeadura.Chamadas);
        Assert.Equal(SemeaduraMarcador.Nome, await NomeUnicoAsync(area.Banco));
        Assert.Equal(0, limpeza.Banco);
        Assert.Equal(0, limpeza.Espelho);
        Assert.DoesNotContain(volume.Arquivos.Keys, nome => nome == volume.NomeArquivo);
    }

    [Fact]
    public async Task BancoApagado_EspelhoValido_RestauraSemSemear_ENaoApagaOEspelho()
    {
        using var area = new Area();
        var backup = area.GravarBackup([4, 4]);
        var volume = VolumeComEspelho(_espelhos.Valido);
        var semeadura = new SemeaduraMarcador(area.Banco);
        var limpeza = new LimpezaQueConta();

        var resultado = await Montar(area, volume, new SenhaFixa(EspelhosReais.Senha), semeadura, limpeza).PrepararAsync();

        Assert.Equal(AcaoArmazenamento.RestaurarDoEspelho, resultado.Acao);
        Assert.Null(resultado.Falha);
        var membro = await MembroUnicoAsync(area.Banco);
        Assert.Equal("Marina", membro.Nome);
        Assert.Equal("foto-20261017.jpg", membro.FotoPath);
        Assert.Equal(0, semeadura.Chamadas);
        Assert.Equal(0, limpeza.Banco);
        Assert.Equal(0, limpeza.Espelho);
        Assert.Equal(_espelhos.Valido, volume.Arquivos[volume.NomeArquivo]);
        Assert.Equal(backup, await File.ReadAllBytesAsync(area.Backup));
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
        Assert.Empty(volume.Apagados);
        Assert.DoesNotContain(EspelhosReais.Senha, resultado.Motivo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SenhaAusente_NaoLeNemCriaBanco_ENaoApagaOEspelho()
    {
        using var area = new Area();
        var backup = area.GravarBackup([5, 5]);
        var volume = VolumeComEspelho(_espelhos.Valido);
        var semeadura = new SemeaduraMarcador(area.Banco);
        var limpeza = new LimpezaQueConta();

        var resultado = await Montar(area, volume, new SenhaFixa(null), semeadura, limpeza).PrepararAsync();

        Assert.Equal(AcaoArmazenamento.TelaDeRecuperacao, resultado.Acao);
        Assert.Equal(0, volume.Copias);
        Assert.Equal(0, semeadura.Chamadas);
        Assert.Equal(0, limpeza.Banco);
        Assert.Equal(0, limpeza.Espelho);
        Assert.False(File.Exists(area.Banco));
        Assert.Equal(_espelhos.Valido, volume.Arquivos[volume.NomeArquivo]);
        Assert.Equal(backup, await File.ReadAllBytesAsync(area.Backup));
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
        Assert.Empty(volume.Apagados);
        AssertNaoVaza(resultado, EspelhosReais.Senha, area.Raiz);
    }

    [Fact]
    public async Task SenhaAusente_NaRestauracaoDireta_NaoCopiaNemSubstitui()
    {
        using var area = new Area();
        var vivo = new byte[] { 7, 7, 7 };
        await File.WriteAllBytesAsync(area.Banco, vivo);
        var volume = VolumeComEspelho(_espelhos.Valido);

        var resultado = await Restaurar(area, volume, new SenhaFixa(null)).RestaurarAsync();

        Assert.False(resultado.Sucesso);
        Assert.Equal(0, volume.Copias);
        Assert.Equal(vivo, await File.ReadAllBytesAsync(area.Banco));
        Assert.Equal(_espelhos.Valido, volume.Arquivos[volume.NomeArquivo]);
        Assert.Empty(volume.Apagados);
        Assert.DoesNotContain(EspelhosReais.Senha, resultado.MotivoFalha, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SenhaErrada_NaRestauracaoDireta_NaoSubstituiOBanco()
    {
        using var area = new Area();
        var vivo = new byte[] { 8, 8, 8, 8 };
        await File.WriteAllBytesAsync(area.Banco, vivo);
        var volume = VolumeComEspelho(_espelhos.Valido);
        var restauracao = Restaurar(area, volume, new SenhaFixa(SenhaErrada));

        var resultado = await restauracao.RestaurarAsync();

        Assert.False(resultado.Sucesso);
        Assert.Equal(vivo, await File.ReadAllBytesAsync(area.Banco));
        Assert.Equal(_espelhos.Valido, volume.Arquivos[volume.NomeArquivo]);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
        Assert.Empty(volume.Apagados);
        Assert.DoesNotContain(EspelhosReais.Senha, resultado.MotivoFalha, StringComparison.Ordinal);
        Assert.DoesNotContain(SenhaErrada, resultado.MotivoFalha, StringComparison.Ordinal);
        Assert.DoesNotContain(area.Raiz, resultado.MotivoFalha, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EspelhoCorrompido_NaoSemeia_ENaoApaga()
    {
        using var area = new Area();
        var corrupto = (byte[])_espelhos.Valido.Clone();
        corrupto[^1] ^= 0xFF;
        var backup = area.GravarBackup([3, 3]);
        var volume = VolumeComEspelho(corrupto);
        var semeadura = new SemeaduraMarcador(area.Banco);
        var limpeza = new LimpezaQueConta();

        var resultado = await Montar(area, volume, new SenhaFixa(EspelhosReais.Senha), semeadura, limpeza).PrepararAsync();

        Assert.Equal(AcaoArmazenamento.TelaDeRecuperacao, resultado.Acao);
        Assert.Equal(SituacaoEspelho.Corrompido, resultado.Estado!.Espelho);
        Assert.Equal(0, semeadura.Chamadas);
        Assert.Equal(0, limpeza.Banco);
        Assert.Equal(0, limpeza.Espelho);
        Assert.False(File.Exists(area.Banco));
        Assert.False(await TemMarcadorAsync(area.Banco));
        Assert.Equal(corrupto, volume.Arquivos[volume.NomeArquivo]);
        Assert.Equal(backup, await File.ReadAllBytesAsync(area.Backup));
        Assert.Empty(volume.Apagados);
        AssertNaoVaza(resultado, EspelhosReais.Senha, area.Raiz);
    }

    [Fact]
    public async Task ArquivoVivoCorrompido_EEspelhoCorrompido_NaoPareceVazio_ENaoApaga()
    {
        using var area = new Area();
        var lixo = "nao-e-sqlite"u8.ToArray();
        await File.WriteAllBytesAsync(area.Banco, lixo);
        var corrupto = (byte[])_espelhos.Valido.Clone();
        corrupto[20] ^= 0xFF;
        var backup = area.GravarBackup([6, 6]);
        var volume = VolumeComEspelho(corrupto);
        var semeadura = new SemeaduraMarcador(area.Banco);
        var limpeza = new LimpezaQueConta();

        var resultado = await Montar(area, volume, new SenhaFixa(EspelhosReais.Senha), semeadura, limpeza).PrepararAsync();

        Assert.Equal(AcaoArmazenamento.TelaDeRecuperacao, resultado.Acao);
        Assert.Equal(SituacaoBancoVivo.Inconsistente, resultado.Estado!.BancoVivo);
        Assert.NotEqual(SituacaoBancoVivo.Vazio, resultado.Estado.BancoVivo);
        Assert.Equal(0, semeadura.Chamadas);
        Assert.Equal(0, limpeza.Banco);
        Assert.Equal(0, limpeza.Espelho);
        Assert.Equal(lixo, await File.ReadAllBytesAsync(area.Banco));
        Assert.Equal(corrupto, volume.Arquivos[volume.NomeArquivo]);
        Assert.Equal(backup, await File.ReadAllBytesAsync(area.Backup));
        Assert.Empty(volume.Apagados);
    }

    [Theory]
    [InlineData(CodigoFalhaPastaEspelho.PermissaoRevogada)]
    [InlineData(CodigoFalhaPastaEspelho.PastaApagada)]
    [InlineData(CodigoFalhaPastaEspelho.Indisponivel)]
    public async Task PastaInacessivel_SemBanco_NaoSemeia_ENaoApaga(CodigoFalhaPastaEspelho codigo)
    {
        using var area = new Area();
        var backup = area.GravarBackup([1, 2, 3]);
        var volume = VolumeComFoto();
        volume.Acesso = codigo;
        volume.Arquivos["anotacao.txt"] = [4, 5];
        var semeadura = new SemeaduraMarcador(area.Banco);
        var limpeza = new LimpezaQueConta();

        var resultado = await Montar(area, volume, new SenhaFixa(EspelhosReais.Senha), semeadura, limpeza).PrepararAsync();

        Assert.Equal(AcaoArmazenamento.TelaDeRecuperacao, resultado.Acao);
        Assert.Equal(SituacaoEspelho.Corrompido, resultado.Estado!.Espelho);
        Assert.Equal(0, semeadura.Chamadas);
        Assert.Equal(0, volume.Copias);
        Assert.Equal(0, limpeza.Banco);
        Assert.Equal(0, limpeza.Espelho);
        Assert.False(File.Exists(area.Banco));
        Assert.Equal(backup, await File.ReadAllBytesAsync(area.Backup));
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
        Assert.Equal(new byte[] { 4, 5 }, volume.Arquivos["anotacao.txt"]);
        Assert.Empty(volume.Apagados);
    }

    [Fact]
    public async Task PastaInacessivel_ComBancoIntegro_UsaOVivo_ENaoApagaAPasta()
    {
        using var area = new Area();
        await CriarMarinaAsync(area.Banco, foto: null);
        var volume = VolumeComEspelho(_espelhos.Valido);
        volume.Acesso = CodigoFalhaPastaEspelho.PermissaoRevogada;
        var semeadura = new SemeaduraMarcador(area.Banco);
        var limpeza = new LimpezaQueConta();

        var resultado = await Montar(area, volume, new SenhaFixa(EspelhosReais.Senha), semeadura, limpeza).PrepararAsync();

        Assert.Equal(AcaoArmazenamento.UsarBancoVivo, resultado.Acao);
        Assert.Equal("Marina", await NomeUnicoAsync(area.Banco));
        Assert.Equal(0, semeadura.Chamadas);
        Assert.Equal(0, volume.Copias);
        Assert.Equal(_espelhos.Valido, volume.Arquivos[volume.NomeArquivo]);
        Assert.Empty(volume.Apagados);
    }

    [Fact]
    public async Task EspelhoMaisNovo_PedeAtualizacao_SemRestaurar_SemSemear_SemApagar()
    {
        using var area = new Area();
        var backup = area.GravarBackup([2, 2]);
        var volume = VolumeComEspelho(_espelhos.SchemaNovo);
        var semeadura = new SemeaduraMarcador(area.Banco);
        var limpeza = new LimpezaQueConta();

        var resultado = await Montar(area, volume, new SenhaFixa(EspelhosReais.Senha), semeadura, limpeza).PrepararAsync();

        Assert.Equal(AcaoArmazenamento.PedirAtualizacaoDoApp, resultado.Acao);
        Assert.Equal(0, semeadura.Chamadas);
        Assert.Equal(0, limpeza.Banco);
        Assert.Equal(0, limpeza.Espelho);
        Assert.False(File.Exists(area.Banco));
        Assert.Equal(_espelhos.SchemaNovo, volume.Arquivos[volume.NomeArquivo]);
        Assert.Equal(backup, await File.ReadAllBytesAsync(area.Backup));
        Assert.Empty(volume.Apagados);
    }

    [Fact]
    public async Task BancoIlegivel_PreservacaoQueFalha_NaoRestaura_NaoSemeia_NaoApaga()
    {
        using var area = new Area();
        var lixo = "banco-ilegivel"u8.ToArray();
        await File.WriteAllBytesAsync(area.Banco, lixo);
        var bloqueio = Path.Combine(area.Raiz, "backups");
        await File.WriteAllBytesAsync(bloqueio, [1, 1, 1]);
        var volume = VolumeComEspelho(_espelhos.Valido);
        var semeadura = new SemeaduraMarcador(area.Banco);
        var limpeza = new LimpezaQueConta();

        var resultado = await Montar(area, volume, new SenhaFixa(EspelhosReais.Senha), semeadura, limpeza).PrepararAsync();

        Assert.Equal(AcaoArmazenamento.TelaDeRecuperacao, resultado.Acao);
        Assert.Equal(AcaoArmazenamento.RestaurarDoEspelho, resultado.AcaoPlanejada);
        Assert.Equal(0, semeadura.Chamadas);
        Assert.Equal(0, limpeza.Banco);
        Assert.Equal(0, limpeza.Espelho);
        Assert.Equal(lixo, await File.ReadAllBytesAsync(area.Banco));
        Assert.Equal(new byte[] { 1, 1, 1 }, await File.ReadAllBytesAsync(bloqueio));
        Assert.Equal(_espelhos.Valido, volume.Arquivos[volume.NomeArquivo]);
        Assert.Empty(volume.Apagados);
        Assert.False(await TemMarcadorAsync(area.Banco));
    }

    [Fact]
    public async Task BancoIlegivel_ComEspelhoValido_Restaura_EGuardaOOriginal()
    {
        using var area = new Area();
        var lixo = "banco-ilegivel-original"u8.ToArray();
        await File.WriteAllBytesAsync(area.Banco, lixo);
        var backup = area.GravarBackup([9, 8, 7]);
        var volume = VolumeComEspelho(_espelhos.Valido);
        var semeadura = new SemeaduraMarcador(area.Banco);
        var limpeza = new LimpezaQueConta();

        var resultado = await Montar(area, volume, new SenhaFixa(EspelhosReais.Senha), semeadura, limpeza).PrepararAsync();

        Assert.Equal(AcaoArmazenamento.RestaurarDoEspelho, resultado.Acao);
        Assert.Equal("Marina", (await MembroUnicoAsync(area.Banco)).Nome);
        Assert.Equal(0, semeadura.Chamadas);
        Assert.Equal(backup, await File.ReadAllBytesAsync(area.Backup));
        Assert.Equal(_espelhos.Valido, volume.Arquivos[volume.NomeArquivo]);
        Assert.Contains(ArquivosDaPasta(area.Raiz), bytes => bytes.SequenceEqual(lixo));
        Assert.Empty(volume.Apagados);
    }

    [Fact]
    public async Task LimparTudoNaAbertura_NaoApagaBancoNemEspelho()
    {
        using var area = new Area();
        var vivo = new byte[] { 3, 2, 1 };
        await File.WriteAllBytesAsync(area.Banco, vivo);
        var backup = area.GravarBackup([6]);
        var volume = VolumeComEspelho(_espelhos.Valido);
        var semeadura = new SemeaduraMarcador(area.Banco);
        var coordenador = Montar(area, volume, new SenhaFixa(null), semeadura, new LimpezaTotalForaDaAbertura());

        await Assert.ThrowsAsync<LimpezaTotalException>(() => coordenador.LimparTudoAsync(PedidoLimparTudo.Explicito));

        Assert.Equal(vivo, await File.ReadAllBytesAsync(area.Banco));
        Assert.Equal(backup, await File.ReadAllBytesAsync(area.Backup));
        Assert.Equal(_espelhos.Valido, volume.Arquivos[volume.NomeArquivo]);
        Assert.Equal(0, semeadura.Chamadas);
        Assert.Empty(volume.Apagados);
    }

    [Fact]
    public async Task TrocaQueFalhaAntesDeMover_NaoApagaOrigemNemBackup()
    {
        using var area = new Area();
        var bloqueio = Path.Combine(area.Raiz, "bloqueio");
        await File.WriteAllBytesAsync(bloqueio, [1]);
        var origem = Path.Combine(area.Raiz, "snapshot.db");
        await File.WriteAllBytesAsync(origem, [2, 3]);
        var backup = area.GravarBackup([4]);

        var resultado = TrocaArquivoBanco.Substituir(Path.Combine(bloqueio, "orcafacil.db"), origem);

        Assert.False(resultado.Sucesso);
        Assert.Equal(new byte[] { 1 }, await File.ReadAllBytesAsync(bloqueio));
        Assert.Equal(new byte[] { 2, 3 }, await File.ReadAllBytesAsync(origem));
        Assert.Equal(backup, await File.ReadAllBytesAsync(area.Backup));
        Assert.False(File.Exists(area.Banco));
    }

    [Fact]
    public async Task Troca_AfastaWalAntigo_ENaoApagaOsBytes()
    {
        using var area = new Area();
        await File.WriteAllBytesAsync(area.Banco, [1, 1]);
        await File.WriteAllBytesAsync(area.Banco + "-wal", [2, 2]);
        var origem = Path.Combine(area.Raiz, "snapshot.db");
        await File.WriteAllBytesAsync(origem, [3, 3, 3]);

        var resultado = TrocaArquivoBanco.Substituir(area.Banco, origem);

        Assert.True(resultado.Sucesso);
        Assert.Equal(new byte[] { 3, 3, 3 }, await File.ReadAllBytesAsync(area.Banco));
        Assert.False(File.Exists(area.Banco + "-wal"));
        Assert.Equal(new byte[] { 3, 3, 3 }, await File.ReadAllBytesAsync(origem));
        var guardados = ArquivosDaPasta(area.Raiz);
        Assert.Contains(guardados, bytes => bytes.SequenceEqual(new byte[] { 1, 1 }));
        Assert.Contains(guardados, bytes => bytes.SequenceEqual(new byte[] { 2, 2 }));
    }

    [Fact]
    public async Task Normalizacao_GravaSoONome_ENaoApagaQuemNaoConverte()
    {
        using var area = new Area();
        await CriarMarinaAsync(area.Banco, "/data/user/0/com.pitthan.orcafacil/files/perfil/foto-20261017.jpg");
        await using (var conexao = await AbrirAsync(area.Banco))
        {
            await using var incluir = conexao.CreateCommand();
            incluir.CommandText = """
                INSERT INTO MembrosFamilia (Nome, Avatar, Role, Ativo, CriadoEm, FotoPath)
                VALUES ('SemNome', 'S', 0, 1, '2026-03-01 00:00:00', '   ')
                """;
            Assert.Equal(1, await incluir.ExecuteNonQueryAsync());
        }

        await NormalizacaoFotoEspelho.AplicarAsync(area.Banco, CancellationToken.None);

        await using var leitura = await AbrirAsync(area.Banco);
        Assert.Equal("foto-20261017.jpg", await EscalarAsync(leitura, "SELECT FotoPath FROM MembrosFamilia WHERE Nome = 'Marina'"));
        Assert.Equal("   ", await EscalarAsync(leitura, "SELECT FotoPath FROM MembrosFamilia WHERE Nome = 'SemNome'"));
        Assert.Equal(2L, await EscalarLongoAsync(leitura, "SELECT COUNT(*) FROM MembrosFamilia"));
    }

    [Fact]
    public void Abertura_RegistraOCoordenador_ENaoMexeNoClienteLlm()
    {
        var raiz = EncontrarRaiz();
        var maui = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "MauiProgram.cs"));
        var inicio = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Services", "InicializacaoApp.cs"));
        var pasta = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Services", "Espelho", "RegistroServicosPastaEspelho.cs"));
        var registro = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Services", "Espelho", "RegistroCoordenadorArmazenamento.cs"));

        Assert.Contains("RegistroCoordenadorArmazenamento.Registrar(builder.Services);", maui, StringComparison.Ordinal);
        Assert.Contains("ClienteMensagens.Criar()", maui, StringComparison.Ordinal);
        Assert.Contains("ICofreChaveApi, CofreChaveApiSecureStorage", maui, StringComparison.Ordinal);
        Assert.Contains("PrepararAsync", inicio, StringComparison.Ordinal);
        Assert.DoesNotContain("DbInitializer.InitializeAsync", inicio, StringComparison.Ordinal);
        Assert.Contains("FileSystem.AppDataDirectory, \"logs\"", inicio, StringComparison.Ordinal);
        Assert.Contains("ProvedorSenhaEspelhoAindaNaoInformada", pasta, StringComparison.Ordinal);
        Assert.Contains("ISondaBancoVivo, SondaBancoArquivo", registro, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<CoordenadorArmazenamento>()", registro, StringComparison.Ordinal);
        Assert.Contains("ILimpezaTotalDados, LimpezaTotalForaDaAbertura", registro, StringComparison.Ordinal);
    }

    private static CoordenadorArmazenamento Montar(
        Area area,
        VolumeMemoriaTeste volume,
        IProvedorSenhaEspelho senha,
        ISemeaduraDados semeadura,
        ILimpezaTotalDados limpeza)
    {
        var caminho = new CaminhoBancoVivo(area.Banco);
        var versao = VersaoSchemaAplicacao.DoApp;
        var espelho = new EspelhoNaPastaUsuario(volume, new LeitorEspelhoLocal(senha, versao), area.Cache);
        return new CoordenadorArmazenamento(
            new SondaBancoArquivo(caminho),
            espelho,
            versao,
            Restaurar(area, volume, senha),
            new MigracaoBancoArquivo(caminho),
            semeadura,
            limpeza,
            new OpcoesPreparoBanco { VersaoAplicativo = "1.0-teste" },
            new PreservacaoBancoArquivo(caminho),
            new PresencaPerfilArquivo(caminho));
    }

    private static RestauracaoEspelhoDaPasta Restaurar(Area area, VolumeMemoriaTeste volume, IProvedorSenhaEspelho senha)
        => new(volume, senha, area.Cache, VersaoSchemaAplicacao.DoApp, new CaminhoBancoVivo(area.Banco));

    private static VolumeMemoriaTeste VolumeComFoto()
    {
        var volume = new VolumeMemoriaTeste();
        volume.Arquivos["foto.jpg"] = [9];
        return volume;
    }

    private static VolumeMemoriaTeste VolumeComEspelho(byte[] espelho)
    {
        var volume = VolumeComFoto();
        volume.Arquivos[volume.NomeArquivo] = espelho;
        return volume;
    }

    private static async Task<string> NomeUnicoAsync(string banco)
        => (await MembroUnicoAsync(banco)).Nome;

    private static async Task<MembroFamilia> MembroUnicoAsync(string banco)
    {
        await using var ctx = ConexaoBancoArquivo.AbrirLeitura(banco);
        return await ctx.MembrosFamilia.SingleAsync();
    }

    private static async Task<bool> TemMarcadorAsync(string banco)
    {
        if (!File.Exists(banco))
            return false;

        try
        {
            await using var ctx = ConexaoBancoArquivo.AbrirLeitura(banco);
            return await ctx.MembrosFamilia.AnyAsync(membro => membro.Nome == SemeaduraMarcador.Nome);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static List<byte[]> ArquivosDaPasta(string raiz)
        => Directory.GetFiles(raiz, "*", SearchOption.AllDirectories)
            .Select(caminho => File.ReadAllBytes(caminho))
            .ToList();

    private static void AssertNaoVaza(ResultadoArmazenamento resultado, string senha, string caminho)
    {
        Assert.DoesNotContain(senha, resultado.Motivo, StringComparison.Ordinal);
        var falha = resultado.Falha?.ToString() ?? "";
        Assert.DoesNotContain(senha, falha, StringComparison.Ordinal);
        Assert.DoesNotContain(caminho, resultado.Motivo, StringComparison.Ordinal);
    }

    private static async Task CriarMarinaAsync(string caminho, string? foto)
    {
        await using var conexao = await AbrirCriandoAsync(caminho);
        var opcoes = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conexao).Options;
        await using (var ctx = new AppDbContext(opcoes))
        {
            await ctx.Database.MigrateAsync();
            ctx.MembrosFamilia.Add(new MembroFamilia
            {
                Nome = "Marina",
                Avatar = "M",
                Ativo = true,
                CriadoEm = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                FotoPath = foto
            });
            await ctx.SaveChangesAsync();
        }

        await using var checkpoint = conexao.CreateCommand();
        checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        await checkpoint.ExecuteNonQueryAsync();
    }

    private static async Task<SqliteConnection> AbrirCriandoAsync(string caminho)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = caminho,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        };
        var conexao = new SqliteConnection(builder.ConnectionString);
        await conexao.OpenAsync();
        return conexao;
    }

    private static async Task<SqliteConnection> AbrirAsync(string caminho)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = caminho,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        };
        var conexao = new SqliteConnection(builder.ConnectionString);
        await conexao.OpenAsync();
        return conexao;
    }

    private static async Task<string> EscalarAsync(SqliteConnection conexao, string sql)
    {
        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        var valor = await comando.ExecuteScalarAsync();
        return valor is null or DBNull ? "" : Convert.ToString(valor) ?? "";
    }

    private static async Task<long> EscalarLongoAsync(SqliteConnection conexao, string sql)
    {
        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        return Convert.ToInt64(await comando.ExecuteScalarAsync());
    }

    private static string EncontrarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OrcaFacil", "MauiProgram.cs")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar MauiProgram.cs.");
    }

    public sealed class EspelhosReais : IAsyncLifetime
    {
        public const string Senha = "TrilhaAzul92";

        public byte[] Valido { get; private set; } = [];

        public byte[] SchemaNovo { get; private set; } = [];

        public async Task InitializeAsync()
        {
            var raiz = Directory.CreateTempSubdirectory("orcafacil-partida-").FullName;
            try
            {
                var banco = Path.Combine(raiz, "orcafacil.db");
                await CriarMarinaAsync(banco, "/data/user/0/com.pitthan.orcafacil/files/perfil/foto-20261017.jpg");
                var espelho = Path.Combine(raiz, "espelho.bin");
                await new GeradorSnapshotBanco(VersaoSchemaAplicacao.DoApp)
                    .GerarAsync(banco, Senha, new DestinoEspelhoArquivo(espelho));
                Valido = await File.ReadAllBytesAsync(espelho);

                string ultima;
                var snapshot = Path.Combine(raiz, "snap.db");
                await using (var conexao = await AbrirAsync(banco))
                {
                    await using (var incluir = conexao.CreateCommand())
                    {
                        incluir.CommandText = """
                            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                            VALUES ('20990101000000_FuturaA', '10.0.0')
                            """;
                        Assert.Equal(1, await incluir.ExecuteNonQueryAsync());
                    }

                    await using (var vacuum = conexao.CreateCommand())
                    {
                        vacuum.CommandText = "VACUUM INTO $destino;";
                        vacuum.Parameters.AddWithValue("$destino", snapshot);
                        await vacuum.ExecuteNonQueryAsync();
                    }

                    ultima = await EscalarAsync(conexao, """
                        SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1
                        """);
                }

                var novo = Path.Combine(raiz, "novo.bin");
                CofreEspelho.Gravar(
                    novo,
                    await File.ReadAllBytesAsync(snapshot),
                    Senha,
                    VersaoSchemaAplicacao.DoApp.Atual + 2,
                    ultima,
                    DateTimeOffset.UtcNow,
                    FormatoEspelho.IteracoesPadrao);
                SchemaNovo = await File.ReadAllBytesAsync(novo);
            }
            finally
            {
                try
                {
                    Directory.Delete(raiz, recursive: true);
                }
                catch (IOException)
                {
                }
            }
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }

    private sealed class Area : IDisposable
    {
        public Area()
        {
            Raiz = Directory.CreateTempSubdirectory("orcafacil-area-").FullName;
            Banco = Path.Combine(Raiz, "orcafacil.db");
            Backup = Path.Combine(Raiz, "backups", "antigo.db");
            Cache = new AreaTemporariaEspelhoEmDisco(Path.Combine(Raiz, "cache"));
        }

        public string Raiz { get; }

        public string Banco { get; }

        public string Backup { get; }

        public AreaTemporariaEspelhoEmDisco Cache { get; }

        public byte[] GravarBackup(byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Backup)!);
            File.WriteAllBytes(Backup, bytes);
            return bytes;
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            try
            {
                if (Directory.Exists(Raiz))
                    Directory.Delete(Raiz, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private sealed class SenhaFixa(string? senha) : IProvedorSenhaEspelho
    {
        public string? Obter() => senha;

        public override string ToString() => "SenhaFixa";
    }

    private sealed class SemeaduraMarcador(string caminho) : ISemeaduraDados
    {
        public const string Nome = "DadoFicticio";

        public int Chamadas { get; private set; }

        public async Task SemearAsync(CancellationToken cancellationToken = default)
        {
            Chamadas++;
            await using var ctx = ConexaoBancoArquivo.AbrirEscrita(caminho);
            ctx.MembrosFamilia.Add(new MembroFamilia
            {
                Nome = Nome,
                Avatar = "X",
                Ativo = true,
                CriadoEm = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
            await ctx.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class LimpezaQueConta : ILimpezaTotalDados
    {
        public int Banco { get; private set; }

        public int Espelho { get; private set; }

        public Task RemoverBancoVivoAsync(CancellationToken cancellationToken = default)
        {
            Banco++;
            throw new InvalidOperationException("não apagar banco");
        }

        public Task RemoverEspelhoAsync(CancellationToken cancellationToken = default)
        {
            Espelho++;
            throw new InvalidOperationException("não apagar espelho");
        }
    }

    private sealed class VolumeMemoriaTeste : IVolumePastaEspelho
    {
        public string NomeArquivo => FormatoEspelho.NomeArquivoSugerido;

        public CodigoFalhaPastaEspelho Acesso { get; set; } = CodigoFalhaPastaEspelho.Nenhuma;

        public Dictionary<string, byte[]> Arquivos { get; } = new(StringComparer.Ordinal);

        public List<string> Apagados { get; } = [];

        public int Copias { get; private set; }

        public DiagnosticoPastaEspelho Consultar()
        {
            if (Acesso != CodigoFalhaPastaEspelho.Nenhuma)
                return new DiagnosticoPastaEspelho(Acesso, arquivoNoNomeFinal: false);

            return new DiagnosticoPastaEspelho(CodigoFalhaPastaEspelho.Nenhuma, Arquivos.ContainsKey(NomeArquivo));
        }

        public Task<ResultadoOperacaoPasta> CriarTemporarioAsync(
            string nomeTemporario,
            string arquivoOrigemLocal,
            CancellationToken cancellationToken)
            => Recusar(nomeTemporario);

        public Task<ResultadoRenomeacaoVolume> RenomearAsync(
            string nomeAtual,
            string nomeNovo,
            CancellationToken cancellationToken)
        {
            Apagados.Add(nomeAtual);
            return Task.FromResult(ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.Indisponivel));
        }

        public Task<ResultadoOperacaoPasta> DescartarTemporarioAsync(string nomeTemporario, CancellationToken cancellationToken)
            => Recusar(nomeTemporario);

        public Task<ResultadoOperacaoPasta> DescartarReservaAposTrocaAsync(string nomeReserva, CancellationToken cancellationToken)
            => Recusar(nomeReserva);

        public Task<ResultadoOperacaoPasta> CopiarParaLocalAsync(
            string nome,
            string caminhoLocal,
            CancellationToken cancellationToken)
        {
            Copias++;
            cancellationToken.ThrowIfCancellationRequested();
            if (Acesso != CodigoFalhaPastaEspelho.Nenhuma)
                return Task.FromResult(ResultadoOperacaoPasta.Falha(Acesso));

            if (!Arquivos.TryGetValue(nome, out var bytes))
                return Task.FromResult(ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.ArquivoAusente));

            File.WriteAllBytes(caminhoLocal, bytes);
            return Task.FromResult(ResultadoOperacaoPasta.Ok());
        }

        private Task<ResultadoOperacaoPasta> Recusar(string nome)
        {
            Apagados.Add(nome);
            return Task.FromResult(ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.Indisponivel));
        }
    }
}
