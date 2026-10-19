using Android.Content;
using OrcaFacil.Core.Armazenamento.Espelho.Pasta;
using AndroidUri = Android.Net.Uri;

namespace OrcaFacil.Platforms.Android.Espelho;

/// <summary>
/// Permissão persistente de leitura e escrita da árvore. Não apaga documento.
/// </summary>
internal static class PermissaoPastaEspelho
{
    private const ActivityFlags LeituraEEscrita =
        ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission;

    public static (bool Leitura, bool Escrita, bool Persistiu) TentarPersistir(ContentResolver? resolver, string? uriTexto)
    {
        if (resolver is null || !UriEscolhaPasta.EhUriDeConteudo(uriTexto))
            return (false, false, false);

        AndroidUri? uri;
        try
        {
            uri = AndroidUri.Parse(uriTexto);
        }
        catch (Exception)
        {
            return (false, false, false);
        }

        if (uri is null)
            return (false, false, false);

        try
        {
            resolver.TakePersistableUriPermission(uri, LeituraEEscrita);
        }
        catch (Exception)
        {
            return (false, false, false);
        }

        return Ler(resolver, uri);
    }

    public static bool Vigente(ContentResolver resolver, AndroidUri uri)
    {
        var (leitura, escrita, persistiu) = Ler(resolver, uri);
        return persistiu && leitura && escrita;
    }

    public static void Liberar(ContentResolver? resolver, string? uriTexto)
    {
        if (resolver is null || !UriEscolhaPasta.EhUriDeConteudo(uriTexto))
            return;

        try
        {
            var uri = AndroidUri.Parse(uriTexto);
            if (uri is null)
                return;

            resolver.ReleasePersistableUriPermission(uri, LeituraEEscrita);
        }
        catch (Exception)
        {
            // A URI local ainda será esquecida. Nenhum documento é apagado.
        }
    }

    private static (bool Leitura, bool Escrita, bool Persistiu) Ler(ContentResolver resolver, AndroidUri uri)
    {
        var lista = resolver.PersistedUriPermissions;
        if (lista is null)
            return (false, false, false);

        foreach (var permissao in lista)
        {
            if (permissao?.Uri is null || !permissao.Uri.Equals(uri))
                continue;

            var leitura = permissao.IsReadPermission;
            var escrita = permissao.IsWritePermission;
            return (leitura, escrita, leitura && escrita);
        }

        return (false, false, false);
    }
}
