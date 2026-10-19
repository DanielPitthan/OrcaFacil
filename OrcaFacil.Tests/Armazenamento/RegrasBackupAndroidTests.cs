using System.Xml.Linq;
using OrcaFacil.Core.Armazenamento.Backup;
using OrcaFacil.Core.Armazenamento.Espelho;
using OrcaFacil.Core.Armazenamento.Espelho.Pasta;
using OrcaFacil.Core.Tema;

namespace OrcaFacil.Tests.Armazenamento;

public class RegrasBackupAndroidTests
{
    private static readonly string[] Sidecars = ["orcafacil.db-wal", "orcafacil.db-shm", "orcafacil.db-journal"];

    [Fact]
    public void Banco_FotosEPreferencias_Entram()
    {
        Assert.True(RegrasBackupAndroid.DeveIncluir(RegrasBackupAndroid.DominioArquivo, RegrasBackupAndroid.NomeBanco));
        foreach (var sidecar in Sidecars)
            Assert.True(RegrasBackupAndroid.DeveIncluir(RegrasBackupAndroid.DominioArquivo, sidecar));

        Assert.True(RegrasBackupAndroid.DeveIncluir(RegrasBackupAndroid.DominioArquivo, "perfil"));
        Assert.True(RegrasBackupAndroid.DeveIncluir(RegrasBackupAndroid.DominioArquivo, "perfil/foto-20261017120000.jpg"));
        Assert.True(RegrasBackupAndroid.DeveIncluir(
            RegrasBackupAndroid.DominioPreferencias,
            RegrasBackupAndroid.ArquivoPreferenciasPadrao));
    }

    [Fact]
    public void Banco_NaoEstaNoDominioDatabaseDoAndroid()
    {
        Assert.False(RegrasBackupAndroid.DeveIncluir(
            RegrasBackupAndroid.DominioDatabaseDoSistema,
            RegrasBackupAndroid.NomeBanco));
        Assert.DoesNotContain(RegrasBackupAndroid.Inclusoes, regra => regra.Dominio == RegrasBackupAndroid.DominioDatabaseDoSistema);
    }

    [Fact]
    public void ChaveDoSecureStorage_FicaDeFora_ENaoSomeComAsPreferencias()
    {
        Assert.Equal(
            "com.pitthan.orcafacil.microsoft.maui.essentials.preferences",
            RegrasBackupAndroid.NomeSecureStorage);
        Assert.Equal(
            "com.pitthan.orcafacil.microsoft.maui.essentials.preferences.xml",
            RegrasBackupAndroid.ArquivoSecureStorage);
        Assert.Equal("com.pitthan.orcafacil_preferences.xml", RegrasBackupAndroid.ArquivoPreferenciasPadrao);
        Assert.NotEqual(RegrasBackupAndroid.ArquivoPreferenciasPadrao, RegrasBackupAndroid.ArquivoSecureStorage);

        var exclusao = Assert.Single(
            RegrasBackupAndroid.Exclusoes,
            regra => regra.Caminho.Contains("microsoft.maui.essentials.preferences", StringComparison.Ordinal));
        Assert.Equal(RegrasBackupAndroid.NomeSecureStorage, exclusao.Caminho);
        Assert.Equal(RegrasBackupAndroid.DominioPreferencias, exclusao.Dominio);
        Assert.False(exclusao.Diretorio);
        Assert.NotEqual(RegrasBackupAndroid.ArquivoSecureStorage, exclusao.Caminho);
        Assert.True(exclusao.Casa(
            RegrasBackupAndroid.DominioPreferencias,
            RegrasBackupAndroid.NomeSecureStorage));
        Assert.True(exclusao.Casa(
            RegrasBackupAndroid.DominioPreferencias,
            RegrasBackupAndroid.ArquivoSecureStorage));

        Assert.False(RegrasBackupAndroid.DeveIncluir(
            RegrasBackupAndroid.DominioPreferencias,
            RegrasBackupAndroid.ArquivoSecureStorage));
        Assert.False(RegrasBackupAndroid.DeveIncluir(
            RegrasBackupAndroid.DominioPreferencias,
            RegrasBackupAndroid.NomeSecureStorage));
        Assert.False(RegrasBackupAndroid.CopiadoPeloXml(
            RegrasBackupAndroid.DominioPreferencias,
            RegrasBackupAndroid.ArquivoSecureStorage));
        Assert.False(RegrasBackupAndroid.CopiadoPeloXml(
            RegrasBackupAndroid.DominioPreferencias,
            RegrasBackupAndroid.NomeSecureStorage));
        Assert.True(RegrasBackupAndroid.DeveIncluir(
            RegrasBackupAndroid.DominioPreferencias,
            RegrasBackupAndroid.ArquivoPreferenciasPadrao));
        Assert.True(RegrasBackupAndroid.CopiadoPeloXml(
            RegrasBackupAndroid.DominioPreferencias,
            RegrasBackupAndroid.ArquivoPreferenciasPadrao));

        Assert.Equal("Config.Tema", PreferenciasTema.Chave);
        Assert.Equal("Espelho.Pasta.Uri", UriEscolhaPasta.ChavePreferencia);
    }

