using Microsoft.AspNetCore.Components;

namespace OrcaFacil.Components.Layout;

/// <summary>
/// Permite que uma tela substitua o conteúdo da app bar sem criar um segundo toolbar.
/// </summary>
public sealed class CascoNavegacao
{
    public RenderFragment? Barra { get; private set; }

    public event Action? Alterado;

    public void Definir(RenderFragment? barra)
    {
        Barra = barra;
        Alterado?.Invoke();
    }

    public void Atualizar() => Alterado?.Invoke();
}
