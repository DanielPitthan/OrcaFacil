namespace OrcaFacil.Core.Enums;

/// <summary>
/// Cesta da apuração. Operações comuns, day trade e FII/Fiagro não se compensam.
/// Fiagro entra na cesta de FII. Day trade de FII ou Fiagro permanece nessa cesta.
/// </summary>
public enum CestaApuracaoIr
{
    Comum = 0,
    DayTrade = 1,
    Fii = 2
}