    [Fact]
    public void Logs_BackupsDeMigracaoECacheDoEspelho_FicamDeFora()
    {
        Assert.False(RegrasBackupAndroid.DeveIncluir(RegrasBackupAndroid.DominioArquivo, "logs"));
        Assert.False(RegrasBackupAndroid.DeveIncluir(RegrasBackupAndroid.DominioArquivo, "logs/orcafacil-20261018.log"));
        Assert.False(RegrasBackupAndroid.DeveIncluir(RegrasBackupAndroid.DominioArquivo, "backups"));
        Assert.False(RegrasBackupAndroid.DeveIncluir(
            RegrasBackupAndroid.DominioArquivo,
            "backups/orcafacil-backup-20261018T000000000Z-1.0-antes-AddCartoes.db"));
        Assert.False(RegrasBackupAndroid.DeveIncluir(RegrasBackupAndroid.DominioArquivo, "backups/orcafacil.db"));
        Assert.False(RegrasBackupAndroid.DeveIncluir(RegrasBackupAndroid.DominioArquivo, "orcafacil-espelho"));
        Assert.False(RegrasBackupAndroid.DeveIncluir(
            RegrasBackupAndroid.DominioArquivo,
            "orcafacil-espelho/0123456789abcdef0123456789abcdef.bin"));
        Assert.False(RegrasBackupAndroid.DeveIncluir(
            RegrasBackupAndroid.DominioArquivo,
            "perfil/orcafacil-espelho/0123456789abcdef0123456789abcdef.bin"));
        Assert.True(RegrasBackupAndroid.CopiadoPeloXml(
            RegrasBackupAndroid.DominioArquivo,
            "perfil/orcafacil-espelho/0123456789abcdef0123456789abcdef.bin"));
    }

