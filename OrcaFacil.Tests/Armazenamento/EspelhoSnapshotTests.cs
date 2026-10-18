using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Armazenamento;
using OrcaFacil.Core.Armazenamento.Espelho;
using OrcaFacil.Core.Data;

namespace OrcaFacil.Tests.Armazenamento;

public class EspelhoSnapshotTests
{
    private const string Senha = "TrilhaAzul92";
    private const string SenhaErrada = "Outra1234";
    private readonly VersaoSchemaAplicacao _versao = VersaoSchemaAplicacao.DoApp;
    private readonly GeradorSnapshotBanco _gerador = new(VersaoSchemaAplicacao.DoApp);

    [Fact]
    public async Task Snapshot_DeBancoComDados_GravaMetadadosENaoAlteraOVivo()
    {
        using var area = new AreaTemp();
        await using var vivo = await CriarBancoAbertoAsync(area.Banco, "Marina");
        Assert.Equal("wal", await EscalarTextoAsync(vivo, "PRAGMA journal_mode"));

        var metadados = await _gerador.GerarAsync(area.Banco, Senha, new DestinoEspelhoArquivo(area.Espelho));

        Assert.Equal(FormatoEspelho.VersaoAtual, metadados.VersaoFormato);
        Assert.Equal(_versao.Atual, metadados.VersaoSchema);
        Assert.Equal(_versao.UltimaMigration, metadados.UltimaMigration);
        Assert.Equal(FormatoEspelho.IteracoesPadrao, metadados.IteracoesPbkdf2);
        Assert.Equal(64, metadados.HashSha256.Length);
        Assert.True(metadados.TamanhoBytes > 0);
        Assert.InRange(metadados.GeradoEm, DateTimeOffset.UtcNow.AddMinutes(-2), DateTimeOffset.UtcNow.AddMinutes(2));
        Assert.Equal("Marina", await EscalarTextoAsync(vivo, "SELECT Nome FROM MembrosFamilia"));
        Assert.True(File.Exists(area.Banco));

        var espelho = await File.ReadAllBytesAsync(area.Espelho);
        Assert.Equal(Encoding.ASCII.GetBytes("ORFESPEL"), espelho[..8]);
        Assert.False(TemPrefixoSqlite(espelho));
        Assert.False(Contem(espelho, Encoding.UTF8.GetBytes("Marina")));
        Assert.False(Contem(espelho, Encoding.UTF8.GetBytes(Senha)));
    }

    [Fact]
    public async Task IdaEVolta_DescriptografaAbreEConfereOsDados()
    {
        using var area = new AreaTemp();
        await using (var vivo = await CriarBancoAbertoAsync(area.Banco, "Marina"))
        {
            await _gerador.GerarAsync(area.Banco, Senha, new DestinoEspelhoArquivo(area.Espelho));
            Assert.Equal("Marina", await EscalarTextoAsync(vivo, "SELECT Nome FROM MembrosFamilia"));
        }

        var antes = await File.ReadAllBytesAsync(area.Espelho);
        using var aberto = await LerEspelho.AbrirAsync(area.Espelho, Senha, _versao);
        string hash;
        await using (var stream = File.OpenRead(aberto.CaminhoSqlite))
            hash = Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
        await using var snapshot = AbrirSomenteLeitura(aberto.CaminhoSqlite);

        Assert.Equal("Marina", await EscalarTextoAsync(snapshot, "SELECT Nome FROM MembrosFamilia"));
        Assert.Equal(_versao.UltimaMigration, await EscalarTextoAsync(snapshot, """
            SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1
            """));
        Assert.Equal(aberto.Metadados.HashSha256, hash);
        Assert.Equal(aberto.Metadados.TamanhoBytes, new FileInfo(aberto.CaminhoSqlite).Length);
        Assert.Equal(antes, await File.ReadAllBytesAsync(area.Espelho));
        Assert.True(File.Exists(area.Banco));
    }

    [Fact]
    public async Task SenhaErrada_LancaSenhaIncorreta_ENaoApagaOEspelho()
    {
        using var area = new AreaTemp("NAO-LOGAR-CAMINHO-9f3a");
        const string senha = "NAO-LOGAR-SENHA-9f3a";
        await using (var vivo = await CriarBancoAbertoAsync(area.Banco, "Marina"))
        {
            await _gerador.GerarAsync(area.Banco, senha, new DestinoEspelhoArquivo(area.Espelho));
        }

        var antes = await File.ReadAllBytesAsync(area.Espelho);
        var excecao = await Assert.ThrowsAsync<SenhaIncorretaException>(() => LerEspelho.AbrirAsync(area.Espelho, SenhaErrada, _versao));

        Assert.Equal(CodigoFalhaEspelho.SenhaIncorreta, excecao.Codigo);
        AssertNaoVaza(excecao, senha, area.Raiz);
        AssertNaoVaza(excecao, SenhaErrada, area.Raiz);
        Assert.Equal(antes, await File.ReadAllBytesAsync(area.Espelho));
        Assert.True(File.Exists(area.Banco));
    }

    [Fact]
    public async Task ByteAlterado_LancaArquivoCorrompido()
    {
        using var area = new AreaTemp("NAO-LOGAR-CAMINHO-9f3a");
        await PublicarAsync(area, "Marina");
        var original = await File.ReadAllBytesAsync(area.Espelho);
        var tamanhoCabecalho = BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(FormatoEspelho.OffsetTamanhoCabecalho, 4));
        var indiceConteudo = (int)tamanhoCabecalho + 8;
        Assert.InRange(indiceConteudo, (int)tamanhoCabecalho, original.Length - FormatoEspelho.TamanhoTag - 1);

