using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Armazenamento;
using OrcaFacil.Core.Data;

namespace OrcaFacil.Tests.Data;

public class PoliticaSemeaduraCoberturaTests
{
    [Fact]
    public void DbSetNovoForaDaPoliticaSemeadura_Falha()
    {
        var conjuntos = typeof(AppDbContext)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(propriedade => propriedade.PropertyType.IsGenericType
                && propriedade.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(propriedade => propriedade.Name)
            .ToHashSet(StringComparer.Ordinal);

        var observados = PoliticaSemeadura.ConjuntosObservados.ToHashSet(StringComparer.Ordinal);
        var semLista = conjuntos.Except(observados).Order(StringComparer.Ordinal).ToArray();
        Assert.True(
            semLista.Length == 0,
            "DbSet novo fora da PoliticaSemeadura: " + string.Join(", ", semLista));

        var fantasma = observados.Except(conjuntos).Order(StringComparer.Ordinal).ToArray();
        Assert.True(
            fantasma.Length == 0,
            "PoliticaSemeadura observa conjunto que não existe: " + string.Join(", ", fantasma));

        var fonte = File.ReadAllText(Path.Combine(EncontrarRaiz(), "OrcaFacil.Core", "Armazenamento", "PoliticaSemeadura.cs"));
        var metodos = CSharpSyntaxTree.ParseText(fonte)
            .GetCompilationUnitRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .ToDictionary(metodo => metodo.Identifier.Text, StringComparer.Ordinal);

        var abertura = AcessosDeContexto(metodos, "HaDadoDoUsuarioAsync");
        var backup = AcessosDeContexto(metodos, "ClassificarConteudoAsync");
        Assert.DoesNotContain("TabelaQueNaoExiste", abertura);
        Assert.DoesNotContain("TabelaQueNaoExiste", backup);

        var semAbertura = conjuntos.Except(abertura).Order(StringComparer.Ordinal).ToArray();
        Assert.True(
            semAbertura.Length == 0,
            "DbSet novo sem checagem de dado do usuário: " + string.Join(", ", semAbertura));

        var semBackup = conjuntos.Except(backup).Order(StringComparer.Ordinal).ToArray();
        Assert.True(
            semBackup.Length == 0,
            "DbSet novo sem classificação de conteúdo: " + string.Join(", ", semBackup));
    }

    private static HashSet<string> AcessosDeContexto(
        IReadOnlyDictionary<string, MethodDeclarationSyntax> metodos,
        string entrada)
    {
        var acessos = new HashSet<string>(StringComparer.Ordinal);
        var visitados = new HashSet<string>(StringComparer.Ordinal);
        var fila = new Queue<string>();
        fila.Enqueue(entrada);
        while (fila.Count > 0)
        {
            var nome = fila.Dequeue();
            if (!visitados.Add(nome) || !metodos.TryGetValue(nome, out var metodo))
                continue;

            var parametro = metodo.ParameterList.Parameters
                .FirstOrDefault(candidato => candidato.Type?.ToString() == "AppDbContext")
                ?.Identifier.Text;
            if (parametro is not null)
            {
                foreach (var acesso in metodo.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
                {
                    if (acesso.Expression is IdentifierNameSyntax alvo && alvo.Identifier.Text == parametro)
                        acessos.Add(acesso.Name.Identifier.Text);
                }
            }

            foreach (var chamada in metodo.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (chamada.Expression is IdentifierNameSyntax identificador)
                    fila.Enqueue(identificador.Identifier.Text);
            }
        }

        return acessos;
    }

    private static string EncontrarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OrcaFacil.Core", "Armazenamento", "PoliticaSemeadura.cs")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar PoliticaSemeadura.cs.");
    }
}
