using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Tema;

/// <summary>
/// Parte de preferências de <c>ArmazenamentoService.LimparAsync</c>.
/// Só o zeramento total redefine. <c>Config.Tema</c> volta como estava.
/// </summary>
public static class LimpezaDePreferencias
{
    public static void Aplicar(TipoLimpezaDados tipo, PreferenciasTema preferencias)
    {
        if (tipo.HasFlag(TipoLimpezaDados.Tudo))
            preferencias.Redefinir();
    }
}
