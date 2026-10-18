using OrcaFacil.Core.Armazenamento.Espelho;
using OrcaFacil.Core.Armazenamento.Espelho.Pasta;

namespace OrcaFacil.Core.Armazenamento.Backup;

/// <summary>
/// O que o Auto Backup do Android leva na troca de aparelho e na nuvem.
/// A tabela é a mesma nos dois transportes. O XML em
/// <c>Platforms/Android/Resources/xml</c> só repete estas linhas: o sistema
/// não lê esta classe.
/// </summary>
/// <remarks>
/// O <c>orcafacil.db</c> fica em <c>getFilesDir()</c> (<see cref="DominioArquivo"/>),
/// não em <c>getDatabasePath()</c>. Incluir o domínio <c>database</c> não traz o banco.
///
/// Preferences do MAUI, com nome nulo, grava
/// <c>{id}_preferences.xml</c>. Tema, avisos e a URI da pasta do espelho viajam nesse arquivo.
/// A chave do SecureStorage é outro arquivo,
/// <c>{id}.microsoft.maui.essentials.preferences.xml</c>, cifrado com chave do Keystore
/// que o backup não leva. Excluir esse arquivo é a mesma regra da issue #34:
/// não criar um segundo XML de backup.
///
/// Temporário e reserva da publicação (<c>orcafacil.espelho.{guid}.tmp</c> e
/// <c>orcafacil.espelho.reserva.{guid}</c>) não têm caminho fixo. O atributo
/// <c>path</c> não aceita curinga nem expressão regular, então
/// <c>perfil/foto-*.jpg</c> não é uma regra válida. A foto é
/// <c>foto-</c> mais <c>yyyyMMddHHmmss</c> mais <c>.jpg</c>, um nome novo a cada
/// gravação: não dá para enumerar o arquivo. O include é a pasta <c>perfil</c> inteira.
/// Hoje o app só grava esse JPEG ali.
///
/// <see cref="DeveIncluir"/> ainda recusa temporário, reserva, <c>orcafacil-espelho</c>
/// e <c>orcafacil-snapshot-*</c> mesmo dentro de <c>perfil</c>. Isso não vai para o XML.
/// <see cref="CopiadoPeloXml"/> é o que o sistema faz: a pasta entra inteira, e um
/// resíduo com esses nomes dentro dela é copiado. Na raiz de <c>getFilesDir()</c>
/// esses nomes continuam de fora, porque não há include que os alcance.
/// A pasta do usuário (SAF) nem entra no domínio do app. O cache
/// (<c>getCacheDir()</c>) o sistema já não copia; a exclusão da pasta
/// <c>orcafacil-espelho</c> cobre o caso de ela aparecer na raiz de <c>getFilesDir()</c>.
///
/// Logs e a pasta <c>backups</c> (cópias de migração, sem descarte) ficam de fora
/// para não estourar a cota de 25 MB da nuvem, que cancela o backup inteiro.
/// A nuvem só sobe se o aparelho cifra o backup. A troca de aparelho não depende disso.
/// <c>requireFlags</c> não entra no XML legado: o app ainda abre no Android 7 (API 24).
/// </remarks>
public static class RegrasBackupAndroid
{
    public const string IdAplicativo = "com.pitthan.orcafacil";

    public const string NomeBanco = "orcafacil.db";

    public const string PastaFotos = "perfil";

    public const string PastaLogs = "logs";

    public const string PastaBackupsMigracao = "backups";

    public const string PastaCacheEspelho = "orcafacil-espelho";

    public const string PrefixoPastaSnapshot = "orcafacil-snapshot-";

    public const string DominioArquivo = "file";

    public const string DominioPreferencias = "sharedpref";

    /// <summary><c>getDatabasePath()</c>. O banco deste app não mora aqui.</summary>
    public const string DominioDatabaseDoSistema = "database";

    public const string RecursoFullBackup = "backup_rules";

    public const string RecursoExtracao = "data_extraction_rules";

    public const bool NuvemSoComCriptografia = true;

    private static readonly string[] SufixosSidecar = ["-wal", "-shm", "-journal"];

    public static string NomePreferenciasPadrao => IdAplicativo + "_preferences";

    public static string ArquivoPreferenciasPadrao => NomePreferenciasPadrao + ".xml";

    /// <summary>Nome passado ao EncryptedSharedPreferences. No disco o arquivo tem <c>.xml</c>.</summary>
    public static string NomeSecureStorage => IdAplicativo + ".microsoft.maui.essentials.preferences";

    public static string ArquivoSecureStorage => NomeSecureStorage + ".xml";

    public static IReadOnlyList<RegraBackupAndroid> Inclusoes { get; } = CriarInclusoes();

    public static IReadOnlyList<RegraBackupAndroid> Exclusoes { get; } = CriarExclusoes();

