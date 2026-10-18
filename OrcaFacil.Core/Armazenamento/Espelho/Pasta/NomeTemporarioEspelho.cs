using System.Text.RegularExpressions;

namespace OrcaFacil.Core.Armazenamento.Espelho.Pasta;

/// <summary>
/// Nomes de exibição usados na pasta do usuário. O definitivo é
/// <see cref="FormatoEspelho.NomeArquivoSugerido"/> (<c>orcafacil.espelho</c>).
/// O temporário e a reserva só existem para a troca e são reconhecíveis pelo padrão,
/// para que um descarte não atinja outro arquivo da pasta.
/// </summary>
public static partial class NomeTemporarioEspelho
{
    public static string Criar(string nomeFinal, Guid id)
    {
        ValidarNomeFinal(nomeFinal);
        ExigirIdentificador(id);
        return nomeFinal + "." + id.ToString("N") + ".tmp";
    }

    public static string CriarReserva(string nomeFinal, Guid id)
    {
        ValidarNomeFinal(nomeFinal);
        ExigirIdentificador(id);
        return nomeFinal + ".reserva." + id.ToString("N");
    }

    public static bool EhTemporarioDesteApp(string? nome, string nomeFinal)
    {
        if (!NomeFinalValido(nomeFinal) || string.IsNullOrEmpty(nome))
            return false;

        var match = Temporario().Match(nome);
        return match.Success && string.Equals(match.Groups[1].Value, nomeFinal, StringComparison.Ordinal);
    }

    public static bool EhReservaDesteApp(string? nome, string nomeFinal)
    {
        if (!NomeFinalValido(nomeFinal) || string.IsNullOrEmpty(nome))
            return false;

        var match = Reserva().Match(nome);
        return match.Success && string.Equals(match.Groups[1].Value, nomeFinal, StringComparison.Ordinal);
    }

    public static bool EhNomeDesteApp(string? nome, string nomeFinal)
        => string.Equals(nome, nomeFinal, StringComparison.Ordinal)
            || EhTemporarioDesteApp(nome, nomeFinal)
            || EhReservaDesteApp(nome, nomeFinal);

    public static void ValidarNomeFinal(string nomeFinal)
    {
        if (!NomeFinalValido(nomeFinal))
            throw new ArgumentException("O nome do espelho não pode ser um caminho.", nameof(nomeFinal));
    }

    private static void ExigirIdentificador(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("A publicação precisa de um identificador.", nameof(id));
    }

    private static bool NomeFinalValido(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome) || nome.Length > 80)
            return false;

        if (nome is "." or ".." || nome.Contains("..", StringComparison.Ordinal))
            return false;

        foreach (var caractere in nome)
        {
            if (caractere is '/' or '\\' or ':' || char.IsControl(caractere))
                return false;
        }

        return true;
    }

    [GeneratedRegex(@"^(.+)\.([0-9a-f]{32})\.tmp$", RegexOptions.CultureInvariant)]
    private static partial Regex Temporario();

    [GeneratedRegex(@"^(.+)\.reserva\.([0-9a-f]{32})$", RegexOptions.CultureInvariant)]
    private static partial Regex Reserva();
}
