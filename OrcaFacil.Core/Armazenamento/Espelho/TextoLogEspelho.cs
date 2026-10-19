using System.Text.RegularExpressions;

namespace OrcaFacil.Core.Armazenamento.Espelho;

/// <summary>
/// Texto que pode ir ao log do espelho. Tira caminho e URI. Não anexa a exceção crua.
/// </summary>
public static class TextoLogEspelho
{
    public const string Oculto = "(omitido)";

    private static readonly Regex Uri = new(
        @"[A-Za-z][A-Za-z0-9+.-]*://\S+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CaminhoWindows = new(
        @"[A-Za-z]:\\[^\s'""]+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CaminhoUnix = new(
        @"(?<![A-Za-z0-9])/(?:[^/\s'""]+/)*[^/\s'""]+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string Montar(string? mensagem, object?[]? argumentos, Exception? excecao)
    {
        var partes = new List<string>();
        var texto = Sanitizar(mensagem);
        if (texto.Length > 0)
            partes.Add(texto);

        if (argumentos is not null)
        {
            foreach (var argumento in argumentos)
            {
                var item = Sanitizar(argumento?.ToString());
                if (item.Length > 0)
                    partes.Add(item);
            }
        }

        if (excecao is not null)
            partes.Add(Nome(excecao) + ": " + Sanitizar(LerMensagem(excecao)));

        return string.Join(' ', partes);
    }

    public static string Sanitizar(string? texto)
    {
        if (string.IsNullOrEmpty(texto))
            return "";

        var semUri = Uri.Replace(texto, Oculto);
        var semWindows = CaminhoWindows.Replace(semUri, Oculto);
        return CaminhoUnix.Replace(semWindows, Oculto);
    }

    private static string Nome(Exception excecao)
    {
        try
        {
            return excecao.GetType().Name;
        }
        catch (Exception)
        {
            return "Exception";
        }
    }

    private static string LerMensagem(Exception excecao)
    {
        try
        {
            return excecao.Message;
        }
        catch (Exception)
        {
            return "";
        }
    }
}