    /// <summary>
    /// Política do Core: a tabela do XML, e ainda recusa resíduo do espelho.
    /// A recusa extra não está no XML. Para o que o Android copia, use <see cref="CopiadoPeloXml"/>.
    /// </summary>
    public static bool DeveIncluir(string dominio, string caminhoRelativo)
    {
        if (!TentarNormalizar(caminhoRelativo, out var caminho))
            return false;

        if (EhResiduoDeEspelho(caminho))
            return false;

        return CopiadoPeloXmlNormalizado(dominio, caminho);
    }

    /// <summary>
    /// O que os XML copiam: <see cref="Inclusoes"/> menos <see cref="Exclusoes"/>.
    /// Não aplica a recusa de resíduo do espelho. A pasta <c>perfil</c> entra inteira.
    /// </summary>
    public static bool CopiadoPeloXml(string dominio, string caminhoRelativo)
    {
        if (!TentarNormalizar(caminhoRelativo, out var caminho))
            return false;

        return CopiadoPeloXmlNormalizado(dominio, caminho);
    }

    private static bool CopiadoPeloXmlNormalizado(string dominio, string caminho)
    {
        if (Exclusoes.Any(regra => regra.Casa(dominio, caminho)))
            return false;

        return Inclusoes.Any(regra => regra.Casa(dominio, caminho));
    }

    private static List<RegraBackupAndroid> CriarInclusoes()
    {
        var lista = new List<RegraBackupAndroid>
        {
            Regra(DominioArquivo, NomeBanco, diretorio: false)
        };

        foreach (var sufixo in SufixosSidecar)
            lista.Add(Regra(DominioArquivo, NomeBanco + sufixo, diretorio: false));

        lista.Add(Regra(DominioArquivo, PastaFotos, diretorio: true));
        lista.Add(Regra(DominioPreferencias, ArquivoPreferenciasPadrao, diretorio: false));
        return lista;
    }

    private static List<RegraBackupAndroid> CriarExclusoes()
        =>
        [
            Regra(DominioPreferencias, ArquivoSecureStorage, diretorio: false),
            Regra(DominioArquivo, PastaLogs, diretorio: true),
            Regra(DominioArquivo, PastaBackupsMigracao, diretorio: true),
            Regra(DominioArquivo, PastaCacheEspelho, diretorio: true)
        ];

    private static RegraBackupAndroid Regra(string dominio, string caminho, bool diretorio)
    {
        if (dominio is not (DominioArquivo or DominioPreferencias))
            throw new InvalidOperationException("Domínio de backup fora de file e sharedpref.");

        if (string.IsNullOrWhiteSpace(caminho)
            || caminho is "." or ".."
            || caminho.Contains('*')
            || caminho.Contains('?')
            || caminho.Contains('\\')
            || caminho.Contains("..", StringComparison.Ordinal)
            || caminho.StartsWith('/')
            || caminho.EndsWith('/'))
            throw new InvalidOperationException("Caminho de backup que o Android não aceita.");

        return new RegraBackupAndroid(dominio, caminho, diretorio);
    }

    /// <summary>
    /// Nomes que o Core recusa e que o XML não consegue nomear.
    /// Dentro de <c>perfil</c>, <see cref="CopiadoPeloXml"/> continua verdadeiro.
    /// </summary>
    private static bool EhResiduoDeEspelho(string caminho)
    {
        var partes = caminho.Split('/');
        foreach (var parte in partes)
        {
            if (string.Equals(parte, PastaCacheEspelho, StringComparison.Ordinal))
                return true;

            if (parte.StartsWith(PrefixoPastaSnapshot, StringComparison.Ordinal))
                return true;
        }

        var nome = partes[^1];
        return NomeTemporarioEspelho.EhTemporarioDesteApp(nome, FormatoEspelho.NomeArquivoSugerido)
            || NomeTemporarioEspelho.EhReservaDesteApp(nome, FormatoEspelho.NomeArquivoSugerido);
    }

    private static bool TentarNormalizar(string? caminho, out string normalizado)
    {
        normalizado = "";
        if (string.IsNullOrWhiteSpace(caminho) || caminho.Contains('\\'))
            return false;

        var texto = caminho;
        while (texto.StartsWith("./", StringComparison.Ordinal))
            texto = texto[2..];

        if (texto.Length == 0 || texto.StartsWith('/') || texto.Contains(':'))
            return false;

        var partes = texto.Split('/');
        foreach (var parte in partes)
        {
            if (parte.Length == 0 || parte is "." or "..")
                return false;
        }

        normalizado = texto;
        return true;
    }
}

/// <summary>Uma linha de <c>include</c> ou <c>exclude</c>. Diretório vale para os filhos.</summary>
public sealed record RegraBackupAndroid(string Dominio, string Caminho, bool Diretorio)
{
    public bool Casa(string dominio, string caminho)
    {
        if (!string.Equals(Dominio, dominio, StringComparison.Ordinal))
            return false;

        if (string.Equals(Caminho, caminho, StringComparison.Ordinal))
            return true;

        return Diretorio && caminho.StartsWith(Caminho + "/", StringComparison.Ordinal);
    }
}
