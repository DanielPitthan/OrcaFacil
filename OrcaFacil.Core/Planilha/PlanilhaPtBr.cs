using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace OrcaFacil.Core.Planilha;

/// <summary>
/// Grava células no padrão pt-BR. Data e moeda são valores nativos do Excel;
/// booleano vira o texto Sim/Não. Texto que o Excel poderia executar como fórmula
/// entra com prefixo de apóstrofo. A largura da coluna é fixa: medir o conteúdo
/// no Android procura uma fonte que não está no aparelho.
/// </summary>
public static class PlanilhaPtBr
{
    public const int MaximoCaracteresAba = 31;
    public const int MaximoCaracteresCelula = 32_767;
    public const int MaximoLinhas = 1_048_576;
    public const int MaximoColunas = 16_384;

    public const string FormatoData = "dd/MM/yyyy";
    public const string FormatoDataHora = "dd/MM/yyyy HH:mm";
    public const string FormatoMoeda = "\"R$\" #,##0.00";

    public const string Sim = "Sim";
    public const string Nao = "Não";

    /// <summary>Largura em caracteres da fonte padrão, sem medir o texto.</summary>
    public const double LarguraColuna = 22;

    internal static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    internal static readonly TimeZoneInfo FusoBrasil = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    private static readonly char[] CaracteresProibidosNaAba = [':', '\\', '/', '?', '*', '[', ']'];

    public static IXLWorksheet CriarAba(XLWorkbook pasta, string nome)
    {
        ArgumentNullException.ThrowIfNull(pasta);
        ValidarNomeAba(nome);
        return pasta.AddWorksheet(nome);
    }

    /// <summary>Célula dentro do teto de linhas e colunas do Excel.</summary>
    public static IXLCell Celula(IXLWorksheet aba, int linha, int coluna)
    {
        ArgumentNullException.ThrowIfNull(aba);
        if (linha < 1 || linha > MaximoLinhas)
            throw new ArgumentOutOfRangeException(nameof(linha), $"A planilha aceita no máximo {MaximoLinhas} linhas.");
        if (coluna < 1 || coluna > MaximoColunas)
            throw new ArgumentOutOfRangeException(nameof(coluna), $"A planilha aceita no máximo {MaximoColunas} colunas.");

        return aba.Cell(linha, coluna);
    }

    public static void GravarCabecalho(IXLWorksheet aba, IReadOnlyList<string> colunas)
    {
        ArgumentNullException.ThrowIfNull(aba);
        ArgumentNullException.ThrowIfNull(colunas);
        if (colunas.Count == 0)
            throw new ArgumentException("Informe ao menos uma coluna.", nameof(colunas));
        if (colunas.Count > MaximoColunas)
            throw new ArgumentException($"A planilha aceita no máximo {MaximoColunas} colunas.", nameof(colunas));

        for (var i = 0; i < colunas.Count; i++)
        {
            var celula = Celula(aba, 1, i + 1);
            GravarTexto(celula, colunas[i]);
            celula.Style.Font.Bold = true;
            aba.Column(i + 1).Width = LarguraColuna;
        }

        aba.SheetView.FreezeRows(1);
    }

    public static void GravarData(IXLCell celula, DateTime? data)
    {
        GarantirCelula(celula);
        if (!DataGravavel(data, out var instante))
        {
            Limpar(celula);
            return;
        }

        var dia = DateTime.SpecifyKind(EmSaoPaulo(instante).Date, DateTimeKind.Unspecified);
        if (!CabeNoExcel(dia))
        {
            Limpar(celula);
            return;
        }

        celula.Value = dia;
        celula.Style.DateFormat.Format = FormatoData;
    }

    public static void GravarData(IXLCell celula, DateOnly? data)
    {
        GarantirCelula(celula);
        if (data is null)
        {
            Limpar(celula);
            return;
        }

        var dia = data.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (!CabeNoExcel(dia))
        {
            Limpar(celula);
            return;
        }

        celula.Value = dia;
        celula.Style.DateFormat.Format = FormatoData;
    }