        var adulterado = (byte[])original.Clone();
        adulterado[indiceConteudo] ^= 0xFF;
        await File.WriteAllBytesAsync(area.Espelho, adulterado);
        var conteudo = await Assert.ThrowsAsync<ArquivoCorrompidoException>(() => LerEspelho.AbrirAsync(area.Espelho, Senha, _versao));
        Assert.Equal(CodigoFalhaEspelho.ArquivoCorrompido, conteudo.Codigo);
        AssertNaoVaza(conteudo, Senha, area.Raiz);

        var cabecalho = (byte[])original.Clone();
        cabecalho[FormatoEspelho.TamanhoPrefixo] ^= 0xFF;
        await File.WriteAllBytesAsync(area.Espelho, cabecalho);
        var schema = await Assert.ThrowsAsync<ArquivoCorrompidoException>(() => LerEspelho.AbrirAsync(area.Espelho, Senha, _versao));
        Assert.IsNotType<SchemaMaisNovoQueOAppException>(schema);
        AssertNaoVaza(schema, Senha, area.Raiz);
        Assert.True(File.Exists(area.Banco));
    }

    [Fact]
    public async Task ArquivoTruncado_LancaArquivoCorrompido()
    {
        using var area = new AreaTemp("NAO-LOGAR-CAMINHO-9f3a");
        await PublicarAsync(area, "Marina");
        var original = await File.ReadAllBytesAsync(area.Espelho);
        await File.WriteAllBytesAsync(area.Espelho, original.Take(original.Length / 2).ToArray());

        var excecao = await Assert.ThrowsAsync<ArquivoCorrompidoException>(() => LerEspelho.AbrirAsync(area.Espelho, Senha, _versao));

        Assert.Equal(CodigoFalhaEspelho.ArquivoCorrompido, excecao.Codigo);
        AssertNaoVaza(excecao, Senha, area.Raiz);
        Assert.True(File.Exists(area.Banco));
    }

    [Fact]
    public async Task VersaoDeFormatoDesconhecida_LancaVersaoFormatoNaoSuportada()
    {
        using var area = new AreaTemp("NAO-LOGAR-CAMINHO-9f3a");
        await PublicarAsync(area, "Marina");
        var bytes = await File.ReadAllBytesAsync(area.Espelho);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(FormatoEspelho.OffsetVersaoFormato, 2), 99);
        await File.WriteAllBytesAsync(area.Espelho, bytes);

        var excecao = await Assert.ThrowsAsync<VersaoFormatoNaoSuportadaException>(() => LerEspelho.AbrirAsync(area.Espelho, Senha, _versao));

        Assert.Equal(CodigoFalhaEspelho.VersaoFormatoNaoSuportada, excecao.Codigo);
        Assert.Equal((ushort)99, excecao.VersaoEncontrada);
        AssertNaoVaza(excecao, Senha, area.Raiz);
    }

    [Fact]
    public async Task SchemaDuasMigrationsAFrente_LancaSchemaMaisNovoQueOApp()
    {
        using var area = new AreaTemp("NAO-LOGAR-CAMINHO-9f3a");
        await GravarEspelhoComHistoricoExtraAsync(
            area,
            _versao.Atual + 2,
            "20990101000000_FuturaA",
            "20990201000000_FuturaB");

        var excecao = await Assert.ThrowsAsync<SchemaMaisNovoQueOAppException>(() => LerEspelho.AbrirAsync(area.Espelho, Senha, _versao));

        Assert.Equal(CodigoFalhaEspelho.SchemaMaisNovoQueOApp, excecao.Codigo);
        Assert.Equal(_versao.Atual + 2, excecao.VersaoEspelho);
        Assert.Equal(_versao.Atual, excecao.VersaoApp);
        Assert.Null(_versao.Calcular(ComExtras("20990101000000_FuturaA", "20990201000000_FuturaB")));
        AssertNaoVaza(excecao, Senha, area.Raiz);
        Assert.True(File.Exists(area.Espelho));
        Assert.True(File.Exists(area.Banco));
    }

    [Fact]
    public async Task NumeroAutenticadoMaior_NaoExigeIgualdadeComORecalculo()
    {
        using var area = new AreaTemp();
        await GravarEspelhoComHistoricoExtraAsync(
            area,
            _versao.Atual + 5,
            "20990101000000_FuturaA",
            "20990201000000_FuturaB");

        var excecao = await Assert.ThrowsAsync<SchemaMaisNovoQueOAppException>(() => LerEspelho.AbrirAsync(area.Espelho, Senha, _versao));

        Assert.Equal(_versao.Atual + 5, excecao.VersaoEspelho);
        Assert.IsNotType<ArquivoCorrompidoException>(excecao);
    }

    [Fact]
    public async Task FuroNoHistorico_NaoAbreComoSchemaAtual()
    {
        using var area = new AreaTemp();
        await GravarEspelhoComFuroAsync(area, _versao.Atual);

        var excecao = await Assert.ThrowsAsync<ArquivoCorrompidoException>(() => LerEspelho.AbrirAsync(area.Espelho, Senha, _versao));

        Assert.Equal(CodigoFalhaEspelho.ArquivoCorrompido, excecao.Codigo);
        Assert.Null(_versao.Calcular(HistoricoComFuro()));
    }

    [Fact]
    public async Task FalhaNoMeioDaPublicacao_MantemOEspelhoAnterior()
    {
        using var area = new AreaTemp();
        await PublicarAsync(area, "Marina");
        var anterior = await File.ReadAllBytesAsync(area.Espelho);
        await using (var vivo = await AbrirAsync(area.Banco))
        {
            await ExecutarAsync(vivo, "UPDATE MembrosFamilia SET Nome = 'Bruno' WHERE Nome = 'Marina'");
        }

        var destino = new DestinoQueFalhaNaTroca(area.Espelho);
        var excecao = await Assert.ThrowsAsync<IOException>(() => _gerador.GerarAsync(area.Banco, Senha, destino));

        Assert.Equal(1, destino.Tentativas);
        Assert.True(destino.GravouTemporario);
        Assert.Equal(anterior, await File.ReadAllBytesAsync(area.Espelho));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(area.Espelho)!, "*.tmp"));
        Assert.DoesNotContain(area.Raiz, excecao.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Senha, excecao.ToString(), StringComparison.Ordinal);

        using var aberto = await LerEspelho.AbrirAsync(area.Espelho, Senha, _versao);
        await using var snapshot = AbrirSomenteLeitura(aberto.CaminhoSqlite);
        Assert.Equal("Marina", await EscalarTextoAsync(snapshot, "SELECT Nome FROM MembrosFamilia"));
    }

    [Fact]
    public async Task SegundaPublicacaoConcluida_SubstituiOEspelho()
    {
        using var area = new AreaTemp();
        await PublicarAsync(area, "Marina");
        var primeiro = await File.ReadAllBytesAsync(area.Espelho);
        await using (var vivo = await AbrirAsync(area.Banco))
        {
            await ExecutarAsync(vivo, "UPDATE MembrosFamilia SET Nome = 'Bruno' WHERE Nome = 'Marina'");
        }

        await _gerador.GerarAsync(area.Banco, Senha, new DestinoEspelhoArquivo(area.Espelho));

        var segundo = await File.ReadAllBytesAsync(area.Espelho);
        Assert.NotEqual(primeiro, segundo);
        using var aberto = await LerEspelho.AbrirAsync(area.Espelho, Senha, _versao);
        await using var snapshot = AbrirSomenteLeitura(aberto.CaminhoSqlite);
        Assert.Equal("Bruno", await EscalarTextoAsync(snapshot, "SELECT Nome FROM MembrosFamilia"));
    }

    [Fact]
    public async Task ForeignKeyCheckFalha_NaoPublica()
    {
        using var area = new AreaTemp("NAO-LOGAR-CAMINHO-9f3a");
        var sentinela = new byte[] { 1, 2, 3, 4 };
        await File.WriteAllBytesAsync(area.Espelho, sentinela);
        await CriarBancoComForeignKeyQuebradaAsync(area.Banco);
        var destino = new DestinoQueRecusa();

        var excecao = await Assert.ThrowsAsync<IntegridadeSnapshotException>(() => _gerador.GerarAsync(area.Banco, Senha, destino));

        Assert.Equal(0, destino.Chamadas);
        Assert.Equal(IntegridadeSnapshotException.ForeignKeyCheck, excecao.Message);
        Assert.Equal(sentinela, await File.ReadAllBytesAsync(area.Espelho));
        AssertNaoVaza(excecao, Senha, area.Raiz);
        Assert.True(File.Exists(area.Banco));
    }

    [Fact]
    public async Task BancoMalformado_NaoPublica()
    {
        using var area = new AreaTemp("NAO-LOGAR-CAMINHO-9f3a");
        await File.WriteAllBytesAsync(area.Banco, "isto-nao-e-sqlite"u8.ToArray());
        var destino = new DestinoQueRecusa();

        var excecao = await Assert.ThrowsAsync<IntegridadeSnapshotException>(() => _gerador.GerarAsync(area.Banco, Senha, destino));

        Assert.Equal(0, destino.Chamadas);
        Assert.Equal(IntegridadeSnapshotException.NadaPublicado, excecao.Message);
        Assert.False(File.Exists(area.Espelho));
        AssertNaoVaza(excecao, Senha, area.Raiz);
    }

    [Fact]
    public async Task SenhaFraca_NaoPublica()
    {
        using var area = new AreaTemp("NAO-LOGAR-CAMINHO-9f3a");
        var destino = new DestinoQueRecusa();
        const string fraca = "curta1";

        var excecao = await Assert.ThrowsAsync<ArgumentException>(() => _gerador.GerarAsync(area.Banco, fraca, destino));

        Assert.Equal(0, destino.Chamadas);
        Assert.False(File.Exists(area.Espelho));
        AssertNaoVaza(excecao, fraca, area.Raiz);
    }

    [Fact]
    public async Task EspelhoArquivo_RestauracaoQueFalha_NaoSemeiaNemApaga()
    {
        using var area = new AreaTemp();
        await PublicarAsync(area, "Marina");
        var antes = await File.ReadAllBytesAsync(area.Espelho);
        var portas = new PortasCoordenador();
        var espelho = new EspelhoArquivo(area.Espelho, new SenhaFixa(Senha), _versao);

        var resultado = await portas.Criar(espelho, _versao).PrepararAsync();

        Assert.Equal(AcaoArmazenamento.TelaDeRecuperacao, resultado.Acao);
        Assert.Equal(AcaoArmazenamento.RestaurarDoEspelho, resultado.AcaoPlanejada);
        Assert.NotNull(resultado.Falha);
        Assert.Equal(1, portas.Restauracoes);
        Assert.Equal(0, portas.Preservacoes);
        Assert.Equal(0, portas.Migracoes);
        Assert.Equal(0, portas.Semeaduras);
        Assert.Equal(0, portas.RemocoesBanco);
        Assert.Equal(0, portas.RemocoesEspelho);
        Assert.Equal(antes, await File.ReadAllBytesAsync(area.Espelho));
        Assert.True(File.Exists(area.Banco));
        Assert.DoesNotContain(Senha, resultado.Falha!.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(area.Raiz, resultado.Falha.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task BancoVazio_NaoSubstituiOEspelhoBom()
    {
        using var area = new AreaTemp("NAO-LOGAR-CAMINHO-9f3a");
        await PublicarAsync(area, "Marina");
        var anterior = await File.ReadAllBytesAsync(area.Espelho);
        ApagarLaterais(area.Banco);
        await File.WriteAllBytesAsync(area.Banco, []);
        var destino = new DestinoEspelhoArquivo(area.Espelho);

        var excecao = await Assert.ThrowsAsync<IntegridadeSnapshotException>(() => _gerador.GerarAsync(area.Banco, Senha, destino));

        Assert.Equal(IntegridadeSnapshotException.BancoSemHistorico, excecao.Message);
        Assert.Equal(anterior, await File.ReadAllBytesAsync(area.Espelho));
        Assert.Equal(0, new FileInfo(area.Banco).Length);
        AssertNaoVaza(excecao, Senha, area.Raiz);

        using var aberto = await LerEspelho.AbrirAsync(area.Espelho, Senha, _versao);
        await using var snapshot = AbrirSomenteLeitura(aberto.CaminhoSqlite);
        Assert.Equal("Marina", await EscalarTextoAsync(snapshot, "SELECT Nome FROM MembrosFamilia"));
    }

    [Fact]
    public async Task SqliteSemHistoricoDoApp_NaoPublica()
    {
        using var area = new AreaTemp();
        await PublicarAsync(area, "Marina");
        var anterior = await File.ReadAllBytesAsync(area.Espelho);
        var vazio = Path.Combine(area.Raiz, "vazio.db");
        await using (var conexao = await AbrirAsync(vazio, criar: true))
        {
        }

        var destino = new DestinoQueRecusa();
        var excecao = await Assert.ThrowsAsync<IntegridadeSnapshotException>(() => _gerador.GerarAsync(vazio, Senha, destino));

        Assert.Equal(0, destino.Chamadas);
        Assert.Equal(IntegridadeSnapshotException.BancoSemHistorico, excecao.Message);
        Assert.Equal(anterior, await File.ReadAllBytesAsync(area.Espelho));
    }

    [Fact]
    public async Task HistoricoSemTabelaDoApp_NaoPublica()
    {
        using var area = new AreaTemp();
        await PublicarAsync(area, "Marina");
        var anterior = await File.ReadAllBytesAsync(area.Espelho);
        var casca = Path.Combine(area.Raiz, "casca.db");
        await CriarHistoricoSemTabelaDoAppAsync(casca);
        var destino = new DestinoQueRecusa();

        var excecao = await Assert.ThrowsAsync<IntegridadeSnapshotException>(() => _gerador.GerarAsync(casca, Senha, destino));

        Assert.Equal(0, destino.Chamadas);
        Assert.Equal(IntegridadeSnapshotException.BancoSemHistorico, excecao.Message);
        Assert.Equal(anterior, await File.ReadAllBytesAsync(area.Espelho));
    }

    [Fact]
    public async Task FuroNoHistorico_NaoPublicaEMantemOEspelhoAnterior()
    {
        using var area = new AreaTemp("NAO-LOGAR-CAMINHO-9f3a");
        await PublicarAsync(area, "Marina");
        var anterior = await File.ReadAllBytesAsync(area.Espelho);
        await using (var vivo = await AbrirAsync(area.Banco))
        {
            await using var apagar = vivo.CreateCommand();
            apagar.CommandText = """
                DELETE FROM "__EFMigrationsHistory"
                WHERE "MigrationId" = '20261008141717_PerfilFechamentoLogs'
                """;
            Assert.Equal(1, await apagar.ExecuteNonQueryAsync());
        }

        var destino = new DestinoQueRecusa();
        var excecao = await Assert.ThrowsAsync<IntegridadeSnapshotException>(() => _gerador.GerarAsync(area.Banco, Senha, destino));

        Assert.Equal(0, destino.Chamadas);
        Assert.Equal(IntegridadeSnapshotException.BancoSemHistorico, excecao.Message);
        Assert.Equal(anterior, await File.ReadAllBytesAsync(area.Espelho));
        AssertNaoVaza(excecao, Senha, area.Raiz);
        Assert.Null(_versao.Calcular(HistoricoComFuro()));
    }

    [Fact]
    public async Task EspelhoArquivo_SchemaMaisNovo_PedeAtualizacaoSemRestaurar()
    {
        using var area = new AreaTemp();
        await GravarEspelhoComHistoricoExtraAsync(
            area,
            _versao.Atual + 2,
            "20990101000000_FuturaA",
            "20990201000000_FuturaB");

        var antes = await File.ReadAllBytesAsync(area.Espelho);
        var portas = new PortasCoordenador();
        var leitura = await new EspelhoArquivo(area.Espelho, new SenhaFixa(Senha), _versao).LerAsync();
        var resultado = await portas.Criar(new EspelhoArquivo(area.Espelho, new SenhaFixa(Senha), _versao), _versao).PrepararAsync();

        Assert.True(leitura.Legivel);
        Assert.Equal(_versao.Atual + 2, leitura.VersaoSchema);
        Assert.Equal(AcaoArmazenamento.PedirAtualizacaoDoApp, resultado.Acao);
        Assert.Null(resultado.Falha);
        Assert.Equal(0, portas.Restauracoes);
        Assert.Equal(0, portas.Preservacoes);
        Assert.Equal(0, portas.Migracoes);
        Assert.Equal(0, portas.Semeaduras);
        Assert.Equal(0, portas.RemocoesBanco);
        Assert.Equal(0, portas.RemocoesEspelho);
        Assert.Equal(antes, await File.ReadAllBytesAsync(area.Espelho));
    }

    [Fact]
    public async Task EspelhoArquivo_SenhaErradaOuAusente_FicaIlegivel_ENaoSemeia()
    {
        using var area = new AreaTemp("NAO-LOGAR-CAMINHO-9f3a");
        await PublicarAsync(area, "Marina");
        var antes = await File.ReadAllBytesAsync(area.Espelho);
        var portas = new PortasCoordenador();
        var errada = new EspelhoArquivo(area.Espelho, new SenhaFixa(SenhaErrada), _versao);
        var leitura = await errada.LerAsync();
        var resultado = await portas.Criar(errada, _versao).PrepararAsync();

        Assert.False(leitura.Legivel);
        Assert.Null(leitura.VersaoSchema);
        Assert.Equal("Senha incorreta.", leitura.Motivo);
        AssertNaoVaza(new InvalidOperationException(leitura.Motivo), Senha, area.Raiz);
        Assert.Equal(AcaoArmazenamento.TelaDeRecuperacao, resultado.Acao);
        Assert.Equal(AcaoArmazenamento.TelaDeRecuperacao, resultado.AcaoPlanejada);
        Assert.Equal(0, portas.Restauracoes);
        Assert.Equal(0, portas.Preservacoes);
        Assert.Equal(0, portas.Migracoes);
        Assert.Equal(0, portas.Semeaduras);
        Assert.Equal(0, portas.RemocoesBanco);
        Assert.Equal(0, portas.RemocoesEspelho);
        Assert.Equal(antes, await File.ReadAllBytesAsync(area.Espelho));

        var ausente = await new EspelhoArquivo(area.Espelho, new SenhaFixa(null), _versao).LerAsync();
        Assert.False(ausente.Legivel);
        Assert.Equal("Senha do espelho ausente.", ausente.Motivo);
        Assert.DoesNotContain(area.Raiz, ausente.Motivo, StringComparison.Ordinal);
        Assert.Equal("EspelhoArquivo", errada.ToString());
        Assert.DoesNotContain(area.Raiz, errada.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Coordenador_SemOpcoesOuSemPreservacao_Lanca()
    {
        var portas = new PortasCoordenador();
        var espelho = new EspelhoArquivo(Path.Combine(Path.GetTempPath(), "orcafacil-sem-arquivo.espelho"), new SenhaFixa(null), _versao);

        var semOpcoes = Assert.Throws<ArgumentNullException>(() => portas.CriarSemOpcoes(espelho, _versao));
        var semPreservacao = Assert.Throws<ArgumentNullException>(() => portas.CriarSemPreservacao(espelho, _versao));

        Assert.Equal("opcoesPreparo", semOpcoes.ParamName);
        Assert.Equal("preservacao", semPreservacao.ParamName);
    }

    [Fact]
    public async Task EspelhoArquivo_BancoVazio_RestauracaoQueFalha_NaoSemeiaNemApaga()
    {
        using var area = new AreaTemp();
        await PublicarAsync(area, "Marina");
        var antes = await File.ReadAllBytesAsync(area.Espelho);
        var portas = new PortasCoordenador
        {
            BancoExiste = true,
            Relatorio = RelatorioVazio()
        };

        var resultado = await portas.Criar(new EspelhoArquivo(area.Espelho, new SenhaFixa(Senha), _versao), _versao).PrepararAsync();

        Assert.Equal(SituacaoBancoVivo.Vazio, resultado.Estado!.BancoVivo);
        Assert.Equal(AcaoArmazenamento.TelaDeRecuperacao, resultado.Acao);
        Assert.Equal(AcaoArmazenamento.RestaurarDoEspelho, resultado.AcaoPlanejada);
        Assert.Equal(1, portas.Restauracoes);
        Assert.Equal(0, portas.Preservacoes);
        Assert.Equal(0, portas.Migracoes);
        Assert.Equal(0, portas.Semeaduras);
        Assert.Equal(0, portas.RemocoesBanco);
        Assert.Equal(0, portas.RemocoesEspelho);
        Assert.Equal(antes, await File.ReadAllBytesAsync(area.Espelho));
        Assert.True(File.Exists(area.Banco));
    }

    [Fact]
    public async Task EspelhoArquivo_ArquivoIlegivel_PreservacaoQueFalha_NaoRestauraNemApaga()
    {
        using var area = new AreaTemp("NAO-LOGAR-CAMINHO-9f3a");
        await PublicarAsync(area, "Marina");
        var antes = await File.ReadAllBytesAsync(area.Espelho);
        var portas = new PortasCoordenador
        {
            BancoExiste = true,
            Relatorio = RelatorioIntegridadeBanco.IlegivelMinimo,
            PreservacaoSucesso = false
        };

        var resultado = await portas.Criar(new EspelhoArquivo(area.Espelho, new SenhaFixa(Senha), _versao), _versao).PrepararAsync();

        Assert.True(RelatorioIntegridadeBanco.IlegivelMinimo.ArquivoIlegivel);
        Assert.Equal(SituacaoBancoVivo.Inconsistente, resultado.Estado!.BancoVivo);
        Assert.NotEqual(SituacaoBancoVivo.Vazio, resultado.Estado.BancoVivo);
        Assert.Equal(AcaoArmazenamento.TelaDeRecuperacao, resultado.Acao);
        Assert.Equal(AcaoArmazenamento.RestaurarDoEspelho, resultado.AcaoPlanejada);
        Assert.Equal(1, portas.Preservacoes);
        Assert.Equal(0, portas.Restauracoes);
        Assert.Equal(0, portas.Migracoes);
        Assert.Equal(0, portas.Semeaduras);
        Assert.Equal(0, portas.RemocoesBanco);
        Assert.Equal(0, portas.RemocoesEspelho);
        Assert.Equal(antes, await File.ReadAllBytesAsync(area.Espelho));
        Assert.True(File.Exists(area.Banco));
        Assert.NotNull(resultado.Falha);
        AssertNaoVaza(resultado.Falha!, Senha, area.Raiz);
    }

    [Fact]
    public async Task EspelhoArquivo_ArquivoIlegivel_RestauracaoQueFalhaDepoisDaCopia_NaoSemeiaNemApaga()
    {
        using var area = new AreaTemp();
        await PublicarAsync(area, "Marina");
        var antes = await File.ReadAllBytesAsync(area.Espelho);
        var portas = new PortasCoordenador
        {
            BancoExiste = true,
            Relatorio = RelatorioIntegridadeBanco.IlegivelMinimo
        };

        var resultado = await portas.Criar(new EspelhoArquivo(area.Espelho, new SenhaFixa(Senha), _versao), _versao).PrepararAsync();

        Assert.Equal(1, portas.Preservacoes);
        Assert.Equal(1, portas.Restauracoes);
        Assert.Equal(0, portas.Migracoes);
        Assert.Equal(0, portas.Semeaduras);
        Assert.Equal(0, portas.RemocoesBanco);
        Assert.Equal(0, portas.RemocoesEspelho);
        Assert.Equal(AcaoArmazenamento.TelaDeRecuperacao, resultado.Acao);
        Assert.Equal(antes, await File.ReadAllBytesAsync(area.Espelho));
        Assert.True(File.Exists(area.Banco));
    }

    [Fact]
    public async Task Semear_MigraComAsOpcoesAntes_ENaoSemeiaSeOSchemaFalha()
    {
        var metodo = typeof(ISemeaduraDados).GetMethod(nameof(ISemeaduraDados.SemearAsync));
        Assert.NotNull(metodo);
        Assert.Equal(typeof(CancellationToken), Assert.Single(metodo!.GetParameters()).ParameterType);

        var portas = new PortasCoordenador { MigracaoExigeRecuperacao = true };
        var espelho = new EspelhoArquivo(
            Path.Combine(Path.GetTempPath(), "orcafacil-sem-espelho-" + Guid.NewGuid().ToString("N") + ".espelho"),
            new SenhaFixa(null),
            _versao);

        var resultado = await portas.Criar(espelho, _versao).PrepararAsync();

        Assert.Equal(AcaoArmazenamento.TelaDeRecuperacao, resultado.Acao);
        Assert.Equal(AcaoArmazenamento.Semear, resultado.AcaoPlanejada);
        Assert.Equal(1, portas.Migracoes);
        Assert.Same(portas.Opcoes, portas.OpcoesRecebidas);
        Assert.Equal("1.2.3", portas.OpcoesRecebidas!.VersaoAplicativo);
        Assert.Equal(0, portas.Semeaduras);
        Assert.Equal(0, portas.Preservacoes);
        Assert.Equal(0, portas.RemocoesBanco);
        Assert.Equal(0, portas.RemocoesEspelho);
        Assert.Contains("schema não foi criado", resultado.Motivo, StringComparison.Ordinal);
    }

    [Fact]
    public void HistoricoVazio_CalcularZero_ClassificaComoVazio()
    {
        Assert.Equal(0, _versao.Calcular([]));
        var relatorio = RelatorioVazio();
        Assert.False(relatorio.ArquivoIlegivel);
        Assert.Empty(relatorio.TabelasExistentes);
        Assert.Empty(relatorio.MigrationsNoHistorico);
        Assert.Equal(SituacaoBancoVivo.Vazio, ClassificacaoBancoVivo.Classificar(LeituraBancoVivo.Lida(relatorio)));
        Assert.NotEqual(SituacaoBancoVivo.Desatualizado, ClassificacaoBancoVivo.Classificar(LeituraBancoVivo.Lida(relatorio)));
        Assert.Equal(
            SituacaoBancoVivo.Inconsistente,
            ClassificacaoBancoVivo.Classificar(LeituraBancoVivo.Lida(RelatorioIntegridadeBanco.IlegivelMinimo)));
    }

    private static RelatorioIntegridadeBanco RelatorioVazio() => new()
    {
        TabelasFaltando = ["MembrosFamilia", "__EFMigrationsHistory"],
        MigrationsAusentesNoHistorico = ["20260826112311_InitialCreate"]
    };

    private async Task GravarEspelhoComHistoricoExtraAsync(AreaTemp area, int versaoSchema, params string[] extras)
    {
        string ultima;
        await using (var vivo = await CriarBancoAbertoAsync(area.Banco, "Marina"))
        {
            foreach (var id in extras)
            {
                await using var comando = vivo.CreateCommand();
                comando.CommandText = """
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                    VALUES ($id, '10.0.0')
                    """;
                comando.Parameters.AddWithValue("$id", id);
                Assert.Equal(1, await comando.ExecuteNonQueryAsync());
            }

            ultima = await EscalarTextoAsync(vivo, """
                SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1
                """);
            var snapshot = Path.Combine(area.Raiz, "consistente.db");
            await using var vacuum = vivo.CreateCommand();
            vacuum.CommandText = "VACUUM INTO $destino;";
            vacuum.Parameters.AddWithValue("$destino", snapshot);
            await vacuum.ExecuteNonQueryAsync();
            var bytes = await File.ReadAllBytesAsync(snapshot);
            CofreEspelho.Gravar(
                area.Espelho,
                bytes,
                Senha,
                versaoSchema,
                ultima,
                DateTimeOffset.UtcNow,
                FormatoEspelho.IteracoesPadrao);
        }
    }

    private async Task GravarEspelhoComFuroAsync(AreaTemp area, int versaoSchema)
    {
        await using var vivo = await CriarBancoAbertoAsync(area.Banco, "Marina");
        await using (var apagar = vivo.CreateCommand())
        {
            apagar.CommandText = """
                DELETE FROM "__EFMigrationsHistory"
                WHERE "MigrationId" = '20261008141717_PerfilFechamentoLogs'
                """;
            Assert.Equal(1, await apagar.ExecuteNonQueryAsync());
        }
        var ultima = await EscalarTextoAsync(vivo, """
            SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1
            """);
        var snapshot = Path.Combine(area.Raiz, "consistente.db");
        await using var vacuum = vivo.CreateCommand();
        vacuum.CommandText = "VACUUM INTO $destino;";
        vacuum.Parameters.AddWithValue("$destino", snapshot);
        await vacuum.ExecuteNonQueryAsync();
        CofreEspelho.Gravar(
            area.Espelho,
            await File.ReadAllBytesAsync(snapshot),
            Senha,
            versaoSchema,
            ultima,
            DateTimeOffset.UtcNow,
            FormatoEspelho.IteracoesPadrao);
    }

    private List<string> HistoricoComFuro()
        => _versao.Migrations.Where(id => id != "20261008141717_PerfilFechamentoLogs").ToList();

    private List<string> ComExtras(params string[] extras)
        => [.. _versao.Migrations, .. extras];

    private static async Task CriarHistoricoSemTabelaDoAppAsync(string caminho)
    {
        await using var conexao = await AbrirAsync(caminho, criar: true);
        await ExecutarAsync(conexao, """
            CREATE TABLE "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            )
            """);
        foreach (var id in VersaoSchemaAplicacao.DoApp.Migrations)
        {
            await using var comando = conexao.CreateCommand();
            comando.CommandText = """
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ($id, '10.0.0')
                """;
            comando.Parameters.AddWithValue("$id", id);
            Assert.Equal(1, await comando.ExecuteNonQueryAsync());
        }
    }

    private static void ApagarLaterais(string banco)
    {
        foreach (var sufixo in new[] { "-wal", "-shm" })
        {
            var lateral = banco + sufixo;
            if (File.Exists(lateral))
                File.Delete(lateral);
        }
    }

    private async Task PublicarAsync(AreaTemp area, string nome)
    {
        await using var vivo = await CriarBancoAbertoAsync(area.Banco, nome);
        await _gerador.GerarAsync(area.Banco, Senha, new DestinoEspelhoArquivo(area.Espelho));
    }

    private static async Task<SqliteConnection> CriarBancoAbertoAsync(string caminho, string nome)
    {
        var conexao = await AbrirAsync(caminho, criar: true);
        await ExecutarAsync(conexao, "PRAGMA journal_mode = WAL;");
        await ExecutarAsync(conexao, "PRAGMA wal_autocheckpoint = 0;");
        var opcoes = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conexao).Options;
        await using (var ctx = new AppDbContext(opcoes))
        {
            await ctx.Database.MigrateAsync();
        }

        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            INSERT INTO MembrosFamilia (Nome, Avatar, Role, Ativo, CriadoEm)
            VALUES ($nome, 'M', 0, 1, '2026-03-01 00:00:00')
            """;
        comando.Parameters.AddWithValue("$nome", nome);
        Assert.Equal(1, await comando.ExecuteNonQueryAsync());
        return conexao;
    }

    private static async Task CriarBancoComForeignKeyQuebradaAsync(string caminho)
    {
        await using var conexao = await AbrirAsync(caminho, criar: true);
        await ExecutarAsync(conexao, "PRAGMA foreign_keys = OFF");
        await ExecutarAsync(conexao, "CREATE TABLE pai (id INTEGER PRIMARY KEY)");
        await ExecutarAsync(conexao, "CREATE TABLE filho (id INTEGER PRIMARY KEY, paiId INTEGER REFERENCES pai(id))");
        await ExecutarAsync(conexao, "INSERT INTO filho (id, paiId) VALUES (1, 42)");
    }

    private static async Task<SqliteConnection> AbrirAsync(string caminho, bool criar = false)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = caminho,
            Mode = criar ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite,
            Pooling = false
        };
        var conexao = new SqliteConnection(builder.ConnectionString);
        await conexao.OpenAsync();
        return conexao;
    }

    private static SqliteConnection AbrirSomenteLeitura(string caminho)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = caminho,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };
        var conexao = new SqliteConnection(builder.ConnectionString);
        conexao.Open();
        return conexao;
    }

    private static async Task ExecutarAsync(SqliteConnection conexao, string sql)
    {
        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<string> EscalarTextoAsync(SqliteConnection conexao, string sql)
    {
        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        var valor = await comando.ExecuteScalarAsync();
        return valor is null or DBNull ? "" : Convert.ToString(valor) ?? "";
    }

    private static bool TemPrefixoSqlite(byte[] espelho)
        => espelho.Length >= 15 && espelho.AsSpan(0, 15).SequenceEqual("SQLite format 3"u8);

    private static bool Contem(byte[] arquivo, byte[] agulha)
        => arquivo.AsSpan().IndexOf(agulha) >= 0;

    private static void AssertNaoVaza(Exception excecao, string senha, string caminho)
    {
        var texto = excecao.ToString();
        Assert.DoesNotContain(senha, texto, StringComparison.Ordinal);
        Assert.DoesNotContain(caminho, texto, StringComparison.Ordinal);
    }

    private sealed class DestinoQueRecusa : IDestinoEspelho
    {
        public int Chamadas { get; private set; }

        public Task PublicarAsync(string arquivoOrigem, CancellationToken cancellationToken = default)
        {
            Chamadas++;
            throw new InvalidOperationException("não deveria publicar");
        }
    }

    /// <summary>
    /// Grava o arquivo novo num temporário ao lado do espelho e falha antes da troca.
    /// </summary>
    private sealed class DestinoQueFalhaNaTroca(string caminho) : IDestinoEspelho
    {
        public int Tentativas { get; private set; }

        public bool GravouTemporario { get; private set; }

        public async Task PublicarAsync(string arquivoOrigem, CancellationToken cancellationToken = default)
        {
            Tentativas++;
            var pasta = Path.GetDirectoryName(caminho)!;
            var temporario = Path.Combine(pasta, ".publicacao-interrompida.tmp");
            await File.WriteAllBytesAsync(temporario, await File.ReadAllBytesAsync(arquivoOrigem, cancellationToken), cancellationToken);
            GravouTemporario = File.Exists(temporario);
            File.Delete(temporario);
            throw new IOException("falha na troca");
        }
    }

    private sealed class SenhaFixa(string? senha) : IProvedorSenhaEspelho
    {
        public string? Obter() => senha;

        public override string ToString() => "SenhaFixa";
    }

    private sealed class PortasCoordenador
    {
        public int Semeaduras { get; private set; }
        public int RemocoesBanco { get; private set; }
        public int RemocoesEspelho { get; private set; }
        public int Restauracoes { get; private set; }
        public int Migracoes { get; private set; }
        public int Preservacoes { get; private set; }
        public bool BancoExiste { get; init; }
        public RelatorioIntegridadeBanco? Relatorio { get; init; }
        public bool PreservacaoSucesso { get; init; } = true;
        public bool MigracaoExigeRecuperacao { get; init; }
        public OpcoesPreparoBanco Opcoes { get; } = new() { VersaoAplicativo = "1.2.3" };
        public OpcoesPreparoBanco? OpcoesRecebidas { get; private set; }

        public CoordenadorArmazenamento Criar(IEspelhoDados espelho, IVersaoSchemaApp versao)
            => Montar(espelho, versao, Opcoes, new Preservacao(this));

        public CoordenadorArmazenamento CriarSemOpcoes(IEspelhoDados espelho, IVersaoSchemaApp versao)
            => Montar(espelho, versao, null!, new Preservacao(this));

        public CoordenadorArmazenamento CriarSemPreservacao(IEspelhoDados espelho, IVersaoSchemaApp versao)
            => Montar(espelho, versao, Opcoes, null!);

        private CoordenadorArmazenamento Montar(
            IEspelhoDados espelho,
            IVersaoSchemaApp versao,
            OpcoesPreparoBanco opcoes,
            IPreservacaoBancoVivo preservacao)
            => new(
                new Sonda(this),
                espelho,
                versao,
                new Restauracao(this),
                new Migracao(this),
                new Semeadura(this),
                new Limpeza(this),
                opcoes,
                preservacao);

        private sealed class Sonda(PortasCoordenador portas) : ISondaBancoVivo
        {
            public bool Existe() => portas.BancoExiste;

            public Task<RelatorioIntegridadeBanco> VerificarAsync(CancellationToken cancellationToken = default)
            {
                if (!portas.BancoExiste)
                    throw new InvalidOperationException("banco ausente não se verifica");

                return Task.FromResult(portas.Relatorio ?? throw new InvalidOperationException("relatório ausente"));
            }
        }

        private sealed class Restauracao(PortasCoordenador portas) : IRestauracaoEspelho
        {
            public Task<ResultadoRestauracao> RestaurarAsync(CancellationToken cancellationToken = default)
            {
                portas.Restauracoes++;
                return Task.FromResult(ResultadoRestauracao.Falha("cópia incompleta"));
            }
        }

        private sealed class Migracao(PortasCoordenador portas) : IMigracaoBancoVivo
        {
            public Task<ResultadoPreparoBanco> MigrarEVerificarAsync(
                OpcoesPreparoBanco opcoes,
                CancellationToken cancellationToken = default)
            {
                ArgumentNullException.ThrowIfNull(opcoes);
                portas.Migracoes++;
                portas.OpcoesRecebidas = opcoes;
                if (portas.MigracaoExigeRecuperacao)
                {
                    return Task.FromResult(new ResultadoPreparoBanco(
                        new InvalidOperationException("schema não criado"),
                        new RelatorioIntegridadeBanco()));
                }

                throw new InvalidOperationException("não deveria migrar");
            }
        }

        private sealed class Preservacao(PortasCoordenador portas) : IPreservacaoBancoVivo
        {
            public Task<ResultadoPreservacao> PreservarAsync(CancellationToken cancellationToken = default)
            {
                portas.Preservacoes++;
                if (!portas.PreservacaoSucesso)
                    return Task.FromResult(ResultadoPreservacao.Falha("não foi possível copiar o banco ilegível"));

                return Task.FromResult(ResultadoPreservacao.Ok("copia-preservada.db"));
            }
        }

        private sealed class Semeadura(PortasCoordenador portas) : ISemeaduraDados
        {
            public Task SemearAsync(CancellationToken cancellationToken = default)
            {
                portas.Semeaduras++;
                throw new InvalidOperationException("não deveria semear");
            }
        }

        private sealed class Limpeza(PortasCoordenador portas) : ILimpezaTotalDados
        {
            public Task RemoverBancoVivoAsync(CancellationToken cancellationToken = default)
            {
                portas.RemocoesBanco++;
                throw new InvalidOperationException("não deveria apagar o banco");
            }

            public Task RemoverEspelhoAsync(CancellationToken cancellationToken = default)
            {
                portas.RemocoesEspelho++;
                throw new InvalidOperationException("não deveria apagar o espelho");
            }
        }
    }

    private sealed class AreaTemp : IDisposable
    {
        public AreaTemp(string? marca = null)
        {
            var nome = "orcafacil-pr2-" + (marca is null ? "" : marca + "-") + Guid.NewGuid().ToString("N");
            Raiz = Path.Combine(Path.GetTempPath(), nome);
            Directory.CreateDirectory(Path.Combine(Raiz, "espelho"));
        }

        public string Raiz { get; }

        public string Banco => Path.Combine(Raiz, "vivo.db");

        public string Espelho => Path.Combine(Raiz, "espelho", FormatoEspelho.NomeArquivoSugerido);

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (!Directory.Exists(Raiz))
                return;

            try
            {
                Directory.Delete(Raiz, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