    [Fact]
    public void TemporarioEReserva_NaRaizFicamDeFora_EDentroDePerfilOXmlAindaCopia()
    {
        var id = Guid.Parse("0123456789abcdef0123456789abcdef");
        var temporario = NomeTemporarioEspelho.Criar(FormatoEspelho.NomeArquivoSugerido, id);
        var reserva = NomeTemporarioEspelho.CriarReserva(FormatoEspelho.NomeArquivoSugerido, id);
        var foto = "perfil/foto-20261017120000.jpg";

        Assert.False(RegrasBackupAndroid.DeveIncluir(RegrasBackupAndroid.DominioArquivo, temporario));
        Assert.False(RegrasBackupAndroid.CopiadoPeloXml(RegrasBackupAndroid.DominioArquivo, temporario));
        Assert.False(RegrasBackupAndroid.DeveIncluir(RegrasBackupAndroid.DominioArquivo, reserva));
        Assert.False(RegrasBackupAndroid.CopiadoPeloXml(RegrasBackupAndroid.DominioArquivo, reserva));
        Assert.False(RegrasBackupAndroid.DeveIncluir(RegrasBackupAndroid.DominioArquivo, FormatoEspelho.NomeArquivoSugerido));
        Assert.False(RegrasBackupAndroid.CopiadoPeloXml(RegrasBackupAndroid.DominioArquivo, FormatoEspelho.NomeArquivoSugerido));

        var perfil = Assert.Single(
            RegrasBackupAndroid.Inclusoes,
            regra => regra.Caminho == RegrasBackupAndroid.PastaFotos);
        Assert.Equal(RegrasBackupAndroid.DominioArquivo, perfil.Dominio);
        Assert.True(perfil.Diretorio);
        Assert.DoesNotContain(
            RegrasBackupAndroid.Inclusoes,
            regra => regra.Caminho.Contains("foto-", StringComparison.Ordinal) || regra.Caminho.Contains('*'));

        Assert.True(RegrasBackupAndroid.DeveIncluir(RegrasBackupAndroid.DominioArquivo, foto));
        Assert.True(RegrasBackupAndroid.CopiadoPeloXml(RegrasBackupAndroid.DominioArquivo, foto));

        Assert.False(RegrasBackupAndroid.DeveIncluir(RegrasBackupAndroid.DominioArquivo, "perfil/" + temporario));
        Assert.True(RegrasBackupAndroid.CopiadoPeloXml(RegrasBackupAndroid.DominioArquivo, "perfil/" + temporario));
        Assert.False(RegrasBackupAndroid.DeveIncluir(RegrasBackupAndroid.DominioArquivo, "perfil/" + reserva));
        Assert.True(RegrasBackupAndroid.CopiadoPeloXml(RegrasBackupAndroid.DominioArquivo, "perfil/" + reserva));
        Assert.DoesNotContain(
            RegrasBackupAndroid.Exclusoes,
            regra => regra.Casa(RegrasBackupAndroid.DominioArquivo, "perfil/" + temporario));
    }

    [Fact]
    public void SnapshotTemporario_NaoEntra_MesmoComNomeDeBanco()
    {
        Assert.False(RegrasBackupAndroid.DeveIncluir(
            RegrasBackupAndroid.DominioArquivo,
            "orcafacil-snapshot-abc/snapshot.db"));
        Assert.False(RegrasBackupAndroid.DeveIncluir(
            RegrasBackupAndroid.DominioArquivo,
            "orcafacil-snapshot-abc/orcafacil.db"));
        Assert.False(RegrasBackupAndroid.DeveIncluir(
            RegrasBackupAndroid.DominioArquivo,
            "perfil/orcafacil-snapshot-abc/snapshot.db"));
        Assert.True(RegrasBackupAndroid.CopiadoPeloXml(
            RegrasBackupAndroid.DominioArquivo,
            "perfil/orcafacil-snapshot-abc/snapshot.db"));
        Assert.False(RegrasBackupAndroid.CopiadoPeloXml(
            RegrasBackupAndroid.DominioArquivo,
            "orcafacil-snapshot-abc/snapshot.db"));
    }

    [Theory]
    [InlineData("file", "outro.txt")]
    [InlineData("file", "Orcafacil.db")]
    [InlineData("file", "orcafacil.db/extra")]
    [InlineData("file", "../orcafacil.db")]
    [InlineData("file", "/orcafacil.db")]
    [InlineData("file", "perfil/../orcafacil.db")]
    [InlineData("file", "perfil\\foto.jpg")]
    [InlineData("sharedpref", "foo.xml")]
    [InlineData("sharedpref", ".")]
    [InlineData("root", "orcafacil.db")]
    [InlineData("external", "orcafacil.db")]
    [InlineData("database", "orcafacil.db")]
    public void CaminhoForaDaTabela_NaoEntra(string dominio, string caminho)
    {
        Assert.False(RegrasBackupAndroid.DeveIncluir(dominio, caminho));
    }

