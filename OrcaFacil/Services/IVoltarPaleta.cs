namespace OrcaFacil.Services;

internal interface IVoltarPaleta
{
    void Abrir(Action fechar);

    void Fechar(Action fechar);
}

/// <summary>
/// Fora do Android o voltar do sistema não passa por aqui.
/// </summary>
internal sealed class VoltarPaletaInerte : IVoltarPaleta
{
    public void Abrir(Action fechar) => ArgumentNullException.ThrowIfNull(fechar);

    public void Fechar(Action fechar) => ArgumentNullException.ThrowIfNull(fechar);
}
