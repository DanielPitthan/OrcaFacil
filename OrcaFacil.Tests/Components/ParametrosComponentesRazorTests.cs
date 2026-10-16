using System.Text;
using System.Text.RegularExpressions;

namespace OrcaFacil.Tests.Components;

/// <summary>
/// O runtime do Blazor quebra a tela quando um .razor passa um atributo que o
/// componente não declara em [Parameter] (ComponentProperties.ThrowForUnknownIncomingParameterName).
/// Este teste varre o fonte, sem bUnit, para esse caso não voltar no simulador.
/// </summary>
public class ParametrosComponentesRazorTests
{
    [Fact]
    public void TelasNaoPassamParametroQueOComponenteNaoDeclara()
    {
        var erros = VarredorParametrosRazor.VarrerRepositorio(EncontrarRaiz());

        Assert.True(erros.Count == 0, string.Join(Environment.NewLine, erros));
    }

    [Fact]
    public void ReconheceIconDeFormRowEIgnoraAtributosDeFramework()
    {
        var arquivos = new (string Caminho, string Conteudo)[]
        {
            ("FormRow.razor", """
                @code {
                    [Parameter] public string? Icone { get; set; }
                    [Parameter] public string Rotulo { get; set; } = "";
                    [Parameter] public RenderFragment? ChildContent { get; set; }
                }
                """),
            ("CampoNumerico.razor", """
                @typeparam TNumber
                @code {
                    [Parameter] public TNumber Value { get; set; } = default!;
                    [Parameter] public EventCallback<TNumber> ValueChanged { get; set; }
                    [Parameter] public string? Label { get; set; }
                    [Parameter] public Expression<Func<TNumber>>? For { get; set; }
                }
                """),
            ("Caixa.razor", """
                @code {
                    [Parameter(CaptureUnmatchedValues = true)]
                    public IDictionary<string, object>? AdditionalAttributes { get; set; }
                    [Parameter] public string? Titulo { get; set; }
                }
                """),
            ("Vazio.razor", "@code { }"),
            ("Uso.razor", """
                <FormRow Icone="x" Rotulo="Ok" />
                <FormRow Icon="x" Rotulo="Quebra" />
                <CampoNumerico TNumber="decimal" @bind-Value="Modelo.Valor" @ref="_c" @key="item"
                               class="of" style="color:red" @onclick="Salvar" @onclick:preventDefault
                               For="@(() => Modelo.Valor)" Label="Valor" />
                <Caixa id="a" class="z" style="b" href="y" QualquerCoisa="1" @onmouseenter="X" />
                <div class="html" Icon="nao-e-componente"></div>
                <CampoNumerico SomaFormatada="@Total.ToString("C")" />
                <FormRow Titulo="@(_x is null ? "Adicionar item" : "Editar item")" />
                <Vazio @onclick="X" class="a" style="b" @ref="r" @key="k" />
                @* <FormRow Icon="comentado" /> *@
                """)
        };

        var erros = VarredorParametrosRazor.Varrer(arquivos);

        Assert.Equal(
            [
                "Uso.razor:2 FormRow não declara o parâmetro 'Icon'.",
                "Uso.razor:8 CampoNumerico não declara o parâmetro 'SomaFormatada'.",
                "Uso.razor:9 FormRow não declara o parâmetro 'Titulo'."
            ],
            erros.Select(e => e.ToString()).ToArray());
    }

    private static string EncontrarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "OrcaFacil", "Components"))
                && Directory.Exists(Path.Combine(dir.FullName, "OrcaFacil.Tests")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Não foi possível localizar a raiz do repositório a partir de " + AppContext.BaseDirectory);
    }
}

public static class VarredorParametrosRazor
{
    private static readonly Regex AtributoParametro = new(@"\[Parameter\b[^\]]*\]", RegexOptions.Compiled);
    private static readonly Regex NomeParametro = new(@"(\w+)\s*$", RegexOptions.Compiled);
    private static readonly Regex ParametroTipo = new(@"@typeparam\s+(\w+)", RegexOptions.Compiled);

