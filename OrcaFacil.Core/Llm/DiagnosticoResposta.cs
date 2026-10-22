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

    /// <summary>Segredo longo demais para a busca com separador (a imagem). A troca exata basta.</summary>
    private const int TamanhoMaximoSegredoFlexivel = 2048;

    /// <summary>Abaixo disto, o segredo só sai quando forma um token inteiro.</summary>
    private const int TamanhoTokenIsolado = 8;

    /// <summary>Marca privada das quebras do texto já montado. Não pode voltar como quebra nova.</summary>
    private const char MarcaQuebra = '\uE000';

    /// <summary>Espaço, quebra, tab ou invisível entre letras e entre os caracteres da chave.</summary>
    private const string SeparadorOpcional =
        @"[\s\u00AD\u180E\u200B-\u200F\u2028\u2029\u202A-\u202E\u2060-\u2064\u2066-\u2069\uFEFF]*";

    /// <summary>Nomes cujo valor some do diagnóstico, em qualquer capitalização.</summary>
    private static readonly string[] CabecalhosSensiveis = ["authorization", "x-api-key", "api_key", "apikey"];

    private static readonly Regex ValorCredencial = CriarValorCredencial();

    private static readonly Regex CabecalhoPedido = new(
        "(?i)(?:\"|')?(?:anthropic-version|anthropic-beta|proxy-authorization)(?:\"|')?[ \t]*[:=][ \t]*(?:\"[^\"]*\"|'[^']*'|[^\\s\uE000]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Bearer = new(
        """(?i)\bbearer\s+[A-Za-z0-9\-._~+/]+=*""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DataImage = new(
        """data:image/[a-zA-Z0-9.+-]+;base64,[A-Za-z0-9+/=_-]+""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private const string AlfabetoBase64 = @"[A-Za-z0-9+/_-]";
    private const string QuebraBase64 = @"(?:\r\n|\n|\r)";

    /// <summary>
    /// Larguras de linha do bloco (76 e 64). A cauda curta só entra se fechar a linha
    /// ou se, na mesma linha, não for uma palavra (Total, Nota, OK).
    /// </summary>
    private static readonly int[] LargurasBase64 = [76, 64];

    /// <summary>
    /// Contínuo de 201, ou linhas de 76/64. O avaliador devolve o ID de um só caso.
    /// O alfabeto inclui o url-safe (- e _).
    /// </summary>
    private static readonly Regex Base64Longo = new(
        MontarBase64(),
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
        texto = SanitizarTextoMontado(texto, lista);

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
                if (codigo == MarcaQuebra || codigo is >= 0xD800 and <= 0xDFFF)
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
        if (texto.IndexOf('\r') < 0 && texto.IndexOf('\n') < 0)
            return texto;

        var saida = new StringBuilder(texto.Length);
        foreach (var caractere in texto)
            saida.Append(caractere is '\r' or '\n' ? ' ' : caractere);

        return saida.ToString();
    }

    /// <summary>
    /// O texto montado já tem as quebras do diagnóstico. Elas viram U+E000,
    /// o passe desescapa e neutraliza, e só então as quebras originais voltam.
    /// </summary>
    private static string SanitizarTextoMontado(string texto, List<string> segredos)
    {
        if (texto.IndexOf(MarcaQuebra) >= 0)
            texto = texto.Replace(MarcaQuebra, ' ');

        var protegido = texto.Replace('\n', MarcaQuebra);
        var limpo = NeutralizarControles(Sanitizar(protegido, segredos, juntarQuebras: false));
        return limpo.Replace(MarcaQuebra, '\n');
    }

    private static string Sanitizar(string? texto, List<string> segredos, bool juntarQuebras = true)
    {
        if (string.IsNullOrEmpty(texto))
            return string.Empty;

        var limpo = DesescaparJson(texto);
        limpo = DataImage.Replace(limpo, MarcaBase64);
        limpo = Base64Longo.Replace(limpo, MascararBase64);
        if (juntarQuebras)
            limpo = JuntarQuebras(limpo);

        limpo = ValorCredencial.Replace(limpo, MarcaCabecalho);
        limpo = CabecalhoPedido.Replace(limpo, MarcaCabecalho);
        limpo = Bearer.Replace(limpo, MarcaCabecalho);
        return RemoverSegredos(limpo, segredos);
    }

    private static string MontarBase64()
    {
        var blocos = new string[LargurasBase64.Length];
        for (var i = 0; i < LargurasBase64.Length; i++)
        {
            var largura = LargurasBase64[i].ToString(CultureInfo.InvariantCulture);
            blocos[i] = "(?:" + AlfabetoBase64 + "{" + largura + "}" + QuebraBase64 + "){2,}"
                + "(?:(?![A-Za-z]+[ \"'])" + AlfabetoBase64 + "{1," + largura + "}={0,2}(?="
                + QuebraBase64 + "|$|[ \"']))?";
        }

        return string.Join("|", blocos) + "|" + AlfabetoBase64 + "{201,}={0,2}";
    }

    /// <summary>
    /// Linha quebrada é base64. Contínuo de um só caso, ou só dígitos, é ID e fica.
    /// Maiúscula e minúscula juntas, ou + / =, mascaram.
    /// </summary>
    private static string MascararBase64(Match correspondencia)
    {
        var valor = correspondencia.Value;
        if (valor.IndexOf('\n') >= 0 || valor.IndexOf('\r') >= 0)
            return MarcaBase64;

        return PareceBase64Continuo(valor) ? MarcaBase64 : valor;
    }

    private static bool PareceBase64Continuo(string valor)
    {
        var minuscula = false;
        var maiuscula = false;
        var sinal = false;
        foreach (var caractere in valor)
        {
            if (caractere is >= 'a' and <= 'z')
                minuscula = true;
            else if (caractere is >= 'A' and <= 'Z')
                maiuscula = true;
            else if (caractere is '+' or '/' or '=')
                sinal = true;
        }

        return sinal || (minuscula && maiuscula);
    }

    private static Regex CriarValorCredencial()
    {
        var nomes = new string[CabecalhosSensiveis.Length];
        for (var i = 0; i < CabecalhosSensiveis.Length; i++)
            nomes[i] = Espacado(CabecalhosSensiveis[i]);

        var nome = "(?:" + string.Join("|", nomes) + ")";
        var aspas = "(?:\"|')?";
        var valor = "(?:\"[^\"]*\"|'[^']*'|[^\\r\\n" + MarcaQuebra + "]*)";
        var padrao = "(?i)" + aspas + nome + aspas + SeparadorOpcional + "[:=]" + SeparadorOpcional + valor;
        return new Regex(padrao, RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    private static string Espacado(string palavra)
    {
        var partes = new string[palavra.Length];
        for (var i = 0; i < palavra.Length; i++)
            partes[i] = Regex.Escape(palavra[i].ToString());

        return string.Join(SeparadorOpcional, partes);
    }

    private static string RemoverSegredos(string texto, List<string> segredos)
    {
        foreach (var segredo in segredos)
        {
            texto = SegredoFlexivel(segredo)
                ? RemoverSegredoFlexivel(texto, segredo)
                : texto.Replace(segredo, "***", StringComparison.Ordinal);
        }

        return texto;
    }

    private static bool SegredoFlexivel(string segredo)
    {
        if (segredo.Length > TamanhoMaximoSegredoFlexivel)
            return false;

        foreach (var caractere in segredo)
        {
            if (EhSeparador(caractere))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Uma passagem. Separador e percent-encoding (inclusive %252D) saem do texto
    /// colapsado; a busca fica proporcional ao corpo, não ao produto com o segredo.
    /// </summary>
    private static string RemoverSegredoFlexivel(string texto, string segredo)
    {
        var pontos = new List<(int Inicio, int Fim)>(texto.Length);
        var colapsado = new StringBuilder(texto.Length);
        for (var i = 0; i < texto.Length;)
        {
            if (EhSeparador(texto[i]))
            {
                i++;
                continue;
            }

            if (TentarPercentual(texto, i, out var decodificado, out var proximo))
            {
                colapsado.Append(decodificado);
                pontos.Add((i, proximo));
                i = proximo;
                continue;
            }

            colapsado.Append(texto[i]);
            pontos.Add((i, i + 1));
            i++;
        }

        var plano = colapsado.ToString();
        var saida = new StringBuilder(texto.Length);
        var origem = 0;
        var busca = 0;
        while (busca <= plano.Length - segredo.Length)
        {
            var achou = plano.IndexOf(segredo, busca, StringComparison.Ordinal);
            if (achou < 0)
                break;

            var inicio = pontos[achou].Inicio;
            var fim = pontos[achou + segredo.Length - 1].Fim;
            if (segredo.Length >= TamanhoTokenIsolado || TokenIsolado(texto, inicio, fim))
            {
                saida.Append(texto, origem, inicio - origem);
                saida.Append("***");
                origem = fim;
                busca = achou + segredo.Length;
                continue;
            }

            busca = achou + 1;
        }

        saida.Append(texto, origem, texto.Length - origem);
        return saida.ToString();
    }

    /// <summary>%XX, ou %25XX (%252D → hífen). Só ASCII; byte alto não entra no segredo.</summary>
    private static bool TentarPercentual(string texto, int posicao, out char caractere, out int proximo)
    {
        caractere = default;
        proximo = posicao;
        if ((uint)posicao >= (uint)texto.Length || texto[posicao] != '%')
            return false;

        if (!HexByte(texto, posicao + 1, out var primeiro))
            return false;

        if (primeiro == '%' && HexByte(texto, posicao + 3, out var segundo) && segundo <= 0x7F)
        {
            caractere = (char)segundo;
            proximo = posicao + 5;
            return true;
        }

        if (primeiro > 0x7F)
            return false;

        caractere = (char)primeiro;
        proximo = posicao + 3;
        return true;
    }

    private static bool HexByte(string texto, int posicao, out int valor)
    {
        valor = 0;
        if ((uint)(posicao + 1) >= (uint)texto.Length)
            return false;

        if (ValorHex(texto[posicao]) is not int alto || ValorHex(texto[posicao + 1]) is not int baixo)
            return false;

        valor = (alto << 4) | baixo;
        return true;
    }

    private static int? ValorHex(char caractere) => caractere switch
    {
        >= '0' and <= '9' => caractere - '0',
        >= 'a' and <= 'f' => caractere - 'a' + 10,
        >= 'A' and <= 'F' => caractere - 'A' + 10,
        _ => null,
    };

    private static bool TokenIsolado(string texto, int inicio, int fim)
    {
        if (inicio > 0 && EhColadoNoToken(texto[inicio - 1]))
            return false;

        if (fim >= texto.Length)
            return true;

        if (texto[fim] == '.' && PontoFinal(texto, fim))
            return true;

        return !EhColadoNoToken(texto[fim]);
    }

    /// <summary>Ponto de frase. Ponto seguido de letra ou dígito continua o host.</summary>
    private static bool PontoFinal(string texto, int indice) =>
        indice + 1 >= texto.Length || !char.IsLetterOrDigit(texto[indice + 1]);

    /// <summary>
    /// Letra, dígito, ponto, hífen ou sublinhado continuam o token.
    /// Espaço, =, aspas e dois-pontos fecham.
    /// </summary>
    private static bool EhColadoNoToken(char caractere) =>
        caractere is not (' ' or '=' or '"' or '\'' or ':')
        && (char.IsLetterOrDigit(caractere) || caractere is '.' or '-' or '_');

    private static bool EhSeparador(char caractere) =>
        char.IsWhiteSpace(caractere) || caractere is '\u00AD' or '\u180E' or '\u200B' or '\u200C' or '\u200D'
            or '\u200E' or '\u200F' or '\u2028' or '\u2029' or '\uFEFF' or '\u2060'
            or >= '\u202A' and <= '\u202E'
            or >= '\u2061' and <= '\u2064'
            or >= '\u2066' and <= '\u2069';
}