    [Fact]
    public void Tabela_NaoUsaCuringa_EExclusaoNaoRepeteInclusao()
    {
        Assert.NotEmpty(RegrasBackupAndroid.Inclusoes);
        Assert.Contains(RegrasBackupAndroid.Exclusoes, regra => regra.Caminho == RegrasBackupAndroid.NomeSecureStorage);
        Assert.DoesNotContain(
            RegrasBackupAndroid.Exclusoes,
            regra => regra.Caminho == RegrasBackupAndroid.ArquivoSecureStorage);

        foreach (var regra in RegrasBackupAndroid.Inclusoes.Concat(RegrasBackupAndroid.Exclusoes))
        {
            Assert.DoesNotContain('*', regra.Caminho);
            Assert.NotEqual(".", regra.Caminho);
        }

        var inclusoes = RegrasBackupAndroid.Inclusoes.Select(regra => (regra.Dominio, regra.Caminho)).ToHashSet();
        var exclusoes = RegrasBackupAndroid.Exclusoes.Select(regra => (regra.Dominio, regra.Caminho)).ToHashSet();
        Assert.Empty(inclusoes.Intersect(exclusoes));
    }

    [Fact]
    public void Nuvem_SoComCriptografia_EOsDoisTransportesUsamAMesmaTabela()
    {
        Assert.True(RegrasBackupAndroid.NuvemSoComCriptografia);

        var raiz = EncontrarRaiz();
        var legado = XDocument.Load(Path.Combine(raiz, "OrcaFacil", "Platforms", "Android", "Resources", "xml", "backup_rules.xml"));
        var extracao = XDocument.Load(Path.Combine(raiz, "OrcaFacil", "Platforms", "Android", "Resources", "xml", "data_extraction_rules.xml"));

        var esperadoInclusoes = Pares(RegrasBackupAndroid.Inclusoes);
        var esperadoExclusoes = Pares(RegrasBackupAndroid.Exclusoes);

        var full = legado.Root;
        Assert.NotNull(full);
        Assert.Equal("full-backup-content", full!.Name.LocalName);
        Assert.Equal(Ordenar(esperadoInclusoes), Ordenar(Pares(full, "include")));
        Assert.Equal(Ordenar(esperadoExclusoes), Ordenar(Pares(full, "exclude")));
        Assert.DoesNotContain(full.Descendants(), elemento => elemento.Attribute("requireFlags") is not null);

        var regras = extracao.Root;
        Assert.NotNull(regras);
        Assert.Equal("data-extraction-rules", regras!.Name.LocalName);
        var nuvem = Assert.Single(regras.Elements("cloud-backup"));
        var troca = Assert.Single(regras.Elements("device-transfer"));
        Assert.Equal("true", (string?)nuvem.Attribute("disableIfNoEncryptionCapabilities"));
        Assert.Null(troca.Attribute("disableIfNoEncryptionCapabilities"));
        Assert.Equal(Ordenar(esperadoInclusoes), Ordenar(Pares(nuvem, "include")));
        Assert.Equal(Ordenar(esperadoExclusoes), Ordenar(Pares(nuvem, "exclude")));
        Assert.Equal(Ordenar(esperadoInclusoes), Ordenar(Pares(troca, "include")));
        Assert.Equal(Ordenar(esperadoExclusoes), Ordenar(Pares(troca, "exclude")));
        Assert.Equal(2, regras.Elements().Count());
    }

