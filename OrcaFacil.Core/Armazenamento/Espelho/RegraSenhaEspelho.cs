namespace OrcaFacil.Core.Armazenamento.Espelho;

/// <summary>
/// Força mínima da senha do espelho, para a UI futura validar antes de gerar o snapshot.
/// </summary>
/// <remarks>
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
    public const int TamanhoMinimo = 8;

    public const int TamanhoMaximo = 1024;

    public const string Requisitos = "Use pelo menos 8 caracteres, com letra e número.";

    public const string Esquecimento =
        "Esquecer a senha impede restaurar este espelho. Não existe recuperação da senha nem chave reserva. " +
        "Informar a senha errada não apaga o arquivo. O fluxo correto é gerar um novo snapshot do banco vivo, " +
        "produzindo um espelho novo com uma senha nova. O espelho anterior só é substituído se essa publicação " +
        "concluir e não é apagado automaticamente.";

    public static bool AtendeForcaMinima(string? senha)
    {
        if (string.IsNullOrWhiteSpace(senha) || senha.Length < TamanhoMinimo || senha.Length > TamanhoMaximo)
            return false;

        if (senha.Trim().Length < TamanhoMinimo)
            return false;

        var temLetra = false;
        var temDigito = false;
        foreach (var caractere in senha)
        {
            if (char.IsLetter(caractere))
                temLetra = true;
            else if (char.IsDigit(caractere))
                temDigito = true;
        }

        return temLetra && temDigito;
    }

    public static void Exigir(string senha)
    {
        if (!AtendeForcaMinima(senha))
            throw new ArgumentException(Requisitos, nameof(senha));
    }
}