    public static IReadOnlyList<AtributoDesconhecido> VarrerRepositorio(string raiz)
    {
        var pasta = Path.Combine(raiz, "OrcaFacil", "Components");
        var arquivos = Directory.EnumerateFiles(pasta, "*.razor", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(caminho =>
            {
                var conteudo = File.ReadAllText(caminho);
                var codeBehind = caminho + ".cs";
                if (File.Exists(codeBehind))
                    conteudo += Environment.NewLine + File.ReadAllText(codeBehind);

                return (Caminho: Path.GetRelativePath(raiz, caminho), Conteudo: conteudo);
            })
            .ToArray();

        return Varrer(arquivos);
    }

    public static IReadOnlyList<AtributoDesconhecido> Varrer(
        IReadOnlyList<(string Caminho, string Conteudo)> arquivos)
    {
        var componentes = new Dictionary<string, Componente>(StringComparer.Ordinal);
        var erros = new List<AtributoDesconhecido>();

        foreach (var (caminho, conteudo) in arquivos)
        {
            var nome = Path.GetFileNameWithoutExtension(caminho);
            if (!componentes.TryAdd(nome, LerComponente(nome, conteudo)))
            {
                erros.Add(new AtributoDesconhecido(
                    caminho, 1, nome, nome, "nome de componente duplicado"));
            }
        }

        foreach (var (caminho, conteudo) in arquivos)
        {
            var texto = ApagarComentarios(conteudo);
            foreach (var uso in EncontrarUsos(texto, componentes))
            {
                var componente = componentes[uso.Nome];
                if (componente.CapturaAtributosSoltos)
                    continue;

                foreach (var atributo in LerAtributos(uso.Corpo))
                {
                    if (AtributoPermitido(atributo, componente))
                        continue;

                    erros.Add(new AtributoDesconhecido(caminho, uso.Linha, uso.Nome, atributo));
                }
            }
        }

        return erros
            .OrderBy(e => e.Arquivo, StringComparer.Ordinal)
            .ThenBy(e => e.Linha)
            .ThenBy(e => e.Atributo, StringComparer.Ordinal)
            .ToArray();
    }

    private static Componente LerComponente(string nome, string conteudo)
    {
        var parametros = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var captura = false;
        foreach (Match atributo in AtributoParametro.Matches(conteudo))
        {
            if (atributo.Value.Contains("CaptureUnmatchedValues", StringComparison.OrdinalIgnoreCase)
                && atributo.Value.Contains("true", StringComparison.OrdinalIgnoreCase))
                captura = true;

            var inicio = atributo.Index + atributo.Length;
            var chave = conteudo.IndexOf('{', inicio);
            if (chave < 0)
                continue;

            var nomeParametro = NomeParametro.Match(conteudo[inicio..chave]);
            if (nomeParametro.Success)
                parametros.Add(nomeParametro.Groups[1].Value);
        }

        var tipos = ParametroTipo.Matches(conteudo)
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new Componente(nome, parametros, tipos, captura);
    }

    private static bool AtributoPermitido(string atributo, Componente componente)
    {
        if (atributo.Equals("@ref", StringComparison.OrdinalIgnoreCase)
            || atributo.Equals("@key", StringComparison.OrdinalIgnoreCase)
            || atributo.Equals("@attributes", StringComparison.OrdinalIgnoreCase))
            return true;

        // Diretivas do compilador, não parâmetros. @bind-Valor aponta para o parâmetro Valor.
        if (atributo.StartsWith("@bind-", StringComparison.OrdinalIgnoreCase))
        {
            var propriedade = atributo["@bind-".Length..];
            var separador = propriedade.IndexOf(':');
            if (separador >= 0)
                propriedade = propriedade[..separador];

            return componente.Parametros.Contains(propriedade);
        }

        // @onclick e @onclick:preventDefault são diretivas, não [Parameter].
        if (atributo.StartsWith("@on", StringComparison.OrdinalIgnoreCase))
            return true;

        // class e style são atributos HTML. Não comparar com [Parameter] evita falso positivo.
        if (atributo.Equals("class", StringComparison.OrdinalIgnoreCase)
            || atributo.Equals("style", StringComparison.OrdinalIgnoreCase))
            return true;

        if (componente.ParametrosTipo.Contains(atributo))
            return true;

        return componente.Parametros.Contains(atributo);
    }

    private static List<(string Nome, int Linha, string Corpo)> EncontrarUsos(
        string texto, IReadOnlyDictionary<string, Componente> componentes)
    {
        var usos = new List<(string, int, string)>();
        var i = 0;
        while (i < texto.Length)
        {
            var abre = texto.IndexOf('<', i);
            if (abre < 0)
                break;

            var cursor = abre + 1;
            if (cursor < texto.Length && texto[cursor] == '/')
            {
                i = cursor + 1;
                continue;
            }

            var inicioNome = cursor;
            if (cursor >= texto.Length || (!char.IsLetter(texto[cursor]) && texto[cursor] != '_'))
            {
                i = abre + 1;
                continue;
            }

            cursor++;
            while (cursor < texto.Length && (char.IsLetterOrDigit(texto[cursor]) || texto[cursor] == '_'))
                cursor++;

            var nome = texto[inicioNome..cursor];
            if (!componentes.ContainsKey(nome)
                || (cursor < texto.Length && texto[cursor] is not (' ' or '\t' or '\r' or '\n' or '/' or '>')))
            {
                i = abre + 1;
                continue;
            }

            var fim = FimDaTag(texto, cursor);
            if (fim < 0)
            {
                i = abre + 1;
                continue;
            }

            var linha = 1;
            for (var n = 0; n < abre; n++)
            {
                if (texto[n] == '\n')
                    linha++;
            }

            usos.Add((nome, linha, texto[cursor..fim]));
            i = fim + 1;
        }

        return usos;
    }

    private static int FimDaTag(string texto, int indice)
    {
        while (indice < texto.Length)
        {
            var caractere = texto[indice];
            if (caractere is '"' or '\'')
            {
                indice = FimDoValorEntreAspas(texto, indice);
                continue;
            }

            if (caractere == '@')
            {
                indice = FimDaExpressao(texto, indice);
                continue;
            }

            if (caractere == '>')
                return indice;

            indice++;
        }

        return -1;
    }

    private static List<string> LerAtributos(string corpo)
    {
        var texto = corpo.Trim();
        if (texto.EndsWith('/'))
            texto = texto[..^1].TrimEnd();

        var atributos = new List<string>();
        var i = 0;
        while (i < texto.Length)
        {
            while (i < texto.Length && char.IsWhiteSpace(texto[i]))
                i++;
            if (i >= texto.Length)
                break;

            var inicio = i;
            if (texto[i] == '@')
                i++;

            while (i < texto.Length && (char.IsLetterOrDigit(texto[i]) || texto[i] is '_' or '-' or ':'))
                i++;

            var nome = texto[inicio..i];
            if (nome.Length == 0 || nome == "@")
            {
                i = Math.Max(i, inicio + 1);
                continue;
            }

            while (i < texto.Length && char.IsWhiteSpace(texto[i]))
                i++;

            if (i < texto.Length && texto[i] == '=')
            {
                i++;
                while (i < texto.Length && char.IsWhiteSpace(texto[i]))
                    i++;
                if (i >= texto.Length)
                    break;

                if (texto[i] is '"' or '\'')
                    i = FimDoValorEntreAspas(texto, i);
                else if (texto[i] == '@')
                    i = FimDaExpressao(texto, i);
                else
                {
                    while (i < texto.Length && !char.IsWhiteSpace(texto[i]))
                        i++;
                }
            }

            atributos.Add(nome);
        }

        return atributos;
    }

    private static int FimDoValorEntreAspas(string texto, int indice)
    {
        var aspas = texto[indice];
        indice++;
        while (indice < texto.Length)
        {
            if (texto[indice] == '@')
            {
                indice = FimDaExpressao(texto, indice);
                continue;
            }

            if (texto[indice] == aspas)
                return indice + 1;

            indice++;
        }

        return indice;
    }

    private static int FimDaExpressao(string texto, int indice)
    {
        if (indice + 1 < texto.Length && texto[indice + 1] == '@')
            return indice + 2;

        if (indice + 1 < texto.Length && texto[indice + 1] == '(')
            return FimDaExpressaoExplicita(texto, indice + 2);

        return FimDaExpressaoImplicita(texto, indice + 1);
    }

    private static int FimDaExpressaoExplicita(string texto, int indice)
    {
        var profundidade = 1;
        while (indice < texto.Length && profundidade > 0)
        {
            if (texto[indice] is '"' or '\'')
            {
                indice = FimDaCadeiaCsharp(texto, indice);
                continue;
            }

            if (texto[indice] == '(')
                profundidade++;
            else if (texto[indice] == ')')
                profundidade--;

            indice++;
        }

        return indice;
    }

    private static int FimDaExpressaoImplicita(string texto, int indice)
    {
        var profundidade = 0;
        while (indice < texto.Length)
        {
            var caractere = texto[indice];
            if (profundidade > 0 && caractere is '"' or '\'')
            {
                indice = FimDaCadeiaCsharp(texto, indice);
                continue;
            }

            if (caractere is '(' or '[' or '{')
            {
                profundidade++;
                indice++;
                continue;
            }

            if (caractere is ')' or ']' or '}')
            {
                if (profundidade == 0)
                    return indice;

                profundidade--;
                indice++;
                continue;
            }

            if (profundidade == 0 && (char.IsWhiteSpace(caractere) || caractere is '"' or '\'' or ',' or ';' or '<' or '>'))
                return indice;

            indice++;
        }

        return indice;
    }

    private static int FimDaCadeiaCsharp(string texto, int indice)
    {
        var aspas = texto[indice];
        indice++;
        while (indice < texto.Length)
        {
            if (texto[indice] == '\\')
            {
                indice = Math.Min(indice + 2, texto.Length);
                continue;
            }

            if (texto[indice] == aspas)
                return indice + 1;

            indice++;
        }

        return indice;
    }

    private static string ApagarComentarios(string texto)
    {
        var saida = new StringBuilder(texto.Length);
        for (var i = 0; i < texto.Length; i++)
        {
            if (texto[i] == '@' && i + 1 < texto.Length && texto[i + 1] == '*')
            {
                saida.Append("  ");
                i += 2;
                while (i < texto.Length && !(texto[i] == '*' && i + 1 < texto.Length && texto[i + 1] == '@'))
                {
                    saida.Append(texto[i] == '\n' ? '\n' : ' ');
                    i++;
                }

                if (i + 1 < texto.Length)
                {
                    saida.Append("  ");
                    i++;
                }

                continue;
            }

            if (texto[i] == '<' && i + 3 < texto.Length && texto[i + 1] == '!' && texto[i + 2] == '-' && texto[i + 3] == '-')
            {
                saida.Append("    ");
                i += 4;
                while (i < texto.Length && !(texto[i] == '-' && i + 2 < texto.Length && texto[i + 1] == '-' && texto[i + 2] == '>'))
                {
                    saida.Append(texto[i] == '\n' ? '\n' : ' ');
                    i++;
                }

                if (i + 2 < texto.Length)
                {
                    saida.Append("   ");
                    i += 2;
                }

                continue;
            }

            saida.Append(texto[i]);
        }

        return saida.ToString();
    }

    private sealed record Componente(
        string Nome,
        HashSet<string> Parametros,
        HashSet<string> ParametrosTipo,
        bool CapturaAtributosSoltos);
}

public sealed record AtributoDesconhecido(string Arquivo, int Linha, string Componente, string Atributo, string? Motivo = null)
{
    public override string ToString()
        => Motivo is null
            ? $"{Arquivo}:{Linha} {Componente} não declara o parâmetro '{Atributo}'."
            : $"{Arquivo}:{Linha} {Componente}: {Motivo}.";
}
