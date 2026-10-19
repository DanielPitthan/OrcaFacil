using System.Reflection;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Armazenamento.Backup;
using OrcaFacil.Core.Armazenamento.Espelho;
using OrcaFacil.Core.Armazenamento.Espelho.Pasta;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Tema;

namespace OrcaFacil.Tests.Armazenamento;

public class BackupEspelhoAgoraTests
{
    private const string Senha = "NAO-LOGAR-SENHA-9f3a";
    private const string SenhaNova = "TrilhaAzul92";
    private const string UriPasta = "content://com.android.externalstorage.documents/tree/primary%3AOrca";

    [Fact]
    public void Provedor_GuardaSoNaInstancia_EToStringNaoTemASenha()
    {
        var provedor = new ProvedorSenhaEspelhoMemoria();
        var outro = new ProvedorSenhaEspelhoMemoria();
        Assert.False(provedor.Informada);
        Assert.Null(provedor.Obter());

        var definida = provedor.Definir(Senha, Senha);

        Assert.True(definida.Aceita);
        Assert.Null(definida.Motivo);
        Assert.True(provedor.Informada);
        Assert.Equal(Senha, provedor.Obter());
        Assert.Null(outro.Obter());
        AssertNaoVaza(provedor, Senha);
        AssertNaoVaza(definida, Senha);
        AssertNaoVazaTexto(provedor.ToString(), Senha);
        foreach (var campo in typeof(ProvedorSenhaEspelhoMemoria).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (campo.GetValue(null) is string texto)
                AssertNaoVazaTexto(texto, Senha);
        }

        provedor.Esquecer();
        Assert.False(provedor.Informada);
        Assert.Null(provedor.Obter());
        AssertNaoVaza(provedor, Senha);
    }

    [Fact]
    public void SenhaFracaOuDiferente_NaoSubstituiAQueJaEstava()
    {
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);

        var fraca = provedor.Definir("caracteres", "caracteres");
        var diferente = provedor.Definir(Senha, SenhaNova);