    public static void GravarDataHora(IXLCell celula, DateTime? data)
    {
        GarantirCelula(celula);
        if (!DataGravavel(data, out var instante))
        {
            Limpar(celula);
            return;
        }

        var noBrasil = EmSaoPaulo(instante);
        if (!CabeNoExcel(noBrasil))
        {
            Limpar(celula);
            return;
        }

        var semSegundos = new DateTime(
            noBrasil.Year,
            noBrasil.Month,
            noBrasil.Day,
            noBrasil.Hour,
            noBrasil.Minute,
            0,
            DateTimeKind.Unspecified);
        celula.Value = semSegundos;
        celula.Style.DateFormat.Format = FormatoDataHora;
    }

    /// <summary>
    /// Moeda como número do Excel quando o decimal sobrevive à ida e volta por double.
    /// Com cerca de 15 dígitos significativos o double arredondaria; nesse caso o
    /// decimal entra como texto, com todos os dígitos.
    /// </summary>
    public static void GravarMoeda(IXLCell celula, decimal? valor)
    {
        GarantirCelula(celula);
        if (valor is null)
        {
            Limpar(celula);
            return;
        }

        if (!CabeEmDouble(valor.Value))
        {
            GravarTexto(celula, TextoExato(valor.Value, moeda: true));
            return;
        }

        celula.Value = (double)valor.Value;
        celula.Style.NumberFormat.Format = FormatoMoeda;
    }

    /// <summary>
    /// Número sem símbolo de moeda. Meta de redução, quantidade e percentual.
    /// Acima de cerca de 15 dígitos significativos, texto exato em vez de double arredondado.
    /// </summary>
    public static void GravarNumero(IXLCell celula, decimal? valor)
    {
        GarantirCelula(celula);
        if (valor is null)
        {
            Limpar(celula);
            return;
        }

        if (!CabeEmDouble(valor.Value))
        {
            GravarTexto(celula, TextoExato(valor.Value, moeda: false));
            return;
        }

        celula.Value = (double)valor.Value;
    }

    /// <summary>Parcela, mês, ano e ordem. O inteiro cabe no double sem perder dígito.</summary>
    public static void GravarNumero(IXLCell celula, int? valor)
        => GravarNumero(celula, valor is null ? null : (decimal)valor.Value);

    public static void GravarSimNao(IXLCell celula, bool valor)
    {
        GarantirCelula(celula);
        GravarTexto(celula, valor ? Sim : Nao);
    }

    public static void GravarTexto(IXLCell celula, string? texto)
    {
        GarantirCelula(celula);
        var bruto = SemControle(TrocarSurrogateSolto(texto ?? ""));
        var neutralizar = PrecisaNeutralizar(bruto);
        var valor = Cortar(bruto);
        if (PrecisaNeutralizar(valor))
            neutralizar = true;

        if (valor.Length == 0)
        {
            Limpar(celula);
            return;
        }

        // O ClosedXML trata o apóstrofo inicial como prefixo de texto e o remove do valor.
        // Duplicar preserva um apóstrofo que já faz parte do texto. O construtor recusa
        // mais de 32.767 caracteres antes dessa remoção, então o texto no teto cede um.
        if (valor[0] == '\'' && valor.Length == MaximoCaracteresCelula)
        {
            var limite = MaximoCaracteresCelula - 1;
            if (limite > 0 && char.IsLowSurrogate(valor[limite]))
                limite--;
            valor = valor[..limite];
        }

        celula.Value = valor[0] == '\'' ? "'" + valor : valor;
        if (neutralizar)
            celula.Style.IncludeQuotePrefix = true;
    }

    private static void GarantirCelula(IXLCell celula)
    {
        ArgumentNullException.ThrowIfNull(celula);
        var linha = celula.Address.RowNumber;
        var coluna = celula.Address.ColumnNumber;
        if (linha < 1 || linha > MaximoLinhas)
            throw new ArgumentOutOfRangeException(nameof(celula), $"A planilha aceita no máximo {MaximoLinhas} linhas.");
        if (coluna < 1 || coluna > MaximoColunas)
            throw new ArgumentOutOfRangeException(nameof(celula), $"A planilha aceita no máximo {MaximoColunas} colunas.");
    }

