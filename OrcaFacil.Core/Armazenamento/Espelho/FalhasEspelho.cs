namespace OrcaFacil.Core.Armazenamento.Espelho;

/// <summary>Erro de leitura do espelho. A mensagem não traz senha, chave, conteúdo nem caminho.</summary>
public enum CodigoFalhaEspelho
{
    SenhaIncorreta,
    ArquivoCorrompido,
    VersaoFormatoNaoSuportada,
    SchemaMaisNovoQueOApp
}

public abstract class FalhaEspelhoException : Exception
{
    protected FalhaEspelhoException(string mensagem) : base(mensagem)
    {
    }

    public abstract CodigoFalhaEspelho Codigo { get; }
}

/// <summary>A senha não abre o espelho. O arquivo permanece no lugar.</summary>
public sealed class SenhaIncorretaException : FalhaEspelhoException
{
    public SenhaIncorretaException() : base("Senha incorreta.")
    {
    }

    public override CodigoFalhaEspelho Codigo => CodigoFalhaEspelho.SenhaIncorreta;
}

/// <summary>
/// Magic inválido, arquivo truncado, adulterado, ou snapshot que não passa
/// em <c>integrity_check</c> / <c>foreign_key_check</c>.
/// </summary>
public sealed class ArquivoCorrompidoException : FalhaEspelhoException
{
    public ArquivoCorrompidoException() : base("Arquivo de espelho corrompido.")
    {
    }

    public override CodigoFalhaEspelho Codigo => CodigoFalhaEspelho.ArquivoCorrompido;
}

public sealed class VersaoFormatoNaoSuportadaException : FalhaEspelhoException
{
    public VersaoFormatoNaoSuportadaException(ushort versaoEncontrada)
        : base("Versão de formato não suportada.")
    {
        VersaoEncontrada = versaoEncontrada;
    }

    public ushort VersaoEncontrada { get; }

    public override CodigoFalhaEspelho Codigo => CodigoFalhaEspelho.VersaoFormatoNaoSuportada;
}

/// <summary>
/// O espelho autenticou e o SQLite passou na integridade, mas o schema é mais novo que este app.
/// A versão fica disponível para a matriz pedir atualização, sem copiar o arquivo para o banco vivo.
/// </summary>
public sealed class SchemaMaisNovoQueOAppException : FalhaEspelhoException
{
    public SchemaMaisNovoQueOAppException(int versaoEspelho, int versaoApp)
        : base("O espelho foi gravado por uma versão mais nova do app.")
    {
        VersaoEspelho = versaoEspelho;
        VersaoApp = versaoApp;
    }

    public int VersaoEspelho { get; }

    public int VersaoApp { get; }

    public override CodigoFalhaEspelho Codigo => CodigoFalhaEspelho.SchemaMaisNovoQueOApp;
}

/// <summary>O snapshot não foi publicado. O espelho anterior, se existir, permanece.</summary>
public sealed class IntegridadeSnapshotException : Exception
{
    public IntegridadeSnapshotException(string mensagem) : base(mensagem)
    {
    }

    public const string NadaPublicado = "Não foi possível gerar um snapshot íntegro. Nada foi publicado.";

    public const string IntegrityCheck = "O snapshot não passou no PRAGMA integrity_check. Nada foi publicado.";

    public const string ForeignKeyCheck = "O snapshot não passou no PRAGMA foreign_key_check. Nada foi publicado.";
}
