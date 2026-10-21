namespace OrcaFacil.Core.Dirpf;

/// <summary>Ano-base sugerido para a DIRPF: o ano civil anterior à data local.</summary>
public static class AnoBaseDirpf
{
    public static int Anterior(DateTime hoje) => hoje.Year - 1;
}
