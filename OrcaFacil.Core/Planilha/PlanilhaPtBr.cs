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

    private static readonly char[] CaracteresProibidosNaAba = [':', '\\', '/', '?', '*', '[', ']'];

    public static IXLWorksheet CriarAba(XLWorkbook pasta, string nome)
    {
        ArgumentNullException.ThrowIfNull(pasta);
        ValidarNomeAba(nome);
        return pasta.AddWorksheet(nome);
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
            var celula = aba.Cell(1, i + 1);
            GravarTexto(celula, colunas[i]);
            celula.Style.Font.Bold = true;
            aba.Column(i + 1).Width = LarguraColuna;
        }

        aba.SheetView.FreezeRows(1);
    }

    public static void GravarData(IXLCell celula, DateTime? data)
    {
        ArgumentNullException.ThrowIfNull(celula);
        if (data is null)
        {
            Limpar(celula);
            return;
        }

        var dia = DateTime.SpecifyKind(data.Value.Date, DateTimeKind.Unspecified);
        celula.Value = dia;
        celula.Style.DateFormat.Format = FormatoData;
    }

    public static void GravarDataHora(IXLCell celula, DateTime? data)
    {
        ArgumentNullException.ThrowIfNull(celula);
        if (data is null)
        {
            Limpar(celula);
            return;
        }

        var instante = data.Value;
        var semSegundos = new DateTime(
            instante.Year,
            instante.Month,
            instante.Day,
            instante.Hour,
            instante.Minute,
            0,
            DateTimeKind.Unspecified);
        celula.Value = semSegundos;
        celula.Style.DateFormat.Format = FormatoDataHora;
    }

    public static void GravarMoeda(IXLCell celula, decimal? valor)
    {
        ArgumentNullException.ThrowIfNull(celula);
        if (valor is null)
        {
            Limpar(celula);
            return;
        }

        celula.Value = (double)valor.Value;
        celula.Style.NumberFormat.Format = FormatoMoeda;
    }

    /// <summary>Número sem símbolo de moeda. Meta de redução, quantidade e percentual.</summary>
    public static void GravarNumero(IXLCell celula, decimal? valor)
    {
        ArgumentNullException.ThrowIfNull(celula);
        if (valor is null)
        {
            Limpar(celula);
            return;
        }

        celula.Value = (double)valor.Value;
    }

    public static void GravarSimNao(IXLCell celula, bool valor)
    {
        ArgumentNullException.ThrowIfNull(celula);
        GravarTexto(celula, valor ? Sim : Nao);
    }

    public static void GravarTexto(IXLCell celula, string? texto)
    {
        ArgumentNullException.ThrowIfNull(celula);
        var bruto = texto ?? "";
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

    private static void Limpar(IXLCell celula)
    {
        celula.Clear(XLClearOptions.Contents);
    }

    private static void ValidarNomeAba(string nome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);

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
    /// = + - @ no início, ou tab, CR e LF, fazem o Excel tratar a célula como fórmula
    /// quando a planilha é aberta ou convertida. Espaço antes do gatilho também.
    /// CR e LF entram juntos porque o ClosedXML grava os dois como quebra de linha.
    /// </summary>
    private static bool PrecisaNeutralizar(string texto)
    {
        foreach (var caractere in texto)
        {
            if (caractere == ' ')
                continue;

            return caractere is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n';
        }

        return false;
    }

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
