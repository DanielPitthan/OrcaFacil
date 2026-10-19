namespace OrcaFacil.Core.Armazenamento.Espelho;

/// <summary>
/// Tira a senha de uma frase que vai para a tela. A frase reserva entra no lugar
/// quando a preferida contém o que foi digitado.
/// </summary>
internal static class TextoSemSegredoEspelho
{
    public static string Limpar(string preferida, string reserva, params string?[] segredos)
    {
        if (Seguro(preferida, segredos))
            return preferida;

        if (Seguro(reserva, segredos))
            return reserva;

        var texto = "Erro.";
        foreach (var segredo in segredos)
        {
            if (string.IsNullOrEmpty(segredo))
                continue;

            texto = texto.Replace(segredo, "", StringComparison.Ordinal);
        }

        return texto.Length == 0 ? "Erro" : texto;
    }

    private static bool Seguro(string? mensagem, string?[] segredos)
    {
        if (string.IsNullOrEmpty(mensagem))
            return false;

        foreach (var segredo in segredos)
        {
            if (!string.IsNullOrEmpty(segredo) && mensagem.Contains(segredo, StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}