    private static void Limpar(IXLCell celula)
    {
        celula.Clear(XLClearOptions.Contents);
    }

    private static bool DataGravavel(DateTime? data, out DateTime instante)
    {
        instante = default;
        if (data is null || !CabeNoExcel(data.Value))
            return false;

        instante = data.Value;
        return true;
    }

    /// <summary>Serial negativo ou anterior a 1900 não é data de Excel. <see cref="DateTime.MinValue"/> cai aqui.</summary>
    private static bool CabeNoExcel(DateTime data)
        => data.Year is >= 1900 and <= 9999;

    private static DateTime EmSaoPaulo(DateTime instante)
    {
        if (instante.Kind != DateTimeKind.Utc)
            return instante;

        return TimeZoneInfo.ConvertTimeFromUtc(instante, FusoBrasil);
    }

    /// <summary>
    /// O double tem cerca de 15 dígitos significativos. <see cref="decimal.MaxValue"/>
    /// vira um double finito e o cast de volta lança <see cref="OverflowException"/>.
    /// Os dois casos, e um double não finito, contam como "não cabe": o texto exato entra no lugar.
    /// </summary>
    private static bool CabeEmDouble(decimal valor)
    {
        try
        {
            var numero = (double)valor;
            if (!double.IsFinite(numero))
                return false;

            return (decimal)numero == valor;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static string TextoExato(decimal valor, bool moeda)
    {
        var numero = valor.ToString("0.#############################", Cultura);
        return moeda ? "R$ " + numero : numero;
    }

    private static void ValidarNomeAba(string nome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);

        if (nome.Equals("History", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("O nome History é reservado pelo Excel.", nameof(nome));

        if (nome.Length > MaximoCaracteresAba)
            throw new ArgumentException(
                $"O nome da aba pode ter no máximo {MaximoCaracteresAba} caracteres.",
                nameof(nome));

        if (nome[0] == '\'' || nome[^1] == '\'')
            throw new ArgumentException("O nome da aba não pode começar ou terminar com apóstrofo.", nameof(nome));

        if (!string.Equals(nome, nome.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("O nome da aba não pode começar ou terminar com espaço.", nameof(nome));

        if (nome.IndexOfAny(CaracteresProibidosNaAba) >= 0)
            throw new ArgumentException("O nome da aba não pode conter : \\ / ? * [ ].", nameof(nome));
    }

    /// <summary>
    /// = + - @ no início, ou tab, CR e LF, fazem o Excel tratar a célula como fórmula.
    /// O mesmo vale para os fullwidth ＝ ＋ － ＠ e para o menos U+2212.
    /// Antes do gatilho, espaço em branco (<see cref="Rune.IsWhiteSpace"/>),
    /// formato (Cf) e separadores Zs, Zl e Zp são ignorados. Assim um NBSP,
    /// U+2000–U+200A, U+202F, U+205F, U+1680, U+2028 ou U+2029 não esconde o =.
    /// </summary>
    private static bool PrecisaNeutralizar(string texto)
    {
        for (var i = 0; i < texto.Length;)
        {
            // Surrogate solto não é escalar. Vira U+FFFD e a leitura segue um char.
            if (!Rune.TryGetRuneAt(texto, i, out var rune))
            {
                rune = Rune.ReplacementChar;
                i++;
            }
            else
            {
                i += rune.Utf16SequenceLength;
            }

            if (IgnoravelNoInicio(rune))
                continue;

            return GatilhoDeFormula(rune);
        }

        return false;
    }

    /// <summary>Par incompleto não pode ir para o XML. Cada um vira U+FFFD.</summary>
    private static string TrocarSurrogateSolto(string texto)
    {
        var temSolto = false;
        for (var i = 0; i < texto.Length; i++)
        {
            if (char.IsHighSurrogate(texto[i]))
            {
                if (i + 1 >= texto.Length || !char.IsLowSurrogate(texto[i + 1]))
                {
                    temSolto = true;
                    break;
                }

                i++;
            }
            else if (char.IsLowSurrogate(texto[i]))
            {
                temSolto = true;
                break;
            }
        }

        if (!temSolto)
            return texto;

        var resultado = new StringBuilder(texto.Length);
        for (var i = 0; i < texto.Length; i++)
        {
            if (char.IsHighSurrogate(texto[i]) && i + 1 < texto.Length && char.IsLowSurrogate(texto[i + 1]))
            {
                resultado.Append(texto[i]);
                resultado.Append(texto[i + 1]);
                i++;
            }
            else if (char.IsSurrogate(texto[i]))
            {
                resultado.Append('\uFFFD');
            }
            else
            {
                resultado.Append(texto[i]);
            }
        }

        return resultado.ToString();
    }

    private static bool IgnoravelNoInicio(Rune rune)
    {
        if (GatilhoDeFormula(rune))
            return false;

        if (Rune.IsWhiteSpace(rune))
            return true;

        var categoria = Rune.GetUnicodeCategory(rune);
        return categoria is UnicodeCategory.Format
            or UnicodeCategory.SpaceSeparator
            or UnicodeCategory.LineSeparator
            or UnicodeCategory.ParagraphSeparator;
    }

    private static bool GatilhoDeFormula(Rune rune)
        => rune.Value <= char.MaxValue && GatilhoDeFormula((char)rune.Value);

    private static bool GatilhoDeFormula(char caractere) => caractere is
        '=' or '+' or '-' or '@' or '\t' or '\r' or '\n'
        or '\uFF1D' or '\uFF0B' or '\uFF0D' or '\uFF20' or '\u2212';

    /// <summary>
    /// XML não aceita estes controles. O ClosedXML os grava como <c>_x0001_</c>,
    /// e o Excel mostra esse texto. Aqui viram espaço. Tab e quebra de linha ficam.
    /// </summary>
    private static string SemControle(string texto)
    {
        var temControle = false;
        foreach (var caractere in texto)
        {
            if (!EhControleInvalido(caractere))
                continue;

            temControle = true;
            break;
        }

        if (!temControle)
            return texto;

        var resultado = new StringBuilder(texto.Length);
        foreach (var caractere in texto)
            resultado.Append(EhControleInvalido(caractere) ? ' ' : caractere);

        return resultado.ToString();
    }

    private static bool EhControleInvalido(char caractere)
        => caractere < ' ' && caractere is not '\t' and not '\n' and not '\r'
            || caractere is '\uFFFE' or '\uFFFF';

    private static string Cortar(string texto)
    {
        var valor = texto.Contains('\n') || texto.Contains('\r') ? NormalizarQuebras(texto) : texto;
        if (valor.Length <= MaximoCaracteresCelula)
            return valor;

        var limite = MaximoCaracteresCelula;
        if (char.IsHighSurrogate(valor[limite - 1]))
            limite--;

        if (limite > 0 && valor[limite - 1] == '\r')
            limite--;

        return valor[..limite];
    }

    /// <summary>
    /// O ClosedXML troca CR, LF e CRLF por <see cref="Environment.NewLine"/> ao gravar.
    /// Normalizar antes evita estourar o teto de 32.767 quando \n vira \r\n.
    /// </summary>
    private static string NormalizarQuebras(string texto)
    {
        var quebra = Environment.NewLine;
        var resultado = new StringBuilder(texto.Length);
        for (var i = 0; i < texto.Length; i++)
        {
            if (texto[i] == '\r' && i + 1 < texto.Length && texto[i + 1] == '\n')
            {
                resultado.Append(quebra);
                i++;
            }
            else if (texto[i] is '\n' or '\r')
            {
                resultado.Append(quebra);
            }
            else
            {
                resultado.Append(texto[i]);
            }
        }

        return resultado.ToString();
    }
}