    [Fact]
    public void Manifesto_ApontaAsRegras_ENaoMexeNasQueriesDoEspelho()
    {
        var raiz = EncontrarRaiz();
        var manifesto = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Platforms", "Android", "AndroidManifest.xml"));

        Assert.Contains("android:allowBackup=\"true\"", manifesto, StringComparison.Ordinal);
        Assert.DoesNotContain("android:allowBackup=\"false\"", manifesto, StringComparison.Ordinal);
        Assert.Contains("android:fullBackupContent=\"@xml/backup_rules\"", manifesto, StringComparison.Ordinal);
        Assert.Contains("android:dataExtractionRules=\"@xml/data_extraction_rules\"", manifesto, StringComparison.Ordinal);
        Assert.Contains("android.intent.action.OPEN_DOCUMENT_TREE", manifesto, StringComparison.Ordinal);
        Assert.Contains("android.intent.action.OPEN_DOCUMENT", manifesto, StringComparison.Ordinal);
        Assert.Contains("android.intent.action.SEND", manifesto, StringComparison.Ordinal);
    }

    [Fact]
    public void Nomes_BatemComOApp_ParaATabelaNaoApontarArquivoErrado()
    {
        var raiz = EncontrarRaiz();
        var csproj = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "OrcaFacil.csproj"));
        var banco = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Data", "DbPathProvider.cs"));
        var fotos = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Services", "FotoPerfilService.cs"));
        var inicio = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Services", "InicializacaoApp.cs"));
        var backup = File.ReadAllText(Path.Combine(raiz, "OrcaFacil.Core", "Data", "BackupBanco.cs"));
        var snapshot = File.ReadAllText(Path.Combine(raiz, "OrcaFacil.Core", "Armazenamento", "Espelho", "GeradorSnapshotBanco.cs"));
        var cache = File.ReadAllText(Path.Combine(raiz, "OrcaFacil", "Services", "Espelho", "AdaptadoresPastaEspelhoMaui.cs"));

        Assert.Contains("<ApplicationId>com.pitthan.orcafacil</ApplicationId>", csproj, StringComparison.Ordinal);
        Assert.Contains("private const string NomeArquivo = \"orcafacil.db\";", banco, StringComparison.Ordinal);
        Assert.Contains("FileSystem.AppDataDirectory, \"perfil\"", fotos, StringComparison.Ordinal);
        Assert.Contains("foto-{DateTime.Now:yyyyMMddHHmmss}.jpg", fotos, StringComparison.Ordinal);
        Assert.Contains("FileSystem.AppDataDirectory, \"logs\"", inicio, StringComparison.Ordinal);
        Assert.Contains("\"backups\"", backup, StringComparison.Ordinal);
        Assert.Contains("orcafacil-snapshot-", snapshot, StringComparison.Ordinal);
        Assert.Contains("\"orcafacil-espelho\"", cache, StringComparison.Ordinal);
        Assert.Equal(RegrasBackupAndroid.RecursoFullBackup, "backup_rules");
        Assert.Equal(RegrasBackupAndroid.RecursoExtracao, "data_extraction_rules");
    }

    private static HashSet<(string Dominio, string Caminho)> Pares(IEnumerable<RegraBackupAndroid> regras)
        => regras.Select(regra => (regra.Dominio, regra.Caminho)).ToHashSet();

    private static (string Dominio, string Caminho)[] Ordenar(IEnumerable<(string Dominio, string Caminho)> pares)
        => pares.OrderBy(par => par.Dominio, StringComparer.Ordinal).ThenBy(par => par.Caminho, StringComparer.Ordinal).ToArray();

    private static HashSet<(string Dominio, string Caminho)> Pares(XElement pai, string nome)
    {
        var elementos = pai.Elements().ToList();
        Assert.All(elementos, elemento => Assert.True(elemento.Name.LocalName is "include" or "exclude"));

        return pai.Elements(nome)
            .Select(elemento =>
            {
                var atributos = elemento.Attributes().Select(atributo => atributo.Name.LocalName).OrderBy(nomeAtributo => nomeAtributo).ToArray();
                Assert.Equal(["domain", "path"], atributos);
                return ((string)elemento.Attribute("domain")!, (string)elemento.Attribute("path")!);
            })
            .ToHashSet();
    }

    private static string EncontrarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OrcaFacil", "Platforms", "Android", "AndroidManifest.xml")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar o AndroidManifest.xml a partir de " + AppContext.BaseDirectory);
    }
}
