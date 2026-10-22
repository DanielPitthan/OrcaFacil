using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OrcaFacil.Tests.Plataforma;

public class SombraNamespaceAndroidTests
{
    [Fact]
    public void QualificadorAndroid_DentroDoNamespaceDaPlataforma_ExigeGlobal()
    {
        var raiz = EncontrarRaiz();
        var arquivos = new List<string>();
        foreach (var arquivo in Directory.EnumerateFiles(raiz, "*.cs", SearchOption.AllDirectories))
        {
            if (arquivo.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                || arquivo.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                continue;

            arquivos.Add(arquivo);
        }

        var namespaces = SombraAndroid.ColetarNamespaces(arquivos);
        var achados = new List<string>();
        foreach (var arquivo in arquivos)
            achados.AddRange(SombraAndroid.Listar(arquivo, namespaces));

        Assert.True(achados.Count == 0, string.Join(Environment.NewLine, achados));
    }

    [Fact]
    public void QualificadorSemGlobal_NoNamespaceAndroid_GeraUmAchado()
    {
        const string fonte =
            "namespace OrcaFacil.Platforms.Android.X; class C { object o = Android.App.Application.Context; }";

        var achado = Assert.Single(SombraAndroid.Listar(fonte, "memoria.cs"));

        Assert.Contains("Android sem global::", achado, StringComparison.Ordinal);
    }

    [Fact]
    public void QualificadorComGlobal_NoNamespaceAndroid_NaoGeraAchado()
    {
        const string fonte =
            "namespace OrcaFacil.Platforms.Android.X; class C { object o = global::Android.App.Application.Context; }";

        Assert.Empty(SombraAndroid.Listar(fonte, "memoria.cs"));
    }

    [Fact]
    public void UsingAndroidNoTopo_ForaDoNamespace_NaoGeraAchado()
    {
        const string fonte = """
            using Android.App;

            namespace OrcaFacil.Platforms.Android.X;

            class C { }
            """;

        Assert.Empty(SombraAndroid.Listar(fonte, "memoria.cs"));
    }

    [Fact]
    public void NomeAndroid_ForaDeNamespaceComAndroid_NaoGeraAchado()
    {
        const string fonte =
            "namespace OrcaFacil.Outro; class C { object o = Android.App.Application.Context; }";

        Assert.Empty(SombraAndroid.Listar(fonte, "memoria.cs"));
    }

    [Theory]
    [InlineData("OrcaFacil.Platforms")]
    [InlineData("OrcaFacil.Platforms.iOS")]
    [InlineData("OrcaFacil.Platforms.Windows.Sub")]
    public void NamespaceIrmaoOuAncestral_SemGlobal_GeraAchado(string @namespace)
    {
        var fonte = $"namespace {@namespace}; class C {{ object o = Android.App.X; }}";

        var achado = Assert.Single(SombraAndroid.Listar(fonte, "memoria.cs"));

        Assert.Contains("Android sem global::", achado, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("OrcaFacil")]
    [InlineData("OrcaFacil.Outro")]
    [InlineData("OrcaFacilPlatforms")]
    public void NamespaceSemFilhoAndroid_NaoGeraAchado(string @namespace)
    {
        var fonte = $"namespace {@namespace}; class C {{ object o = Android.App.X; }}";

        Assert.Empty(SombraAndroid.Listar(fonte, "memoria.cs"));
    }

    [Fact]
    public void SubnamespaceLocal_Espelho_NaoGeraAchado()
    {
        const string fonte = """
            namespace OrcaFacil.Platforms.Android.Espelho;
            class C { object o = Android.Espelho.X; }
            """;

        Assert.Empty(SombraAndroid.Listar(fonte, "memoria.cs"));
    }

    [Fact]
    public void SubnamespaceLocal_MasAppNao_GeraUm()
    {
        const string fonte = """
            namespace OrcaFacil.Platforms.Android;
            class C
            {
                object espelho = Android.Espelho.X;
                object fotos = Android.Fotos.Y;
                object app = Android.App.X;
            }
            """;
        var locais = new HashSet<string>(StringComparer.Ordinal)
        {
            SombraAndroid.NamespaceAndroidLocal,
            SombraAndroid.NamespaceAndroidLocal + ".Espelho",
            SombraAndroid.NamespaceAndroidLocal + ".Fotos",
        };

        var achado = Assert.Single(SombraAndroid.Listar(fonte, "memoria.cs", locais));

        Assert.Equal("memoria.cs:6: Android sem global::", achado);
    }

    [Theory]
    [InlineData("atributo", false)]
    [InlineData("atributo", true)]
    [InlineData("typeof", false)]
    [InlineData("typeof", true)]
    [InlineData("nameof", false)]
    [InlineData("nameof", true)]
    [InlineData("cref", false)]
    [InlineData("cref", true)]
    [InlineData("alias", false)]
    [InlineData("alias", true)]
    [InlineData("using", false)]
    [InlineData("using", true)]
    public void ContextoNoNamespaceSombreado_ExigeGlobal(string contexto, bool comGlobal)
    {
        var android = comGlobal ? "global::Android" : "Android";
        var corpo = contexto switch
        {
            "atributo" => $"[{android}.Runtime.Register(\"x\")] class C {{ }}",
            "typeof" => $"class C {{ object o = typeof({android}.App.Activity); }}",
            "nameof" => $"class C {{ string s = nameof({android}.App); }}",
            "cref" => $"/// <summary><see cref=\"{android}.App.Activity\"/></summary>\nclass C {{ }}",
            "alias" => $"using A = {android}.App;\nclass C {{ }}",
            "using" => $"using {android}.App;\nclass C {{ }}",
            _ => throw new ArgumentOutOfRangeException(nameof(contexto), contexto, "Contexto desconhecido."),
        };
        var fonte = $$"""
            namespace OrcaFacil.Platforms.Android.X
            {
                {{corpo}}
            }
            """;

        var achados = SombraAndroid.Listar(fonte, "memoria.cs").ToList();

        if (comGlobal)
            Assert.Empty(achados);
        else
        {
            var achado = Assert.Single(achados);
            Assert.Contains("Android sem global::", achado, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ArquivoForaDePlatforms_ComNamespaceSombreado_GeraAchado()
    {
        const string semGlobal =
            "namespace OrcaFacil.Platforms.Android.X; class C { object o = Android.App.X; }";
        const string comGlobal =
            "namespace OrcaFacil.Platforms.Android.X; class C { object o = global::Android.App.X; }";

        var achado = Assert.Single(SombraAndroid.Listar(semGlobal, "Services/x.cs"));

        Assert.Equal("Services/x.cs:1: Android sem global::", achado);
        Assert.Empty(SombraAndroid.Listar(comGlobal, "Services/x.cs"));
    }

    [Fact]
    public void ColetarNamespaces_LeDeclaracoesFileScopedEBloco()
    {
        var pasta = Directory.CreateTempSubdirectory("sombra-android-");
        try
        {
            var bloco = Path.Combine(pasta.FullName, "Bloco.cs");
            var arquivo = Path.Combine(pasta.FullName, "Arquivo.cs");
            var futuro = Path.Combine(pasta.FullName, "Futuro.cs");
            File.WriteAllText(bloco, """
                namespace OrcaFacil.Um
                {
                    namespace Dois.Tres
                    {
                        class C { }
                    }
                }
                """);
            File.WriteAllText(arquivo, """
                namespace OrcaFacil.Platforms.Android.Espelho;
                class D { }
                """);
            File.WriteAllText(futuro, """
                namespace OrcaFacil.Futuro.Android.Widget;
                class E { }
                """);

            var namespaces = SombraAndroid.ColetarNamespaces([bloco, arquivo, futuro]);

            Assert.Contains("OrcaFacil.Um", namespaces);
            Assert.Contains("OrcaFacil.Um.Dois.Tres", namespaces);
            Assert.Contains("OrcaFacil.Platforms.Android.Espelho", namespaces);
            Assert.Contains("OrcaFacil.Futuro.Android.Widget", namespaces);

            var irmao = Assert.Single(SombraAndroid.Listar(
                "namespace OrcaFacil.Futuro; class C { object o = Android.App.X; }",
                "memoria.cs",
                namespaces));
            Assert.Contains("Android sem global::", irmao, StringComparison.Ordinal);
            var ios = Assert.Single(SombraAndroid.Listar(
                "namespace OrcaFacil.Platforms.iOS; class C { object o = Android.App.X; }",
                "memoria.cs",
                namespaces));
            Assert.Contains("Android sem global::", ios, StringComparison.Ordinal);
            Assert.Empty(SombraAndroid.Listar(
                "namespace OrcaFacil; class C { object o = Android.App.X; }",
                "memoria.cs",
                namespaces));

            var semPlataforma = SombraAndroid.ColetarNamespaces([bloco, futuro]);
            Assert.Empty(SombraAndroid.Listar(
                "namespace OrcaFacil.Platforms.iOS; class C { object o = Android.App.X; }",
                "memoria.cs",
                semPlataforma));
        }
        finally
        {
            pasta.Delete(recursive: true);
        }
    }

    private static string EncontrarRaiz()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null)
        {
            if (File.Exists(Path.Combine(diretorio.FullName, "OrcaFacil.slnx")))
                return diretorio.FullName;
            diretorio = diretorio.Parent;
        }

        throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}

static class SombraAndroid
{
    public const string NamespaceAndroidLocal = "OrcaFacil.Platforms.Android";

    public static IReadOnlySet<string> ColetarNamespaces(IEnumerable<string> arquivos)
    {
        var namespaces = new HashSet<string>(StringComparer.Ordinal);
        foreach (var arquivo in arquivos)
        {
            foreach (var declarado in Declaracoes(LerArvore(File.ReadAllText(arquivo), arquivo)))
                namespaces.Add(declarado);
        }

        return namespaces;
    }

    public static IEnumerable<string> Listar(string caminho, IReadOnlySet<string> namespacesLocais)
        => Listar(File.ReadAllText(caminho), caminho, namespacesLocais);

    public static IEnumerable<string> Listar(string texto, string caminho, IReadOnlySet<string>? namespacesLocais = null)
    {
        var raiz = LerArvore(texto, caminho);
        var locais = namespacesLocais ?? ComPadrao(Declaracoes(raiz));
        var conhecidos = IncluirPrefixos(locais);
        var subnamespaces = SubnamespacesAndroid(locais);

        foreach (var nome in raiz.DescendantNodes(descendIntoTrivia: true).OfType<IdentifierNameSyntax>())
        {
            if (nome.Identifier.ValueText != "Android")
                continue;

            var direito = SegmentoDireito(nome);
            if (direito is null || !EstaSombreado(nome, conhecidos) || subnamespaces.Contains(direito))
                continue;

            var linha = nome.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            yield return $"{caminho}:{linha}: Android sem global::";
        }
    }

    private static SyntaxNode LerArvore(string texto, string caminho)
        => CSharpSyntaxTree.ParseText(
            texto,
            new CSharpParseOptions(documentationMode: DocumentationMode.Parse),
            caminho).GetRoot();

    private static IReadOnlySet<string> ComPadrao(IEnumerable<string> declarados)
    {
        var locais = new HashSet<string>(StringComparer.Ordinal) { NamespaceAndroidLocal };
        foreach (var declarado in declarados)
            locais.Add(declarado);
        return locais;
    }

    private static HashSet<string> IncluirPrefixos(IReadOnlySet<string> namespacesLocais)
    {
        var conhecidos = new HashSet<string>(namespacesLocais, StringComparer.Ordinal);
        foreach (var declarado in namespacesLocais)
        {
            var partes = declarado.Split('.');
            var prefixo = "";
            foreach (var parte in partes)
            {
                if (parte.Length == 0)
                    continue;
                prefixo = prefixo.Length == 0 ? parte : prefixo + "." + parte;
                conhecidos.Add(prefixo);
            }
        }

        return conhecidos;
    }

    private static IReadOnlySet<string> SubnamespacesAndroid(IReadOnlySet<string> namespacesLocais)
    {
        var subs = new HashSet<string>(StringComparer.Ordinal);
        var raiz = NamespaceAndroidLocal + ".";
        foreach (var declarado in namespacesLocais)
        {
            if (!declarado.StartsWith(raiz, StringComparison.Ordinal))
                continue;

            var resto = declarado[raiz.Length..];
            var ponto = resto.IndexOf('.');
            var segmento = ponto < 0 ? resto : resto[..ponto];
            if (segmento.Length > 0)
                subs.Add(segmento);
        }

        return subs;
    }

    private static string? SegmentoDireito(IdentifierNameSyntax nome)
        => nome.Parent switch
        {
            QualifiedNameSyntax qualificado when qualificado.Left == nome => qualificado.Right.Identifier.ValueText,
            MemberAccessExpressionSyntax acesso when acesso.Expression == nome => acesso.Name.Identifier.ValueText,
            _ => null,
        };

    private static bool EstaSombreado(SyntaxNode nome, IReadOnlySet<string> namespacesConhecidos)
    {
        for (SyntaxNode? no = nome.Parent; no is not null; no = Proximo(no))
        {
            if (no is BaseNamespaceDeclarationSyntax ns && PrefixoTemFilhoAndroid(NomeCompleto(ns), namespacesConhecidos))
                return true;
        }

        return false;
    }

    private static bool PrefixoTemFilhoAndroid(string nomeCompleto, IReadOnlySet<string> namespacesConhecidos)
    {
        var prefixo = "";
        foreach (var parte in nomeCompleto.Split('.'))
        {
            if (parte.Length == 0)
                continue;
            prefixo = prefixo.Length == 0 ? parte : prefixo + "." + parte;
            if (namespacesConhecidos.Contains(prefixo + ".Android"))
                return true;
        }

        return false;
    }

    private static string NomeCompleto(BaseNamespaceDeclarationSyntax declaracao)
    {
        var nomes = new List<string>();
        for (SyntaxNode? no = declaracao; no is not null; no = no.Parent)
        {
            if (no is BaseNamespaceDeclarationSyntax ns)
                nomes.Add(string.Join('.', Partes(ns.Name)));
        }

        nomes.Reverse();
        return string.Join('.', nomes);
    }

    private static SyntaxNode? Proximo(SyntaxNode no)
        => no.Parent ?? (no is StructuredTriviaSyntax trivia ? trivia.ParentTrivia.Token.Parent : null);

    private static IEnumerable<string> Declaracoes(SyntaxNode raiz)
    {
        foreach (var ns in raiz.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
            yield return NomeCompleto(ns);
    }

    private static IEnumerable<string> Partes(NameSyntax nome)
    {
        if (nome is QualifiedNameSyntax qualificado)
        {
            foreach (var parte in Partes(qualificado.Left))
                yield return parte;
            yield return qualificado.Right.Identifier.ValueText;
            yield break;
        }

        if (nome is IdentifierNameSyntax simples)
            yield return simples.Identifier.ValueText;
    }
}
