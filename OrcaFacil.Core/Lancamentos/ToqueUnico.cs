namespace OrcaFacil.Core.Lancamentos;

/// <summary>
/// Segura o segundo toque em Confirmar enquanto o primeiro não terminou.
/// Sem isso, dois toques lançam dois estornos e abatem o total duas vezes.
/// </summary>
public sealed class ToqueUnico
{
    private int _emCurso;

    public bool Entrar() => Interlocked.CompareExchange(ref _emCurso, 1, 0) == 0;

    public void Sair() => Interlocked.Exchange(ref _emCurso, 0);
}
