using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Dirpf;

public static class CestaApuracaoIrTexto
{
    public static string Rotulo(this CestaApuracaoIr cesta) => cesta switch
    {
        CestaApuracaoIr.Comum => "Operações comuns",
        CestaApuracaoIr.DayTrade => "Day trade",
        CestaApuracaoIr.Fii => "FII e Fiagro",
        _ => throw new InvalidOperationException($"Cesta de apuração desconhecida: {cesta}.")
    };
}
