using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OrcaFacil.Tests.Lancamentos;

public class TransacaoServiceTravaTests
{
    [Fact]
    public void EditarPelaTela_BloqueiaAtivoRendaFixaEProvento()
    {
        var fonte = File.ReadAllText(Path.Combine(EncontrarRaiz(), "OrcaFacil", "Services", "TransacaoService.cs"));
        var arvore = CSharpSyntaxTree.ParseText(fonte);
        var chamadas = arvore.GetRoot()
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(chamada => chamada.Expression.ToString().EndsWith("GarantirEditavelNaTela", StringComparison.Ordinal))
            .ToList();

        var porMetodo = chamadas.ToDictionary(
            chamada => chamada.Ancestors().OfType<MethodDeclarationSyntax>().First().Identifier.Text,
            chamada => chamada.ArgumentList.Arguments.Select(argumento => argumento.ToString()).ToList());

        Assert.Equal(
            ["AtualizarAsync", "ExcluirAsync", "MarcarComoPagoAsync"],
            porMetodo.Keys.OrderBy(nome => nome, StringComparer.Ordinal).ToArray());

        foreach (var argumentos in porMetodo.Values)
        {
            Assert.Equal(
                ["transacao.OperacaoAtivoId", "transacao.OperacaoRendaFixaId", "transacao.ProventoId"],
                argumentos);
        }
    }

    private static string EncontrarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OrcaFacil", "Services", "TransacaoService.cs")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar o TransacaoService.");
    }
}
