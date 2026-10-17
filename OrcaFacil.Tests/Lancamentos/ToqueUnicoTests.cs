using OrcaFacil.Core.Lancamentos;

namespace OrcaFacil.Tests.Lancamentos;

public class ToqueUnicoTests
{
    [Fact]
    public async Task DuploToque_EnquantoConfirma_IgnoraOSegundoELiberaDepois()
    {
        var toque = new ToqueUnico();
        var vezes = 0;
        var dentro = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var libera = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var primeira = Task.Run(async () =>
        {
            Assert.True(toque.Entrar());
            Interlocked.Increment(ref vezes);
            dentro.TrySetResult();
            await libera.Task;
            toque.Sair();
        });

        await dentro.Task;
        Assert.False(toque.Entrar());
        libera.TrySetResult();
        await primeira;

        Assert.Equal(1, vezes);
        Assert.True(toque.Entrar());
    }

    [Fact]
    public void EstornoDialog_ConfirmarEntraNoToqueAntesDeGravar()
    {
        var texto = File.ReadAllText(Path.Combine(EncontrarRaiz(), "OrcaFacil", "Components", "Pages", "Lancamentos", "EstornoDialog.razor"));
        var entrar = texto.IndexOf("_confirmacao.Entrar()", StringComparison.Ordinal);
        var gravar = texto.IndexOf("OnConfirmar.InvokeAsync", StringComparison.Ordinal);
        var sair = texto.IndexOf("_confirmacao.Sair()", StringComparison.Ordinal);

        Assert.True(entrar >= 0 && entrar < gravar);
        Assert.True(gravar < sair);
    }

    private static string EncontrarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OrcaFacil", "Components", "Pages", "Lancamentos", "EstornoDialog.razor")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar o EstornoDialog.");
    }
}
