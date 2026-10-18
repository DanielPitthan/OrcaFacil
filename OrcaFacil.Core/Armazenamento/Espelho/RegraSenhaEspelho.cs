namespace OrcaFacil.Core.Armazenamento.Espelho;

/// <summary>
/// Força mínima da senha do espelho, para a UI futura validar antes de gerar o snapshot.
/// </summary>
/// <remarks>
/// <para>
/// Aceita 12 caracteres ou mais, desde que tenha letra e não seja uma combinação óbvia.
/// Com 10 ou 11 caracteres, exige maiúscula, minúscula, número e símbolo.
/// </para>
/// <para>
/// Esquecer a senha impede restaurar este espelho. Não existe recuperação da senha nem chave reserva.
/// Informar a senha errada não apaga o arquivo.
/// </para>
/// <para>
/// O fluxo correto, quando a senha se perdeu, é gerar um novo snapshot do banco vivo:
/// um espelho novo, com uma senha nova. O espelho anterior só é substituído se essa publicação
/// concluir. Ele não é apagado automaticamente por senha esquecida, por senha incorreta
/// ou por falha no meio da publicação.
/// </para>
/// </remarks>
public static class RegraSenhaEspelho
{
    public const int TamanhoMinimo = 10;

    public const int TamanhoSemSimbolo = 12;

    public const int TamanhoMaximo = 1024;

    public const string Requisitos =
        "Use 12 caracteres ou mais, com letra, ou 10 com maiúscula, minúscula, número e símbolo. Evite combinações óbvias.";

    public const string Esquecimento =
        "Esquecer a senha impede restaurar este espelho. Não existe recuperação da senha nem chave reserva. " +
        "Informar a senha errada não apaga o arquivo. O fluxo correto é gerar um novo snapshot do banco vivo, " +
        "produzindo um espelho novo com uma senha nova. O espelho anterior só é substituído se essa publicação " +
        "concluir e não é apagado automaticamente.";

    private static readonly HashSet<string> Obvias = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password123", "password1234", "password12345",
        "senha", "senha123", "senha1234", "senha123456", "senha12345678",
        "123456", "1234567", "12345678", "123456789", "1234567890", "123456789012",
        "qwerty", "qwerty123", "qwertyuiop", "abc123", "abcdef", "abcdef123456", "abcdefghijkl",
        "admin", "admin123", "letmein", "welcome", "changeme", "iloveyou", "master",
        "orcafacil", "orcafacil123", "orcafacil1234",
        "aaaaaaaaaaaa", "111111111111"
    };

    private static readonly HashSet<string> NucleosObvios = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "senha", "qwerty", "qwertyuiop", "admin", "orcafacil",
        "abcdef", "abcdefghijkl", "letmein", "welcome", "iloveyou", "changeme", "master"
    };

    public static bool AtendeForcaMinima(string? senha)
    {
        if (string.IsNullOrWhiteSpace(senha) || senha.Length < TamanhoMinimo || senha.Length > TamanhoMaximo)
            return false;

        if (senha != senha.Trim())
            return false;

        if (EhRepetida(senha) || EhObvia(senha))
            return false;

        if (senha.Length >= TamanhoSemSimbolo)
            return TemLetra(senha);

        return TemMaiuscula(senha) && TemMinuscula(senha) && TemDigito(senha) && TemSimbolo(senha);
    }

    public static void Exigir(string senha)
    {
        if (!AtendeForcaMinima(senha))
            throw new ArgumentException(Requisitos, nameof(senha));
    }

    private static bool EhRepetida(string senha)
    {
        var primeiro = senha[0];
        foreach (var caractere in senha)
        {
            if (caractere != primeiro)
                return false;
        }

        return true;
    }

    private static bool EhObvia(string senha)
    {
        if (Obvias.Contains(senha))
            return true;

        var alfanumerica = Filtrar(senha, char.IsLetterOrDigit);
        if (alfanumerica.Length > 0 && Obvias.Contains(alfanumerica))
            return true;

        var letras = Filtrar(senha, char.IsLetter);
        return letras.Length > 0 && NucleosObvios.Contains(letras) && senha.Length < 20;
    }

    private static string Filtrar(string senha, Func<char, bool> aceita)
    {
        Span<char> buffer = stackalloc char[senha.Length];
        var n = 0;
        foreach (var caractere in senha)
        {
            if (aceita(caractere))
                buffer[n++] = char.ToLowerInvariant(caractere);
        }

        return new string(buffer[..n]);
    }

    private static bool TemLetra(string senha)
    {
        foreach (var caractere in senha)
        {
            if (char.IsLetter(caractere))
                return true;
        }

        return false;
    }

    private static bool TemMaiuscula(string senha)
    {
        foreach (var caractere in senha)
        {
            if (char.IsUpper(caractere))
                return true;
        }

        return false;
    }

    private static bool TemMinuscula(string senha)
    {
        foreach (var caractere in senha)
        {
            if (char.IsLower(caractere))
                return true;
        }

        return false;
    }

    private static bool TemDigito(string senha)
    {
        foreach (var caractere in senha)
        {
            if (char.IsDigit(caractere))
                return true;
        }

        return false;
    }

    private static bool TemSimbolo(string senha)
    {
        foreach (var caractere in senha)
        {
            if (!char.IsLetterOrDigit(caractere) && !char.IsWhiteSpace(caractere))
                return true;
        }

        return false;
    }
}
