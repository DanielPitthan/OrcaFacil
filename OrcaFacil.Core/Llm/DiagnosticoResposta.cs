using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OrcaFacil.Core.Llm;

/// <summary>
/// Texto único do erro da leitura, já sanitizado, para o diálogo, a cópia e o log.
/// Não guarda token, cabeçalho de pedido, imagem nem base64 do comprovante.
/// </summary>
public sealed class DiagnosticoResposta
{
    public const int LimiteCorpo = 8000;
    public const string MarcaTruncado = "[truncado]";
    public const string MarcaBase64 = "[base64 mascarado]";
    public const string MarcaCabecalho = "[cabeçalho removido]";

    private const int TamanhoMinimoSegredo = 8;

    private static readonly Regex ValorCredencial = new(
        """(?i)(?:"|')?(?:authorization|x-api-key)(?:"|')?[ \t]*[:=][ \t]*(?:"[^"]*"|'[^']*'|[^\r\n]*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CabecalhoPedido = new(
        """(?i)(?:"|')?(?:anthropic-version|anthropic-beta|proxy-authorization)(?:"|')?[ \t]*[:=][ \t]*(?:"[^"]*"|'[^']*'|\S+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Bearer = new(
        """(?i)\bbearer\s+[A-Za-z0-9\-._~+/]+=*""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DataImage = new(
        """data:image/[a-zA-Z0-9.+-]+;base64,[A-Za-z0-9+/=]+""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex Base64Longo = new(
        """[A-Za-z0-9+/]{201,}={0,2}""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex EscapeUnicode = new(
        @"\\u([0-9a-fA-F]{4})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private DiagnosticoResposta(
        int? statusHttp,
        string? origem,
        string? modelo,
        string tipo,
        string mensagem,
        string? motivo,
        string? stopReason,
        string? finishReason,
        string? corpo,
        bool corpoTruncado,
        string texto)
    {
        StatusHttp = statusHttp;
        Origem = origem;
        Modelo = modelo;
        Tipo = tipo;
        Mensagem = mensagem;
        Motivo = motivo;
        StopReason = stopReason;
        FinishReason = finishReason;
        Corpo = corpo;
        CorpoTruncado = corpoTruncado;
        Texto = texto;
    }

    public int? StatusHttp { get; }
    public string? Origem { get; }
    public string? Modelo { get; }
    public string Tipo { get; }
    public string Mensagem { get; }
    public string? Motivo { get; }
    public string? StopReason { get; }
    public string? FinishReason { get; }
    public string? Corpo { get; }
    public bool CorpoTruncado { get; }
    public string Texto { get; }

    public static DiagnosticoResposta Criar(
        int? statusHttp,
        string? url,
        string? modelo,
        string? tipo,
        string? mensagem,
        string? motivo,
        string? stopReason,
        string? finishReason,
        string? corpo,
        bool corpoIncompleto,
        IEnumerable<string?>? segredos)
    {
        var lista = NormalizarSegredos(segredos);
        var origem = PrepararCampo(SomenteOrigem(url), lista);
        var modeloLimpo = PrepararCampo(modelo?.Trim(), lista);
        var tipoLimpo = PrepararCampo(string.IsNullOrWhiteSpace(tipo) ? "Falha" : tipo.Trim(), lista);
        var mensagemLimpa = PrepararCampo(mensagem?.Trim(), lista);
        var motivoLimpo = PrepararCampo(motivo?.Trim(), lista);
        var stopLimpo = PrepararCampo(stopReason?.Trim(), lista);
        var finishLimpo = PrepararCampo(finishReason?.Trim(), lista);

        var corpoLimpo = NeutralizarControles(Sanitizar(corpo, lista));
        var truncado = corpoIncompleto || corpoLimpo.Length > LimiteCorpo;
        corpoLimpo = Cortar(corpoLimpo);

        var corpoExibido = corpoLimpo.Length == 0 ? null : corpoLimpo;
        var texto = MontarTexto(
            statusHttp,
            origem,
            modeloLimpo,
            tipoLimpo,
            mensagemLimpa,
            motivoLimpo,
            stopLimpo,
            finishLimpo,
            corpoExibido,
            truncado);
        texto = Sanitizar(texto, lista, juntarQuebras: false);

        return new DiagnosticoResposta(
            statusHttp,
            string.IsNullOrEmpty(origem) ? null : origem,
            string.IsNullOrEmpty(modeloLimpo) ? null : modeloLimpo,
            tipoLimpo,
            mensagemLimpa,
            string.IsNullOrEmpty(motivoLimpo) ? null : motivoLimpo,
            string.IsNullOrEmpty(stopLimpo) ? null : stopLimpo,
            string.IsNullOrEmpty(finishLimpo) ? null : finishLimpo,
            corpoExibido,
            truncado,
            texto);
    }

    /// <summary>Só esquema, host e porta não padrão. Sem caminho, consulta ou usuário.</summary>
    public static string? SomenteOrigem(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return null;

        return SomenteOrigem(uri);
    }

    public static string? SomenteOrigem(Uri? uri)
    {
        if (uri is null || !uri.IsAbsoluteUri)
            return null;

        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            return null;

        if (string.IsNullOrEmpty(uri.Host))
            return null;

        var host = uri.Host;
        if (host.Contains(':', StringComparison.Ordinal) && !host.StartsWith('['))
            host = "[" + host + "]";
        var porta = uri.IsDefaultPort
            ? string.Empty
            : ":" + uri.Port.ToString(CultureInfo.InvariantCulture);
        return uri.Scheme + "://" + host + porta;
    }

    public override string ToString() => "DiagnosticoResposta";

    private static string MontarTexto(
        int? statusHttp,
        string? origem,
        string? modelo,
        string tipo,
        string? mensagem,
        string? motivo,
        string? stopReason,
        string? finishReason,
        string? corpo,
        bool truncado)
    {
        var texto = new StringBuilder();
        texto.Append("Status HTTP: ").Append(statusHttp?.ToString(CultureInfo.InvariantCulture) ?? "—").Append('\n');
        texto.Append("Endpoint: ").Append(Vazio(origem)).Append('\n');
        texto.Append("Modelo: ").Append(Vazio(modelo)).Append('\n');
        texto.Append("Tipo: ").Append(Vazio(tipo)).Append('\n');
        texto.Append("Mensagem: ").Append(Vazio(mensagem)).Append('\n');
        texto.Append("Motivo: ").Append(Vazio(motivo)).Append('\n');
        texto.Append("stop_reason: ").Append(Vazio(stopReason)).Append('\n');
        texto.Append("finish_reason: ").Append(Vazio(finishReason)).Append('\n');
        texto.Append("Corpo:").Append('\n');
        if (!string.IsNullOrEmpty(corpo))
            texto.Append(corpo);
        else
            texto.Append('—');

        if (truncado)
            texto.Append('\n').Append(MarcaTruncado);

        return texto.ToString();
    }

    private static string Vazio(string? valor) => string.IsNullOrEmpty(valor) ? "—" : valor;

    private static List<string> NormalizarSegredos(IEnumerable<string?>? segredos)
    {
        var lista = new List<string>();
        if (segredos is null)
            return lista;

        foreach (var segredo in segredos)
        {
            if (string.IsNullOrWhiteSpace(segredo))
                continue;

            var limpo = segredo.Trim();
            if (limpo.Length < TamanhoMinimoSegredo)
                continue;

            if (!lista.Contains(limpo, StringComparer.Ordinal))
                lista.Add(limpo);
        }

        lista.Sort((a, b) => b.Length.CompareTo(a.Length));
        return lista;
    }

    private static string PrepararCampo(string? texto, List<string> segredos) =>
        Cortar(NeutralizarControles(Sanitizar(texto, segredos)));

    private static string Cortar(string texto)
    {
        if (texto.Length <= LimiteCorpo)
            return texto;

        var corte = LimiteCorpo;
        if (char.IsHighSurrogate(texto[corte - 1]))
            corte--;

        return texto[..corte];
    }

    private static string NeutralizarControles(string texto)
    {
        var precisa = false;
        foreach (var caractere in texto)
        {
            if (!EhControle(caractere))
                continue;

            precisa = true;
            break;
        }

        if (!precisa)
            return texto;

        var saida = new StringBuilder(texto.Length);
        foreach (var caractere in texto)
            saida.Append(EhControle(caractere) ? ' ' : caractere);

        return saida.ToString();
    }

    private static bool EhControle(char caractere) =>
        char.IsControl(caractere) || caractere is '\u2028' or '\u2029';

    private static string DesescaparJson(string texto)
    {
        var limpo = texto;
        for (var volta = 0; volta < 8; volta++)
        {
            var passo = EscapeUnicode.Replace(limpo, match =>
            {
                var codigo = Convert.ToInt32(match.Groups[1].Value, 16);
                if (codigo is >= 0xD800 and <= 0xDFFF)
                    return " ";

                return char.ConvertFromUtf32(codigo);
            });
            passo = passo.Replace("\\/", "/", StringComparison.Ordinal).Replace("\\\"", "\"", StringComparison.Ordinal);
            if (passo == limpo)
                break;

            limpo = passo;
        }

        return limpo;
    }

    private static string JuntarQuebras(string texto)
    {
        if (texto.IndexOf('\r') < 0 &&
            texto.IndexOf('\n') < 0 &&
            texto.IndexOf('\u2028') < 0 &&
            texto.IndexOf('\u2029') < 0)
            return texto;

        var saida = new StringBuilder(texto.Length);
        foreach (var caractere in texto)
            saida.Append(caractere is '\r' or '\n' or '\u2028' or '\u2029' ? ' ' : caractere);

        return saida.ToString();
    }

    private static string Sanitizar(string? texto, List<string> segredos, bool juntarQuebras = true)
    {
        if (string.IsNullOrEmpty(texto))
            return string.Empty;

        var limpo = DesescaparJson(texto);
        if (juntarQuebras)
            limpo = JuntarQuebras(limpo);

        limpo = ValorCredencial.Replace(limpo, MarcaCabecalho);
        limpo = CabecalhoPedido.Replace(limpo, MarcaCabecalho);
        limpo = Bearer.Replace(limpo, MarcaCabecalho);
        limpo = DataImage.Replace(limpo, MarcaBase64);
        foreach (var segredo in segredos)
            limpo = limpo.Replace(segredo, "***", StringComparison.Ordinal);

        return Base64Longo.Replace(limpo, MarcaBase64);
    }
}
