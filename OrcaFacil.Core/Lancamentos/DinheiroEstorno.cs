namespace OrcaFacil.Core.Lancamentos;

/// <summary>Magnitude do estorno em reais, com duas casas e meio afastando-se do zero.</summary>
public static class DinheiroEstorno
{
    public static decimal Arredondar(decimal valor)
        => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
