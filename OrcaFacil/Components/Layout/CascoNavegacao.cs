using Microsoft.AspNetCore.Components;

namespace OrcaFacil.Components.Layout;

/// <summary>
/// Permite que uma tela substitua o conteúdo da app bar sem criar um segundo toolbar.
/// </summary>
public sealed class CascoNavegacao
{
    public RenderFragment? Barra { get; private set; }

    public event Action? Alterado;

    /// <summary>A barra personalizada pede para abrir o drawer. O layout é quem guarda esse estado.</summary>
    public event Action? AlternarMenu;

    public void Definir(RenderFragment? barra)
    {
        Barra = barra;
        Alterado?.Invoke();
    }

    public void SolicitarMenu() => AlternarMenu?.Invoke();

    public void Atualizar() => Alterado?.Invoke();
}