        Assert.False(fraca.Aceita);
        Assert.False(diferente.Aceita);
        Assert.Equal(Senha, provedor.Obter());
        Assert.DoesNotContain("caracteres", fraca.Motivo, StringComparison.Ordinal);
        Assert.DoesNotContain(Senha, diferente.Motivo, StringComparison.Ordinal);
        Assert.DoesNotContain(SenhaNova, diferente.Motivo, StringComparison.Ordinal);
        AssertNaoVaza(fraca, "caracteres");
        AssertNaoVaza(diferente, Senha);
        AssertNaoVaza(diferente, SenhaNova);
    }

    [Fact]
    public void DefinicaoELeitura_PrecisamSerOMesmoObjeto()
    {
        var volume = new VolumeMemoria();
        var destino = new DestinoEspelhoPasta(new ProtocoloPublicacaoEspelho(volume));
        var leitura = new ProvedorSenhaEspelhoMemoria();
        var definicao = new ProvedorSenhaEspelhoMemoria();

        var excecao = Assert.Throws<InvalidOperationException>(() =>
            new BackupEspelhoAgora(volume, destino, leitura, definicao, VersaoSchemaAplicacao.DoApp));

        AssertNaoVaza(excecao, Senha);
    }

    [Fact]
    public async Task SenhaFraca_NaoConsultaAPasta_NaoPublica_NaoGravaPreferencia()
    {
        var preferencias = new PreferenciasEspiao();
        var memoria = new MemoriaPastaEspelho(preferencias);
        memoria.Gravar(UriPasta);
        var volume = new VolumeMemoria();
        var anterior = new byte[] { 4, 4, 4 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        var backup = Criar(volume, provedor);

        var resultado = await backup.ExecutarAsync(Path.Combine(Path.GetTempPath(), "ausente.db"), "caracteres", "caracteres");

        Assert.False(resultado.Sucesso);
        Assert.Equal("Nada foi publicado.", resultado.Mensagem);
        Assert.DoesNotContain("caracteres", resultado.Mensagem, StringComparison.Ordinal);
        AssertNaoVaza(resultado, "caracteres");
        Assert.Equal(0, volume.Consultas);
        Assert.Equal(0, volume.CriacoesIniciadas);
        Assert.Equal(anterior, volume.Arquivos[volume.NomeArquivo]);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
        Assert.Null(provedor.Obter());
        AssertSoAUri(preferencias, "caracteres");
    }

    [Fact]
    public async Task SenhasDiferentes_NaoPublicam_ENaoTrocamASenhaMemorizada()
    {
        var volume = new VolumeMemoria();
        var anterior = new byte[] { 3, 3 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);
        var backup = Criar(volume, provedor);

        var resultado = await backup.ExecutarAsync("orcafacil.db", Senha, SenhaNova);

        Assert.False(resultado.Sucesso);
        Assert.Equal(BackupEspelhoAgora.MensagemNaoCoincide, resultado.Mensagem);
        AssertNaoVaza(resultado, Senha);
        AssertNaoVaza(resultado, SenhaNova);
        Assert.Equal(Senha, provedor.Obter());
        Assert.Equal(0, volume.Consultas);
        Assert.Equal(anterior, volume.Arquivos[volume.NomeArquivo]);
    }

    [Fact]
    public async Task SemSenha_NaoPublica()
    {
        var volume = new VolumeMemoria();
        var backup = Criar(volume, new ProvedorSenhaEspelhoMemoria());

        var resultado = await backup.ExecutarAsync("orcafacil.db", "", null);

        Assert.False(resultado.Sucesso);
        Assert.Equal(BackupEspelhoAgora.MensagemSemSenha, resultado.Mensagem);
        Assert.Equal(0, volume.Consultas);
        Assert.Equal(0, volume.CriacoesIniciadas);
    }

    [Fact]
    public async Task PastaNaoEscolhida_NaoPublica_NaoMemorizaSenha_NaoApagaOAnterior()
    {
        var volume = new VolumeMemoria { Acesso = CodigoFalhaPastaEspelho.PastaNaoEscolhida };
        var anterior = new byte[] { 8, 8, 8 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        var backup = Criar(volume, provedor);

        var resultado = await backup.ExecutarAsync("orcafacil.db", Senha, Senha);

        Assert.False(resultado.Sucesso);
        Assert.Equal(MensagensPastaEspelho.Para(CodigoFalhaPastaEspelho.PastaNaoEscolhida), resultado.Mensagem);
        AssertNaoVaza(resultado, Senha);
        Assert.Null(provedor.Obter());
        Assert.Equal(0, volume.CriacoesIniciadas);
        Assert.Equal(anterior, volume.Arquivos[volume.NomeArquivo]);
        Assert.Empty(volume.Apagados);
    }

    [Fact]
    public async Task ConsultaQueEstoura_NaoPublica()
    {
        var volume = new VolumeMemoria { EstourarConsulta = true };
        var provedor = new ProvedorSenhaEspelhoMemoria();
        var backup = Criar(volume, provedor);

        var resultado = await backup.ExecutarAsync("orcafacil.db", Senha, Senha);

        Assert.False(resultado.Sucesso);
        Assert.Equal(MensagensPastaEspelho.Para(CodigoFalhaPastaEspelho.Indisponivel), resultado.Mensagem);
        AssertNaoVaza(resultado, Senha);
        Assert.Null(provedor.Obter());
        Assert.Equal(0, volume.CriacoesIniciadas);
    }

    [Fact]
    public async Task Cancelado_NaoPublica()
    {
        var volume = new VolumeMemoria();
        var backup = Criar(volume, new ProvedorSenhaEspelhoMemoria());
        using var cancelamento = new CancellationTokenSource();
        cancelamento.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            backup.ExecutarAsync("orcafacil.db", Senha, Senha, cancelamento.Token));

        Assert.Equal(0, volume.CriacoesIniciadas);
    }

    [Fact]
    public async Task BancoAusente_ComSenhaJaMemorizada_NaoPublica()
    {
        using var area = new AreaTemp();
        var volume = new VolumeMemoria();
        var anterior = new byte[] { 1, 1, 1 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);
        var backup = Criar(volume, provedor);

        var resultado = await backup.ExecutarAsync(Path.Combine(area.Raiz, "ausente.db"), null, null);

        Assert.False(resultado.Sucesso);
        Assert.Equal(IntegridadeSnapshotException.NadaPublicado, resultado.Mensagem);
        AssertNaoVaza(resultado, Senha);
        Assert.Equal(Senha, provedor.Obter());
        Assert.Equal(0, volume.CriacoesIniciadas);
        Assert.Equal(anterior, volume.Arquivos[volume.NomeArquivo]);
    }

    [Fact]
    public async Task Backup_DeSqliteReal_PublicaPeloProtocolo_SemVazarSenha()
    {
        using var area = new AreaTemp();
        await using var vivo = await CriarBancoAbertoAsync(area.Banco, "Marina");
        var preferencias = new PreferenciasEspiao();
        var memoria = new MemoriaPastaEspelho(preferencias);
        memoria.Gravar(UriPasta);
        var volume = new VolumeMemoria();
        var provedor = new ProvedorSenhaEspelhoMemoria();
        var backup = Criar(volume, provedor);

        var resultado = await backup.ExecutarAsync(area.Banco, Senha, Senha);

        Assert.True(resultado.Sucesso);
        Assert.Equal(BackupEspelhoAgora.MensagemGravado, resultado.Mensagem);
        AssertNaoVaza(resultado, Senha);
        Assert.Equal(Senha, provedor.Obter());
        Assert.Equal("Marina", await EscalarTextoAsync(vivo, "SELECT Nome FROM MembrosFamilia"));
        var espelho = volume.Arquivos[volume.NomeArquivo];
        Assert.Equal(Encoding.ASCII.GetBytes("ORFESPEL"), espelho[..8]);
        AssertNaoContem(espelho, Senha);
        AssertNaoContem(espelho, "Marina");
        AssertNaoContemArquivo(area.Banco, Senha);
        var wal = area.Banco + "-wal";
        if (File.Exists(wal))
            AssertNaoContemArquivo(wal, Senha);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
        Assert.DoesNotContain(volume.Arquivos.Keys, nome => nome.EndsWith(".tmp", StringComparison.Ordinal));
        AssertSoAUri(preferencias, Senha);

        var copia = Path.Combine(area.Raiz, "espelho.bin");
        await File.WriteAllBytesAsync(copia, espelho);
        using (var aberto = await LerEspelho.AbrirAsync(copia, Senha, VersaoSchemaAplicacao.DoApp))
        {
            await using var snapshot = AbrirSomenteLeitura(aberto.CaminhoSqlite);
            Assert.Equal("Marina", await EscalarTextoAsync(snapshot, "SELECT Nome FROM MembrosFamilia"));
        }

        Assert.Equal(espelho, volume.Arquivos[volume.NomeArquivo]);
        Assert.True(File.Exists(area.Banco));
        var criacoes = volume.CriacoesIniciadas;

        var deNovo = await backup.ExecutarAsync(Path.Combine(area.Raiz, "ausente.db"), "", "");

        Assert.False(deNovo.Sucesso);
        Assert.Equal(IntegridadeSnapshotException.NadaPublicado, deNovo.Mensagem);
        Assert.Equal(Senha, provedor.Obter());
        Assert.Equal(criacoes, volume.CriacoesIniciadas);
        Assert.Equal(espelho, volume.Arquivos[volume.NomeArquivo]);
    }

    [Fact]
    public async Task FalhaNoMeio_MantemOEspelhoAnterior_ENaoTrocaASenha()
    {
        using var area = new AreaTemp();
        await using var vivo = await CriarBancoAbertoAsync(area.Banco, "Marina");
        var preferencias = new PreferenciasEspiao();
        new MemoriaPastaEspelho(preferencias).Gravar(UriPasta);
        var volume = new VolumeMemoria
        {
            FalhaAoCriar = CodigoFalhaPastaEspelho.SemEspaco,
            DeixarParcialAoFalharCriacao = true
        };
        var anterior = new byte[] { 7, 7, 7 };
        volume.Arquivos[volume.NomeArquivo] = anterior;
        var provedor = new ProvedorSenhaEspelhoMemoria();
        Assert.True(provedor.Definir(Senha, Senha).Aceita);
        var backup = Criar(volume, provedor);

        var resultado = await backup.ExecutarAsync(area.Banco, SenhaNova, SenhaNova);

        Assert.False(resultado.Sucesso);
        Assert.Contains("arquivo anterior foi mantido", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        AssertNaoVaza(resultado, Senha);
        AssertNaoVaza(resultado, SenhaNova);
        Assert.Equal(Senha, provedor.Obter());
        Assert.Equal(anterior, volume.Arquivos[volume.NomeArquivo]);
        Assert.DoesNotContain(volume.NomeArquivo, volume.Apagados);
        Assert.DoesNotContain("foto.jpg", volume.Apagados);
        Assert.Equal(new byte[] { 9 }, volume.Arquivos["foto.jpg"]);
        Assert.Equal("Marina", await EscalarTextoAsync(vivo, "SELECT Nome FROM MembrosFamilia"));
        AssertNaoContemArquivo(area.Banco, SenhaNova);
        AssertSoAUri(preferencias, Senha);
        AssertSoAUri(preferencias, SenhaNova);
        foreach (var par in volume.Arquivos)
            AssertNaoContem(par.Value, SenhaNova);
    }

    [Fact]
    public void MensagemDeSucesso_NaoRepeteSenhaQueCabeNaFrase()
    {
        const string senha = "gravado na pa";
        Assert.True(RegraSenhaEspelho.AtendeForcaMinima(senha));

        var resultado = ResultadoBackupEspelho.Ok(BackupEspelhoAgora.MensagemGravado, senha);

        Assert.True(resultado.Sucesso);
        Assert.DoesNotContain(senha, resultado.Mensagem, StringComparison.Ordinal);
        AssertNaoVaza(resultado, senha);
    }

    [Fact]
    public void FalhaQueEcoaASenha_PerdeOSegredo()
    {
        var resultado = ResultadoBackupEspelho.Falha("falhou " + Senha, Senha);

        Assert.False(resultado.Sucesso);
        Assert.DoesNotContain(Senha, resultado.Mensagem, StringComparison.Ordinal);
        AssertNaoVaza(resultado, Senha);
    }

    [Fact]
    public void RotuloDaPasta_NaoLevaUri()
    {
        const string uri = "content://com.android.externalstorage.documents/tree/primary%3ASegredo";
        var casos = new[]
        {
            RotuloPastaEspelho.Descrever(new DiagnosticoPastaEspelho(CodigoFalhaPastaEspelho.Nenhuma, true)),
            RotuloPastaEspelho.Descrever(new DiagnosticoPastaEspelho(CodigoFalhaPastaEspelho.Nenhuma, false)),
            RotuloPastaEspelho.Descrever(new DiagnosticoPastaEspelho(CodigoFalhaPastaEspelho.PastaNaoEscolhida, false)),
            RotuloPastaEspelho.Descrever(new DiagnosticoPastaEspelho(CodigoFalhaPastaEspelho.PermissaoRevogada, false))
        };

        Assert.Equal("Escolhida", casos[0].Valor);
        Assert.Equal("Há um espelho nesta pasta.", casos[0].Detalhe);
        Assert.Equal("Ainda não há espelho nesta pasta.", casos[1].Detalhe);
        Assert.Equal("Não escolhida", casos[2].Valor);
        Assert.Equal("Indisponível", casos[3].Valor);
        foreach (var resumo in casos)
        {
            Assert.DoesNotContain(uri, resumo.Valor, StringComparison.Ordinal);
            Assert.DoesNotContain(uri, resumo.Detalhe, StringComparison.Ordinal);
            Assert.Equal("ResumoPastaEspelho", resumo.ToString());
            Assert.DoesNotContain("content:", resumo.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SenhaNaoEntraEmPreferencias_SecureStorage_Backup_NemNaTela()
    {
        var raiz = RaizRepositorio();
        var provedor = File.ReadAllText(Path.Combine(raiz, "OrcaFacil.Core/Armazenamento/Espelho/ProvedorSenhaEspelhoMemoria.cs"));
        var backup = File.ReadAllText(Path.Combine(raiz, "OrcaFacil.Core/Armazenamento/Espelho/BackupEspelhoAgora.cs"));
        var tela = File.ReadAllText(Path.Combine(raiz, "OrcaFacil/Components/Pages/Configuracoes/ArmazenamentoPanel.razor"));
        var registro = File.ReadAllText(Path.Combine(raiz, "OrcaFacil/Services/Espelho/RegistroServicosPastaEspelho.cs"));
        var maui = File.ReadAllText(Path.Combine(raiz, "OrcaFacil/MauiProgram.cs"));

        foreach (var fonte in new[] { provedor, backup, tela })
            AssertSemPersistencia(fonte);

        Assert.Contains("AddSingleton<IProvedorSenhaEspelho, ProvedorSenhaEspelhoMemoria>", registro, StringComparison.Ordinal);
        Assert.DoesNotContain("ProvedorSenhaEspelhoAindaNaoInformada", registro, StringComparison.Ordinal);
        Assert.DoesNotContain("ProvedorSenhaEspelhoMemoria", maui, StringComparison.Ordinal);
        Assert.Contains("RegraSenhaEspelho.Esquecimento", tela, StringComparison.Ordinal);
        Assert.Contains("Fazer backup agora", tela, StringComparison.Ordinal);
        Assert.Contains("Escolher pasta", tela, StringComparison.Ordinal);
        Assert.Contains("se perde ao fechar o app", tela, StringComparison.Ordinal);
        Assert.DoesNotContain("Restaurar", tela, StringComparison.Ordinal);
        Assert.DoesNotContain("ex.Message", tela, StringComparison.Ordinal);
        foreach (var linha in tela.Split('\n'))
        {
            if (!linha.Contains("Snackbar.Add", StringComparison.Ordinal))
                continue;

            Assert.DoesNotContain("_senha", linha, StringComparison.Ordinal);
            Assert.DoesNotContain("_confirmacao", linha, StringComparison.Ordinal);
        }

        Assert.False(RegrasBackupAndroid.DeveIncluir(
            RegrasBackupAndroid.DominioPreferencias,
            RegrasBackupAndroid.ArquivoSecureStorage));
        Assert.False(RegrasBackupAndroid.CopiadoPeloXml(
            RegrasBackupAndroid.DominioPreferencias,
            RegrasBackupAndroid.ArquivoSecureStorage));
        Assert.DoesNotContain(
            RegrasBackupAndroid.Inclusoes,
            regra => regra.Caminho.Contains("senha", StringComparison.OrdinalIgnoreCase)
                || regra.Caminho.Contains("password", StringComparison.OrdinalIgnoreCase));
    }

    private static BackupEspelhoAgora Criar(VolumeMemoria volume, ProvedorSenhaEspelhoMemoria provedor)
        => new(
            volume,
            new DestinoEspelhoPasta(new ProtocoloPublicacaoEspelho(volume)),
            provedor,
            provedor,
            VersaoSchemaAplicacao.DoApp);

    private static void AssertSoAUri(PreferenciasEspiao preferencias, string senha)
    {
        Assert.Equal(UriPasta, preferencias.ValorOuNulo(UriEscolhaPasta.ChavePreferencia));
        Assert.Single(preferencias.Gravacoes);
        foreach (var (chave, valor) in preferencias.Gravacoes)
        {
            AssertNaoVazaTexto(chave, senha);
            AssertNaoVazaTexto(valor, senha);
        }
    }

    private static void AssertSemPersistencia(string fonte)
    {
        Assert.DoesNotContain("SecureStorage.", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("IPreferenciasSimples", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("Preferences.", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Maui.Storage", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("ILogger", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("Log.", fonte, StringComparison.Ordinal);
    }

    private static void AssertNaoVaza(object valor, string senha)
    {
        AssertNaoVazaTexto(valor.ToString(), senha);
        foreach (var propriedade in valor.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (propriedade.GetValue(valor) is string texto)
                AssertNaoVazaTexto(texto, senha);
        }
    }

    private static void AssertNaoVazaTexto(string? texto, string senha)
    {
        Assert.NotNull(texto);
        Assert.DoesNotContain(senha, texto, StringComparison.Ordinal);
    }

    private static void AssertNaoContem(byte[] arquivo, string texto)
        => Assert.True(arquivo.AsSpan().IndexOf(Encoding.UTF8.GetBytes(texto)) < 0);

    private static void AssertNaoContemArquivo(string caminho, string texto)
        => AssertNaoContem(File.ReadAllBytes(caminho), texto);

    private static string RaizRepositorio()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null)
        {
            if (File.Exists(Path.Combine(diretorio.FullName, "OrcaFacil.Core", "OrcaFacil.Core.csproj")))
                return diretorio.FullName;

            diretorio = diretorio.Parent;
        }

        throw new InvalidOperationException("Não achei a raiz do repositório.");
    }

    private static async Task<SqliteConnection> CriarBancoAbertoAsync(string caminho, string nome)
    {
        var conexao = await AbrirAsync(caminho);
        await ExecutarAsync(conexao, "PRAGMA journal_mode = WAL;");
        await ExecutarAsync(conexao, "PRAGMA wal_autocheckpoint = 0;");
        var opcoes = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conexao).Options;
        await using (var ctx = new AppDbContext(opcoes))
            await ctx.Database.MigrateAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            INSERT INTO MembrosFamilia (Nome, Avatar, Role, Ativo, CriadoEm)
            VALUES ($nome, 'M', 0, 1, '2026-03-01 00:00:00')
            """;
        comando.Parameters.AddWithValue("$nome", nome);
        Assert.Equal(1, await comando.ExecuteNonQueryAsync());
        return conexao;
    }

    private static async Task<SqliteConnection> AbrirAsync(string caminho)
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

    private sealed class AreaTemp : IDisposable
    {
        public AreaTemp()
        {
            Raiz = Directory.CreateTempSubdirectory("orcafacil-backup-").FullName;
            Banco = Path.Combine(Raiz, "orcafacil.db");
        }

        public string Raiz { get; }

        public string Banco { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Raiz))
                    Directory.Delete(Raiz, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private sealed class PreferenciasEspiao : IPreferenciasSimples
    {
        private readonly Dictionary<string, string> _valores = new(StringComparer.Ordinal);

        public List<(string Chave, string Valor)> Gravacoes { get; } = [];

        public bool Contem(string chave) => _valores.ContainsKey(chave);

        public string Obter(string chave, string padrao) =>
            _valores.TryGetValue(chave, out var valor) ? valor : padrao;

        public void Definir(string chave, string valor)
        {
            Gravacoes.Add((chave, valor));
            _valores[chave] = valor;
        }

        public void Limpar() => _valores.Clear();

        public string? ValorOuNulo(string chave) =>
            _valores.TryGetValue(chave, out var valor) ? valor : null;
    }
}
