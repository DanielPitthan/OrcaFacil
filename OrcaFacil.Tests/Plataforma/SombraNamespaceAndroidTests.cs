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
        var achados = new List<string>();
        foreach (var arquivo in Directory.EnumerateFiles(raiz, "*.cs", SearchOption.AllDirectories))
        {
            if (arquivo.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                || arquivo.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                continue;

            achados.AddRange(SombraAndroid.Listar(arquivo));
        }

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
    public static IEnumerable<string> Listar(string caminho)
        => Listar(File.ReadAllText(caminho), caminho);

    public static IEnumerable<string> Listar(string texto, string caminho)
    {
        var arvore = CSharpSyntaxTree.ParseText(
            texto,
            new CSharpParseOptions(documentationMode: DocumentationMode.Parse),
            caminho);
        foreach (var nome in arvore.GetRoot().DescendantNodes(descendIntoTrivia: true).OfType<IdentifierNameSyntax>())
        {
            if (nome.Identifier.ValueText != "Android" || !EhQualificador(nome) || !EstaSombreado(nome) || EhGlobal(nome))
                continue;

            var linha = nome.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            yield return $"{caminho}:{linha}: Android sem global::";
        }
    }

    private static bool EhQualificador(IdentifierNameSyntax nome)
        => nome.Parent is QualifiedNameSyntax qualificado && qualificado.Left == nome
            || nome.Parent is MemberAccessExpressionSyntax acesso && acesso.Expression == nome;

    private static bool EstaSombreado(SyntaxNode nome)
    {
        for (SyntaxNode? no = nome.Parent; no is not null; no = Proximo(no))
        {
            if (no is BaseNamespaceDeclarationSyntax ns && Partes(ns.Name).Contains("Android"))
                return true;
        }

        return false;
    }

    private static SyntaxNode? Proximo(SyntaxNode no)
        => no.Parent ?? (no is StructuredTriviaSyntax trivia ? trivia.ParentTrivia.Token.Parent : null);

    private static bool EhGlobal(IdentifierNameSyntax nome)
        => nome.Parent is AliasQualifiedNameSyntax alias && alias.Alias.Identifier.ValueText == "global";

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
